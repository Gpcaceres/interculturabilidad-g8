"""Genera el modelo base de las 8 piezas de los pulmones en pulmones.blend.

Uso (desde la raíz del repo):
    blender -b Modelado/pulmones/pulmones.blend --python Modelado/scripts/pulmones_modelo_base.py
    blender -b Modelado/pulmones/pulmones.blend --python Modelado/scripts/pulmones_modelo_base.py -- --forzar

Sin --forzar se niega a correr si ya hay piezas PUL_* (para no pisar trabajo
hecho a mano). Con --forzar las borra y las vuelve a crear.

Convenciones:
  - El frente del cuerpo mira hacia -Y; la derecha del paciente es -X; arriba es +Z.
  - Medidas en cm aquí, guardadas en metros. Cada pieza tiene su origen en su centro.
  - Los lóbulos son volúmenes implícitos (distancia aproximada) que se convierten en
    malla con OpenVDB; las cisuras son superficies que los separan.
  - Respiración: shape key "Inhalar" en las piezas que se mueven. Todas usan el mismo
    campo de desplazamiento (inhalar()), así los lóbulos no se separan al respirar.

Salidas: piezas en el .blend, texturas en texturas/ y pulmones_flujo.json (recorridos
del aire, ya en ejes de Unity) en Exportados/. Se copian también al proyecto de Unity.
"""
import json
import math
import os
import shutil
import sys

import bmesh
import bpy
import numpy as np
import openvdb

CM = 0.01
VOXEL = 0.12
RNG = np.random.default_rng(8)
TRIS_LOBULO = 2600
PUNTAS_POR_LOBULO = 21
LADO_TEXTURA = 1024

LOBULOS = ["Pulmon_Der_Lobulo_Sup", "Pulmon_Der_Lobulo_Med", "Pulmon_Der_Lobulo_Inf",
           "Pulmon_Izq_Lobulo_Sup", "Pulmon_Izq_Lobulo_Inf"]
PARTES = ["Traquea", "Bronquios", *LOBULOS, "Diafragma"]

CARINA = np.array([0.0, 0.9, 4.2])
CIMA = np.array([0.0, 0.1, 17.5])
HILIO = {"Der": np.array([-3.1, 1.0, 1.9]), "Izq": np.array([4.0, 1.2, 1.0])}


# --- Formas implícitas (cm; negativo = dentro) ---------------------------------
def smax(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.maximum(a, b) + h * h * k * 0.25


def suave(x, a, b):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def elipsoide(p, c, r):
    q = (p - np.asarray(c)) / np.asarray(r)
    n = np.sqrt((q * q).sum(-1)) + 1e-9
    return (n - 1.0) * min(r)


def cupula(x, y):
    """Altura del diafragma: dos cúpulas, la derecha más alta (por el hígado)."""
    d = -5.6 - 0.115 * ((x + 6.4) ** 2 + 0.85 * (y - 0.3) ** 2)
    i = -6.7 - 0.115 * ((x - 6.4) ** 2 + 0.85 * (y - 0.3) ** 2)
    centro = -8.0 - 0.10 * x ** 2 - 0.06 * y ** 2          # bajo el corazón
    return np.logaddexp(np.logaddexp(d * 0.8, i * 0.8), centro * 0.8) / 0.8


def pulmon(p, lado):
    s = -1.0 if lado == "Der" else 1.0
    x, y, z = p[..., 0] * s, p[..., 1], p[..., 2]   # x > 0 hacia fuera
    t = np.clip((z + 13.0) / 25.5, 0.0, 1.0)
    perfil = np.sqrt(np.maximum(1 - t ** 2.5, 0.004))
    rx = (5.75 if lado == "Der" else 5.45) * perfil
    ry = 7.1 * perfil
    cx = 1.3 + rx + 2.4 * (1 - perfil)
    cy = 0.2 + 0.7 * (1 - perfil)
    n = 2.4
    r = ((np.abs(x - cx) / rx) ** n + (np.abs(y - cy) / ry) ** n) ** (1 / n)
    f = (r - 1.0) * np.minimum(rx, ry)
    f = smax(f, z - 12.4, 0.8)
    # Huellas suaves de las costillas y ondulación orgánica.
    f = f + 0.07 * np.sin((z + 0.35 * y) * 2 * math.pi / 2.5) * suave(x, 5.0, 8.0) * suave(z, -9, -5)
    f = f + 0.10 * np.sin(x * 0.9 + 1.3) * np.sin(y * 0.8 + z * 0.5) * np.sin(z * 0.7)
    # Base cóncava sobre el diafragma.
    f = smax(f, (cupula(p[..., 0], y) + 0.12 - z) * 0.75, 0.5)
    f = smax(f, -10.6 - z, 1.0)
    # Mediastino: grandes vasos, columna y corazón (inclinado a la izquierda).
    f = smax(f, 2.3 - np.sqrt((x - 0.2) ** 2 + (y - 0.2) ** 2), 1.0)
    f = smax(f, 2.9 - np.sqrt(x ** 2 + (y - 5.6) ** 2), 1.0)
    f = smax(f, -elipsoide(p, (1.5, -2.6, -3.6), (4.6, 4.0, 5.4)), 1.0)
    if lado == "Izq":   # incisura cardíaca: deja debajo la língula
        f = smax(f, -elipsoide(p, (2.4, -6.3, -3.4), (3.4, 2.9, 3.0)), 0.9)
    f = smax(f, -elipsoide(p, HILIO[lado], (1.5, 1.4, 1.9) if lado == "Der" else (1.1, 1.0, 1.4)), 0.7)
    return f


def cisura_oblicua(p, lado):
    """Positivo = por delante y encima (lóbulos superior y medio)."""
    s = -1.0 if lado == "Der" else 1.0
    x, y, z = p[..., 0] * s, p[..., 1], p[..., 2]
    alto = 5.0 + 1.60 * (y - 5.6) + 0.35 * np.sin(x * 0.5) + 0.22 * np.sin(y * 0.9)
    return (z - alto) / 1.9


def cisura_horizontal(p):
    """Positivo = por encima (lóbulo superior derecho)."""
    x, y, z = p[..., 0], p[..., 1], p[..., 2]
    return z - (0.4 - 0.05 * y + 0.18 * np.sin(x * 0.7) + 0.12 * np.sin(y))


HOLGURA, REDONDEO = 0.06, 0.7


def lobulo(p, nombre):
    lado = "Der" if "_Der_" in nombre else "Izq"
    f = pulmon(p, lado)
    ob = cisura_oblicua(p, lado)
    if nombre.endswith("Inf"):
        return smax(f, ob + HOLGURA, REDONDEO)
    f = smax(f, -ob + HOLGURA, REDONDEO)
    if lado == "Der":
        h = cisura_horizontal(p)
        f = smax(f, (-h if nombre.endswith("Sup") else h) + HOLGURA, REDONDEO)
    return f


def inhalar(p):
    """Campo de desplazamiento (cm) de la inspiración: baja la base y ensancha el tórax."""
    d = np.zeros_like(p)
    x, y, z = p[..., 0], p[..., 1], p[..., 2]
    alto = 1 - suave(z, 8.0, 14.0)
    d[..., 0] = 0.042 * x * alto
    d[..., 1] = 0.045 * (y - 5.0) * alto * suave(np.abs(x), 0.5, 4.0)
    d[..., 2] = -1.15 * (1 - suave(z, -9.0, 7.5))
    return d


# --- Utilidades de malla ---------------------------------------------------------
def crear_objeto(parte, verts, caras, uvs=None):
    me = bpy.data.meshes.new(f"PUL_{parte}")
    me.from_pydata((np.asarray(verts) * CM).tolist(), [], [tuple(int(i) for i in c) for c in caras])
    if uvs is not None:
        capa = me.uv_layers.new(name="UVMap")
        plano = [uv for cara in uvs for uv in cara]
        capa.data.foreach_set("uv", np.asarray(plano, dtype=np.float32).ravel())
    me.update()
    obj = bpy.data.objects.new(f"PUL_{parte}", me)
    bpy.data.collections[f"PUL_{parte}"].objects.link(obj)
    me.shade_smooth()
    return obj


def activar(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def triangulos(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def desplegar(obj):
    activar(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.012)
    bpy.ops.object.mode_set(mode="OBJECT")


def dejar_isla_mayor(obj):
    """Descarta astillas sueltas que dejan los recortes."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    pendientes, islas = set(bm.verts), []
    while pendientes:
        pila, isla = [pendientes.pop()], []
        while pila:
            v = pila.pop()
            isla.append(v)
            for e in v.link_edges:
                o = e.other_vert(v)
                if o in pendientes:
                    pendientes.remove(o)
                    pila.append(o)
        islas.append(isla)
    islas.sort(key=len, reverse=True)
    for isla in islas[1:]:
        bmesh.ops.delete(bm, geom=isla, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()


def malla_lobulo(nombre):
    der = "_Der_" in nombre
    lo = np.array([-13.8 if der else 0.0, -8.2, -13.6])
    hi = np.array([0.0 if der else 13.8, 8.6, 13.4])
    n = np.ceil((hi - lo) / VOXEL).astype(int) + 1
    ejes = [lo[i] + np.arange(n[i]) * VOXEL for i in range(3)]
    p = np.stack(np.meshgrid(*ejes, indexing="ij"), -1)
    campo = np.clip(lobulo(p, nombre), -0.6, 0.6).astype(np.float32)
    rejilla = openvdb.FloatGrid(0.6)
    rejilla.copyFromArray(campo)
    pts, quads = rejilla.convertToQuads(0.0)
    obj = crear_objeto(nombre, pts * VOXEL + lo, quads)
    dejar_isla_mayor(obj)

    activar(obj)
    s = obj.modifiers.new("Suave", "SMOOTH")
    s.iterations = 4
    d = obj.modifiers.new("Reducir", "DECIMATE")
    d.ratio = TRIS_LOBULO / triangulos(obj)
    for m in ("Suave", "Reducir"):
        bpy.ops.object.modifier_apply(modifier=m)
    obj.data.shade_smooth()
    desplegar(obj)
    return obj


# --- Vías aéreas -----------------------------------------------------------------
def bezier(a, b, c, d, n):
    t = np.linspace(0, 1, n)[:, None]
    return (1 - t)**3 * a + 3 * (1 - t)**2 * t * b + 3 * (1 - t) * t * t * c + t**3 * d


class Tubos:
    """Acumula tubos (anillos de vértices alrededor de una línea) en una sola malla."""

    def __init__(self):
        self.v, self.c, self.uv = [], [], []

    def tubo(self, pts, radio, lados, anillos=0.0, posterior_plana=False, tapa=False):
        """anillos: separación (cm) entre cartílagos, 0 = liso. El UV v marca el cartílago (entero)."""
        pts = np.asarray(pts, float)
        radios = np.broadcast_to(np.asarray(radio, float), (len(pts),))
        largo = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
        tang = np.gradient(pts, axis=0)
        tang /= np.linalg.norm(tang, axis=1)[:, None]
        n = np.array([0.0, 1.0, 0.0])
        if abs(tang[0] @ n) > 0.9:
            n = np.array([1.0, 0.0, 0.0])
        base = len(self.v)
        ang = np.linspace(0, 2 * math.pi, lados, endpoint=False)
        trasero = np.clip(np.cos(ang), 0, 1)        # 1 = cara posterior
        for i, p in enumerate(pts):
            n = n - tang[i] * (n @ tang[i])
            n /= np.linalg.norm(n)
            b = np.cross(tang[i], n)
            r = np.full(lados, radios[i])
            if anillos:
                onda = 0.5 + 0.5 * math.cos(2 * math.pi * largo[i] / anillos)
                r += radios[i] * 0.13 * (onda ** 0.6 - 0.4) * (1 - suave(trasero, 0.45, 0.8))
            if posterior_plana:
                r *= 1 - 0.28 * suave(trasero, 0.35, 1.0)
            for k in range(lados):
                self.v.append(p + (n * math.cos(ang[k]) + b * math.sin(ang[k])) * r[k])
        vs = largo / anillos if anillos else np.full(len(pts), 0.5)
        for i in range(len(pts) - 1):
            for k in range(lados):
                k2 = (k + 1) % lados
                a0, a1 = base + i * lados + k, base + i * lados + k2
                b0, b1 = a0 + lados, a1 + lados
                self.c.append((a0, a1, b1, b0))
                u0, u1 = k / lados, (k + 1) / lados
                self.uv.append([(u0, vs[i]), (u1, vs[i]), (u1, vs[i + 1]), (u0, vs[i + 1])])
        if tapa:
            f = base + (len(pts) - 1) * lados
            self.v.append(pts[-1] + tang[-1] * radios[-1] * 0.8)
            fin = len(self.v) - 1
            for k in range(lados):
                self.c.append((f + k, f + (k + 1) % lados, fin))
                self.uv.append([(0, 0.5), (0.5, 0.5), (0.5, 0.5)])


def puntas(nombre, n):
    """Puntos bien repartidos en el interior del lóbulo (muestreo del más lejano)."""
    der = "_Der_" in nombre
    q = RNG.uniform([-13 if der else 1, -7.5, -12.5], [-1 if der else 13, 8, 12.3], (90000, 3))
    q = q[lobulo(q, nombre) < -0.75]
    elegidos = [q[np.argmin(np.linalg.norm(q - q.mean(0), axis=1))]]
    d = np.linalg.norm(q - elegidos[0], axis=1)
    while len(elegidos) < n:
        elegidos.append(q[np.argmax(d)])
        d = np.minimum(d, np.linalg.norm(q - elegidos[-1], axis=1))
    return np.array(elegidos)


def crecer(tubos, origen, direccion, pts, r0, total, camino, rutas):
    """Divide las puntas en dos grupos una y otra vez: cada división es una bifurcación."""
    n = len(pts)
    r = max(r0 * (n / total) ** 0.42, 0.085)
    fin = pts[0] if n == 1 else origen + (pts.mean(0) - origen) * 0.46
    largo = np.linalg.norm(fin - origen)
    recta = (fin - origen) / max(largo, 1e-6)
    muestras = 5 if n > 3 else 4
    linea = bezier(origen, origen + direccion * largo * 0.35, fin - recta * largo * 0.25, fin, muestras)
    r_fin = max(r0 * (n / 2 / total) ** 0.42, 0.075) if n > 1 else 0.05
    tubos.tubo(linea, np.linspace(r, r_fin, muestras), 8 if n > 8 else 6 if n > 2 else 5, tapa=(n == 1))
    camino = camino + [linea[1:]]
    if n == 1:
        rutas.append(np.concatenate(camino))
        return
    eje = np.linalg.svd(pts - pts.mean(0), full_matrices=False)[2][0]
    orden = np.argsort((pts - pts.mean(0)) @ eje)
    m = n // 2
    sale = linea[-1] - linea[-2]
    sale /= np.linalg.norm(sale)
    for grupo in (pts[orden[:m]], pts[orden[m:]]):
        crecer(tubos, fin, sale, grupo, r0, total, camino, rutas)


def vias_aereas():
    tr = Tubos()
    n_anillos = 18
    paso = np.linalg.norm(CIMA - CARINA) / n_anillos
    eje = bezier(CIMA, CIMA + [0, 0.1, -4.5], CARINA + [0, 0.1, 4.5], CARINA + [0, 0, -0.35], n_anillos * 5 + 1)
    radios = np.full(len(eje), 0.95)
    radios[-8:] = np.linspace(0.95, 1.12, 8)          # se ensancha en la carina
    tr.tubo(eje, radios, 20, anillos=paso, posterior_plana=True)
    # Borde superior grueso (cartílago cricoides).
    tr.tubo([CIMA + [0, 0, z] for z in (0.75, 0.6, 0.2, -0.1)], [1.0, 1.16, 1.18, 1.0], 20)

    br, rutas = Tubos(), []
    for lado, r0 in (("Der", 0.74), ("Izq", 0.62)):
        s = -1.0 if lado == "Der" else 1.0
        h = HILIO[lado]
        ini = CARINA + [s * 0.35, 0, -0.1]
        linea = bezier(ini, ini + [s * 0.9, 0.0, -0.75], h - np.array([s * 1.0, 0, -0.5]), h, 25)
        br.tubo(linea, np.linspace(r0 + 0.08, r0 - 0.1, 25), 14, anillos=0.62, posterior_plana=True)
        sale = linea[-1] - linea[-2]
        sale /= np.linalg.norm(sale)
        for nombre in [l for l in LOBULOS if f"_{lado}_" in l]:
            pts = puntas(nombre, PUNTAS_POR_LOBULO)
            crecer(br, h, sale, pts, r0 * 0.72, len(pts), [eje, linea], rutas)
    return tr, br, rutas


def diafragma():
    anillos, sectores = 15, 48
    capas = []
    for dz in (0.0, -0.38):
        capa = []
        for i in range(anillos + 1):
            r = i / anillos
            for k in range(sectores):
                a = 2 * math.pi * k / sectores
                x, y = 12.9 * r * math.cos(a), 0.35 + 7.7 * r * math.sin(a)
                # Escotadura posterior de la columna.
                y -= 2.0 * math.exp(-(x / 2.6) ** 2) * suave(y, 2.0, 8.0)
                capa.append((x, y, float(cupula(x, y)) + dz))
        capas.append(np.array(capa))
    # Relieve de las fibras musculares hacia el borde.
    ang = np.tile(np.arange(sectores), anillos + 1)
    rr = np.repeat(np.arange(anillos + 1) / anillos, sectores)
    capas[0][:, 2] += 0.06 * np.where(ang % 2 == 0, 1, -1) * suave(rr, 0.45, 0.8)
    m = (anillos + 1) * sectores
    caras = []
    for i in range(anillos):
        for k in range(sectores):
            k2 = (k + 1) % sectores
            a0, a1 = i * sectores + k, i * sectores + k2
            b0, b1 = a0 + sectores, a1 + sectores
            caras.append((a0, b0, b1, a1))
            caras.append((m + a0, m + a1, m + b1, m + b0))
    for k in range(sectores):
        a0, a1 = anillos * sectores + k, anillos * sectores + (k + 1) % sectores
        caras.append((a0, m + a0, m + a1, a1))
    obj = crear_objeto("Diafragma", np.concatenate(capas), caras)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)   # une el centro
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.shade_smooth()
    desplegar(obj)
    return obj


# --- Materiales y texturas -------------------------------------------------------
def imagen(nombre, color=True, lado=LADO_TEXTURA):
    vieja = bpy.data.images.get(nombre)
    if vieja:
        bpy.data.images.remove(vieja)
    img = bpy.data.images.new(nombre, lado, lado)
    img.colorspace_settings.name = "sRGB" if color else "Non-Color"
    return img


def carpeta_texturas():
    return os.path.join(os.path.dirname(bpy.data.filepath), "texturas")


def guardar(img):
    os.makedirs(carpeta_texturas(), exist_ok=True)
    img.filepath_raw = os.path.join(carpeta_texturas(), img.name + ".png")
    img.file_format = "PNG"
    img.save()


def nuevo_material(nombre):
    viejo = bpy.data.materials.get(nombre)
    if viejo:
        bpy.data.materials.remove(viejo)
    mat = bpy.data.materials.new(nombre)
    if mat.node_tree is None:
        mat.use_nodes = True
    return mat


class Nodos:
    """Atajos para armar el material procedural que se hornea."""

    def __init__(self, nt):
        self.nt = nt
        self.pos = self.nuevo("NewGeometry").outputs["Position"]

    def nuevo(self, tipo):
        return self.nt.nodes.new("ShaderNode" + tipo)

    def poner(self, entrada, valor):
        if hasattr(valor, "node"):
            self.nt.links.new(valor, entrada)
        else:
            entrada.default_value = valor

    def ruido(self, escala, detalle=4.0, rug=0.55):
        n = self.nuevo("TexNoise")
        n.inputs["Scale"].default_value = escala
        n.inputs["Detail"].default_value = detalle
        n.inputs["Roughness"].default_value = rug
        self.poner(n.inputs["Vector"], self.pos)
        return n.outputs[0]

    def rampa(self, fuente, paradas):
        r = self.nuevo("ValToRGB")
        for el, (pos, col) in zip(r.color_ramp.elements, paradas):
            el.position, el.color = pos, (*col, 1)
        self.poner(r.inputs[0], fuente)
        return r.outputs[0]

    def mezcla(self, a, b, fac):
        m = self.nuevo("Mix")
        m.data_type = "RGBA"
        colores = [s for s in m.inputs if s.type == "RGBA"]
        self.poner([s for s in m.inputs if s.type == "VALUE"][0], fac)
        self.poner(colores[0], a)
        self.poner(colores[1], b)
        return [s for s in m.outputs if s.type == "RGBA"][0]

    def mate(self, op, a, b=0.0):
        m = self.nuevo("Math")
        m.operation = op
        self.poner(m.inputs[0], a)
        self.poner(m.inputs[1], b)
        return m.outputs[0]

    def celdas(self, escala, deforma):
        """Distancia al borde de celdas irregulares (lobulillos, vasos)."""
        n = self.nuevo("TexNoise")
        n.inputs["Scale"].default_value = escala * 1.3
        self.poner(n.inputs["Vector"], self.pos)
        esc = self.nuevo("VectorMath")
        esc.operation = "SCALE"
        esc.inputs["Scale"].default_value = deforma
        self.poner(esc.inputs[0], n.outputs[1])
        suma = self.nuevo("VectorMath")
        suma.operation = "ADD"
        self.poner(suma.inputs[0], self.pos)
        self.poner(suma.inputs[1], esc.outputs[0])
        v = self.nuevo("TexVoronoi")
        v.feature = "DISTANCE_TO_EDGE"
        v.inputs["Scale"].default_value = escala
        self.poner(v.inputs["Vector"], suma.outputs[0])
        return v.outputs[0]


NEGRO, BLANCO = (0, 0, 0), (1, 1, 1)


def aspecto_diafragma(k):
    sep = k.nuevo("SeparateXYZ")
    k.poner(sep.inputs[0], k.pos)
    x, y = k.mate("DIVIDE", sep.outputs[0], 0.129), k.mate("DIVIDE", sep.outputs[1], 0.077)
    ang = k.mate("ARCTAN2", y, x)
    ondas = k.mate("SINE", k.mate("ADD", k.mate("MULTIPLY", ang, 120.0), k.mate("MULTIPLY", k.ruido(40), 9.0)))
    fibras = k.mate("ADD", k.mate("MULTIPLY", ondas, 0.5), 0.5)
    rad = k.mate("SQRT", k.mate("ADD", k.mate("MULTIPLY", x, x), k.mate("MULTIPLY", y, y)))
    rad = k.mate("ADD", rad, k.mate("MULTIPLY", k.mate("SUBTRACT", k.ruido(25), 0.5), 0.25))
    musculo = k.rampa(fibras, [(0.0, (0.28, 0.035, 0.03)), (1.0, (0.55, 0.12, 0.09))])
    tendon = k.rampa(rad, [(0.40, BLANCO), (0.56, NEGRO)])
    return k.mezcla(musculo, (0.80, 0.58, 0.46, 1), tendon), fibras


def aspecto_lobulo(k, tono):
    rosa = k.rampa(k.ruido(16, 6), [(0.30, (0.50, 0.115 + tono, 0.115 + tono)),
                                    (0.70, (0.74, 0.27 + tono, 0.25 + tono))])
    lobulillos = k.celdas(95, 0.006)
    bordes = k.rampa(lobulillos, [(0.0, BLANCO), (0.10, NEGRO)])
    vasos = k.rampa(k.celdas(30, 0.045), [(0.0, BLANCO), (0.035, NEGRO)])
    motas = k.rampa(k.ruido(120, 5, 0.7), [(0.64, NEGRO), (0.74, BLANCO)])
    color = k.mezcla(rosa, (0.32, 0.06, 0.075, 1), k.mate("MULTIPLY", bordes, 0.42))
    color = k.mezcla(color, (0.38, 0.015, 0.03, 1), k.mate("MULTIPLY", vasos, 0.32))
    color = k.mezcla(color, (0.16, 0.06, 0.07, 1), k.mate("MULTIPLY", motas, 0.35))
    altura = k.mate("MULTIPLY", k.rampa(lobulillos, [(0.0, NEGRO), (0.2, BLANCO)]), 0.8)
    altura = k.mate("ADD", altura, k.mate("MULTIPLY", k.ruido(260, 3), 0.5))
    return color, k.mate("ADD", altura, k.mate("MULTIPLY", vasos, 0.15))


def hornear(obj, parte):
    """Hornea color (como emisión) y relieve (normal tangente) del material procedural."""
    esc = bpy.context.scene
    esc.render.engine = "CYCLES"
    esc.cycles.device = "CPU"
    esc.cycles.samples = 4
    esc.render.bake.margin = 8

    mat = nuevo_material("TMP_hornear")
    nt = mat.node_tree
    nt.nodes.clear()
    k = Nodos(nt)
    tono = {"Pulmon_Der_Lobulo_Med": 0.05, "Pulmon_Der_Lobulo_Inf": -0.04,
            "Pulmon_Izq_Lobulo_Inf": -0.04}.get(parte, 0.0)
    color_s, altura = aspecto_diafragma(k) if parte == "Diafragma" else aspecto_lobulo(k, tono)
    sal = k.nuevo("OutputMaterial")
    emi = k.nuevo("Emission")
    k.poner(emi.inputs["Color"], color_s)
    bump = k.nuevo("Bump")
    bump.inputs["Strength"].default_value = 0.55
    bump.inputs["Distance"].default_value = 0.0012
    k.poner(bump.inputs["Height"], altura)
    bsdf = k.nuevo("BsdfPrincipled")
    k.poner(bsdf.inputs["Normal"], bump.outputs[0])
    destino = k.nuevo("TexImage")
    nt.nodes.active = destino

    obj.data.materials.clear()
    obj.data.materials.append(mat)
    activar(obj)
    color = imagen(f"PUL_{parte}_BaseColor")
    destino.image = color
    nt.links.new(emi.outputs[0], sal.inputs["Surface"])
    bpy.ops.object.bake(type="EMIT")
    guardar(color)
    normal = imagen(f"PUL_{parte}_Normal", color=False)
    destino.image = normal
    nt.links.new(bsdf.outputs[0], sal.inputs["Surface"])
    bpy.ops.object.bake(type="NORMAL")
    guardar(normal)
    return color, normal


def textura_anillos():
    """Cartílago (v entero) y membrana (v = 0,5), repetible en v. La cara posterior es solo membrana."""
    lado = 256
    eje = (np.arange(lado) + 0.5) / lado
    v, u = np.meshgrid(eje, eje, indexing="ij")
    cart = suave(0.5 + 0.5 * np.cos(2 * math.pi * v), 0.30, 0.62)
    cart = cart * (1 - suave(np.cos(2 * math.pi * u), 0.45, 0.75))
    membrana, cartilago = np.array([0.80, 0.50, 0.46]), np.array([0.93, 0.88, 0.80])
    rgb = membrana + (cartilago - membrana) * cart[..., None]
    rgb = rgb + RNG.normal(0, 0.012, rgb.shape)
    px = np.ones((lado, lado, 4), np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    img = imagen("PUL_Vias_BaseColor", lado=lado)
    img.pixels.foreach_set(px.ravel())
    img.update()
    guardar(img)
    return img


def material_final(obj, parte, color, normal=None, rugosidad=0.42):
    mat = nuevo_material(f"MAT_PUL_{parte}")
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rugosidad
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = color
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    if normal:
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = normal
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# --- Respiración -----------------------------------------------------------------
def coords(obj):
    co = np.empty(len(obj.data.vertices) * 3, np.float32)
    obj.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def origen_al_centro(obj):
    co = coords(obj)
    c = (co.min(0) + co.max(0)) / 2
    obj.data.vertices.foreach_set("co", (co - c).ravel())
    obj.location = c.tolist()
    obj.data.update()


def clave_inhalar(obj, parte):
    co = coords(obj)
    mundo = (co + np.array(obj.location, np.float32)) / CM
    d = inhalar(mundo)
    if parte == "Diafragma":       # el borde está sujeto a las costillas
        r = np.sqrt((mundo[:, 0] / 12.9) ** 2 + ((mundo[:, 1] - 0.35) / 7.7) ** 2)
        d *= (1 - 0.55 * suave(r, 0.7, 1.0))[:, None]
    obj.shape_key_add(name="Basis")
    k = obj.shape_key_add(name="Inhalar", from_mix=False)
    k.data.foreach_set("co", (co + d * CM).astype(np.float32).ravel())
    # Ciclo de 4 s a 24 fps para verlo en Blender (en Unity lo mueve un script).
    for cuadro, valor in ((1, 0.0), (40, 1.0), (97, 0.0)):
        k.value = valor
        k.keyframe_insert("value", frame=cuadro)
    k.value = 0.0


def volumen(obj, clave=None):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if clave:
        puntos = obj.data.shape_keys.key_blocks[clave].data
        for v in bm.verts:
            v.co = puntos[v.index].co
    total = abs(bm.calc_volume()) * 1e6
    bm.free()
    return total


def exportar_flujo(rutas):
    """Recorridos del aire (tráquea -> puntas) en ejes de Unity: (x, y, z) -> (-x, z, -y), en metros."""
    def a_unity(m):
        return [round(float(x), 5) for p in m for x in (-p[0], p[2], -p[1])]

    salida = []
    for r in rutas:
        largo = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(r, axis=0), axis=1))])
        s = np.linspace(0, largo[-1], 40)
        q = np.stack([np.interp(s, largo, r[:, i]) for i in range(3)], 1)
        salida.append({"reposo": a_unity(q * CM), "inhalado": a_unity((q + inhalar(q)) * CM)})
    modelado = os.path.dirname(os.path.dirname(bpy.data.filepath))
    ruta = os.path.join(modelado, "Exportados", "pulmones_flujo.json")
    with open(ruta, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"rutas": salida}, f, separators=(",", ":"))
    unity = os.path.join(os.path.dirname(modelado), "AtlasAnatomic", "AtlasAnatomic",
                         "Assets", "Atlas", "Modelos", "Pulmones")
    if os.path.isdir(os.path.dirname(unity)):
        os.makedirs(unity, exist_ok=True)
        shutil.copy2(ruta, unity)
        for n in os.listdir(carpeta_texturas()):
            if n.endswith(".png"):
                shutil.copy2(os.path.join(carpeta_texturas(), n), unity)


# --- Principal -------------------------------------------------------------------
def main():
    forzar = "--" in sys.argv and "--forzar" in sys.argv[sys.argv.index("--") + 1:]
    existentes = [o for o in bpy.data.objects if o.name.startswith("PUL_") and "_REF_" not in o.name]
    if existentes and not forzar:
        raise SystemExit(f"Ya hay {len(existentes)} piezas PUL_*. Usa '-- --forzar' para regenerarlas.")
    for o in existentes:
        datos = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if datos and datos.users == 0:
            bpy.data.meshes.remove(datos)
    for parte in PARTES:
        if f"PUL_{parte}" not in bpy.data.collections:
            raise SystemExit(f"Falta la colección PUL_{parte}: corre antes configurar_escena.py -- pulmones")

    esc = bpy.context.scene
    motor = esc.render.engine
    esc.render.fps, esc.frame_start, esc.frame_end = 24, 1, 96
    esc.frame_set(1)

    tr, br, rutas = vias_aereas()
    piezas = {
        "Traquea": crear_objeto("Traquea", tr.v, tr.c, tr.uv),
        "Bronquios": crear_objeto("Bronquios", br.v, br.c, br.uv),
    }
    for nombre in LOBULOS:
        piezas[nombre] = malla_lobulo(nombre)
    piezas["Diafragma"] = diafragma()

    anillos = textura_anillos()
    for parte, obj in piezas.items():
        if parte in ("Traquea", "Bronquios"):
            material_final(obj, parte, anillos, rugosidad=0.38)
        else:
            color, normal = hornear(obj, parte)
            material_final(obj, parte, color, normal)
    tmp = bpy.data.materials.get("TMP_hornear")
    if tmp:
        bpy.data.materials.remove(tmp)

    for parte, obj in piezas.items():
        origen_al_centro(obj)
        if parte != "Traquea":
            clave_inhalar(obj, parte)
    esc.frame_set(1)
    exportar_flujo(rutas)
    esc.render.engine = motor

    total = 0
    for parte, obj in piezas.items():
        n = triangulos(obj)
        total += n
        me = obj.data
        tuneles = 1 - (len(me.vertices) - len(me.edges) + len(me.polygons)) // 2
        if parte in LOBULOS and tuneles:
            print(f"[AVISO] {parte}: {tuneles} túneles; revisar los recortes")
        print(f"[pulmones] {parte}: {n} triángulos, {[round(v * 100, 1) for v in obj.dimensions]} cm")
    v0 = sum(volumen(piezas[n]) for n in LOBULOS)
    v1 = sum(volumen(piezas[n], "Inhalar") for n in LOBULOS)
    print(f"[pulmones] total {total} triángulos, {len(rutas)} recorridos de aire")
    print(f"[pulmones] volumen {v0:.0f} -> {v1:.0f} cm3 ({(v1 / v0 - 1) * 100:+.1f} %)")
    bpy.ops.wm.save_mainfile()


main()
