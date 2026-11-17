"""Genera la geometría base (blockout) de las 9 piezas del ojo en ojo.blend.

Es un punto de partida con medidas anatómicas aproximadas de un adulto, para
refinar después a mano (esculpido, texturas, detalle).

Uso (desde la raíz del repo):
    blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/ojo_modelo_base.py
    blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/ojo_modelo_base.py -- --forzar

Sin --forzar se niega a correr si ya hay piezas OJO_* (para no pisar trabajo
hecho a mano). Con --forzar las borra y las vuelve a crear.

Convenciones (ver Modelado/ojo/README.md):
  - Eje óptico en -Y: la córnea mira hacia -Y (vista Front, Numpad 1).
  - Centro del globo en el origen. Medidas en mm aquí, guardadas en metros.
  - Cada pieza tiene su origen en su propio centro.
  - Los grosores de las capas son modificadores Solidify vivos: se pueden
    ajustar en el panel de modificadores.
"""
import math
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

MM = 0.001

# --- Medidas (mm) -----------------------------------------------------------
R_ESCLERA = 12.0          # radio externo del globo (diámetro ~24 mm)
G_ESCLERA = 0.6
G_COROIDES = 0.25
G_RETINA = 0.25
R_COROIDES = R_ESCLERA - G_ESCLERA          # 11.4
R_RETINA = R_COROIDES - G_COROIDES          # 11.15
R_VITREO = R_RETINA - G_RETINA - 0.05       # 10.85

R_CORNEA_CURV = 7.8       # radio de curvatura de la córnea
R_CORNEA_BASE = 5.75      # mitad del diámetro corneal (~11.5 mm)
G_CORNEA = 0.55
# Unión córnea-esclerótica (limbo)
Y_LIMBO = -math.sqrt(R_ESCLERA**2 - R_CORNEA_BASE**2)                     # ≈ -10.53
Y_CENTRO_CORNEA = Y_LIMBO + math.sqrt(R_CORNEA_CURV**2 - R_CORNEA_BASE**2)  # ≈ -5.27

R_PUPILA = 1.75
R_IRIS = 6.0
Y_IRIS = -9.6
G_IRIS = 0.4

R_CRISTALINO = 4.75       # diámetro ~9.5 mm
Y_CRISTALINO = -7.2
G_CRIST_DELANTE = 1.7     # grosor total ~4 mm, más curvo por detrás
G_CRIST_DETRAS = 2.3

Y_CILIAR = -7.4
R_CILIAR = 6.9
r_CILIAR = 1.1
N_PROCESOS_CILIARES = 70

Y_CORTE_RETINA = -6.3     # ora serrata (aprox.)
Y_CORTE_COROIDES = -7.0
Y_CORTE_VITREO = -6.0

R_NERVIO = 1.5

# parte: (color base lineal, rugosidad, alpha). alpha < 1 = pieza transparente.
COLORES = {
    "Esclerotica": ((0.80, 0.72, 0.66, 1), 0.40, 1.0),
    "Cornea": ((0.90, 0.95, 1.00, 1), 0.02, 0.12),
    "Iris": ((0.08, 0.20, 0.42, 1), 0.55, 1.0),
    "Cristalino": ((0.75, 0.85, 1.00, 1), 0.04, 0.35),
    "Cuerpo_Ciliar": ((0.30, 0.05, 0.03, 1), 0.60, 1.0),
    "Humor_Vitreo": ((0.70, 0.85, 0.95, 1), 0.08, 0.18),
    "Retina": ((0.90, 0.30, 0.04, 1), 0.50, 1.0),
    "Coroides": ((0.18, 0.02, 0.02, 1), 0.55, 1.0),
    "Nervio_Optico": ((0.85, 0.65, 0.30, 1), 0.60, 1.0),
}


# --- Utilidades --------------------------------------------------------------
def material(parte):
    nombre = f"MAT_OJO_{parte}"
    mat = bpy.data.materials.get(nombre) or bpy.data.materials.new(nombre)
    color, rugosidad, alpha = COLORES[parte]
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = rugosidad
    bsdf.inputs["Transmission Weight"].default_value = 0.0
    bsdf.inputs["Alpha"].default_value = 1.0
    mat.diffuse_color = color
    if alpha < 1.0:
        # Transparencia por alpha (no por transmisión): es lo que Unity/URP
        # reproduce en el Quest sin raytracing.
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["IOR"].default_value = 1.38
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "BLENDED"
        if hasattr(mat, "use_transparency_overlap"):
            mat.use_transparency_overlap = False
    return mat


def esfera_bm(radio, segmentos=64, anillos=32):
    """UV sphere con los polos sobre el eje Y (polo frontal en -Y)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segmentos, v_segments=anillos, radius=radio)
    bmesh.ops.rotate(bm, verts=bm.verts, matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    return bm


def cortar(bm, y, quitar_delante=True):
    """Corta con un plano perpendicular a Y y quita la parte de delante (y < corte) o de detrás."""
    normal = (0, -1, 0) if quitar_delante else (0, 1, 0)
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, y, 0), plane_no=normal, clear_outer=True)


def rellenar_huecos(bm):
    bordes = [e for e in bm.edges if e.is_boundary]
    if bordes:
        bmesh.ops.holes_fill(bm, edges=bordes, sides=0)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])


def crear_objeto(parte, bm):
    nombre = f"OJO_{parte}"
    me = bpy.data.meshes.new(nombre)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.transform(Matrix.Scale(MM, 4))
    obj = bpy.data.objects.new(nombre, me)
    bpy.data.collections[nombre].objects.link(obj)
    me.materials.append(material(parte))
    me.shade_smooth()
    return obj


def origen_al_centro(obj):
    me = obj.data
    if not me.vertices:
        return
    minimo = Vector([min(v.co[i] for v in me.vertices) for i in range(3)])
    maximo = Vector([max(v.co[i] for v in me.vertices) for i in range(3)])
    centro = (minimo + maximo) / 2
    me.transform(Matrix.Translation(-centro))
    obj.location = centro


def solidify(obj, grosor_mm):
    m = obj.modifiers.new("Grosor", "SOLIDIFY")
    m.thickness = grosor_mm * MM
    m.offset = -1.0          # hacia dentro: el radio externo se conserva
    m.use_even_offset = True
    m.use_quality_normals = True
    return m


# --- Piezas ------------------------------------------------------------------
def esclerotica():
    bm = esfera_bm(R_ESCLERA, 56, 28)
    cortar(bm, Y_LIMBO)
    obj = crear_objeto("Esclerotica", bm)
    solidify(obj, G_ESCLERA)
    return obj


def cornea():
    bm = esfera_bm(R_CORNEA_CURV, 64, 48)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, Y_CENTRO_CORNEA, 0))
    cortar(bm, Y_LIMBO, quitar_delante=False)
    obj = crear_objeto("Cornea", bm)
    solidify(obj, G_CORNEA)
    return obj


def iris():
    bm = bmesh.new()
    n = 96
    anillos = [(R_PUPILA, Y_IRIS - 0.25), (R_PUPILA + 1.2, Y_IRIS - 0.3),
               (R_IRIS - 1.0, Y_IRIS + 0.1), (R_IRIS, Y_IRIS + 0.35)]
    filas = []
    for r, y in anillos:
        filas.append([bm.verts.new((r * math.cos(2 * math.pi * i / n), y,
                                    r * math.sin(2 * math.pi * i / n))) for i in range(n)])
    for a, b in zip(filas, filas[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # que la cara frontal mire hacia -Y
    bm.faces.ensure_lookup_table()
    bm.normal_update()
    if bm.faces[0].normal.y > 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    obj = crear_objeto("Iris", bm)
    solidify(obj, G_IRIS)
    return obj


def cristalino():
    bm = esfera_bm(1.0, 48, 24)
    for v in bm.verts:
        x, y, z = v.co
        v.co = (x * R_CRISTALINO, y * (G_CRIST_DELANTE if y < 0 else G_CRIST_DETRAS), z * R_CRISTALINO)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, Y_CRISTALINO, 0))
    return crear_objeto("Cristalino", bm)


def cuerpo_ciliar():
    """Anillo alrededor del cristalino con ~70 procesos (pliegues) hacia dentro."""
    bm = bmesh.new()
    mayor, menor = N_PROCESOS_CILIARES * 3, 12
    filas = []
    for i in range(mayor):
        a = 2 * math.pi * i / mayor
        pliegue = 0.5 + 0.5 * math.cos(N_PROCESOS_CILIARES * a)
        fila = []
        for j in range(menor):
            b = 2 * math.pi * j / menor
            hacia_dentro = max(0.0, -math.cos(b))
            r = R_CILIAR + r_CILIAR * math.cos(b) - 0.8 * pliegue * hacia_dentro
            y = Y_CILIAR + 1.8 * r_CILIAR * math.sin(b)
            fila.append(bm.verts.new((r * math.cos(a), y, r * math.sin(a))))
        filas.append(fila)
    for i in range(mayor):
        a, b = filas[i], filas[(i + 1) % mayor]
        for j in range(menor):
            k = (j + 1) % menor
            bm.faces.new((a[j], b[j], b[k], a[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return crear_objeto("Cuerpo_Ciliar", bm)


def capa_posterior(parte, radio, y_corte, grosor):
    bm = esfera_bm(radio, 48, 24)
    cortar(bm, y_corte)
    obj = crear_objeto(parte, bm)
    solidify(obj, grosor)
    return obj


def humor_vitreo(obj_cristalino):
    bm = esfera_bm(R_VITREO, 48, 24)
    cortar(bm, Y_CORTE_VITREO)
    rellenar_huecos(bm)
    obj = crear_objeto("Humor_Vitreo", bm)
    # Fosa hialoidea: el hueco donde se apoya la parte trasera del cristalino.
    molde = obj_cristalino.copy()
    molde.data = obj_cristalino.data.copy()
    molde.scale = (1.04, 1.04, 1.04)
    bpy.context.scene.collection.objects.link(molde)
    m = obj.modifiers.new("Fosa", "BOOLEAN")
    m.operation = "DIFFERENCE"
    m.solver = "EXACT"
    m.object = molde
    dg = bpy.context.evaluated_depsgraph_get()
    nueva = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    viejo = obj.data
    obj.modifiers.remove(m)
    obj.data = nueva
    nueva.name = "OJO_Humor_Vitreo"
    bpy.data.meshes.remove(viejo)
    malla_molde = molde.data
    bpy.data.objects.remove(molde, do_unlink=True)
    bpy.data.meshes.remove(malla_molde)
    nueva.shade_smooth()
    return obj


def nervio_optico():
    """Sale por detrás, unos 3 mm hacia el lado nasal (-X), con una curva suave."""
    cu = bpy.data.curves.new("OJO_Nervio_Optico_curva", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = R_NERVIO * MM
    cu.bevel_resolution = 6
    cu.resolution_u = 12
    cu.use_fill_caps = True
    sp = cu.splines.new("BEZIER")
    pts = [(-3.0, 10.9, 0.0), (-4.2, 17.0, 0.0), (-7.5, 23.0, 0.0)]
    sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = Vector(p) * MM
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    tmp = bpy.data.objects.new("tmp_nervio", cu)
    bpy.context.scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp, do_unlink=True)
    bpy.data.curves.remove(cu)
    me.name = "OJO_Nervio_Optico"
    obj = bpy.data.objects.new("OJO_Nervio_Optico", me)
    bpy.data.collections["OJO_Nervio_Optico"].objects.link(obj)
    me.materials.append(material("Nervio_Optico"))
    me.shade_smooth()
    return obj


# --- Principal ---------------------------------------------------------------
def main():
    forzar = "--forzar" in sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else False
    existentes = [o for o in bpy.data.objects if o.name.startswith("OJO_") and "_REF_" not in o.name]
    if existentes and not forzar:
        raise SystemExit(f"Ya hay {len(existentes)} piezas OJO_*. Usa '-- --forzar' para regenerarlas.")
    for o in existentes:
        datos = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if datos and datos.users == 0:
            bpy.data.meshes.remove(datos)

    for parte in ("Cornea", "Iris", "Cristalino", "Cuerpo_Ciliar", "Humor_Vitreo",
                  "Retina", "Coroides", "Esclerotica", "Nervio_Optico"):
        if f"OJO_{parte}" not in bpy.data.collections:
            raise SystemExit("Falta la colección OJO_" + parte + ": corre antes configurar_escena.py -- ojo")

    obj_cristalino = cristalino()
    piezas = [
        cornea(),
        iris(),
        obj_cristalino,
        cuerpo_ciliar(),
        humor_vitreo(obj_cristalino),
        capa_posterior("Retina", R_RETINA, Y_CORTE_RETINA, G_RETINA),
        capa_posterior("Coroides", R_COROIDES, Y_CORTE_COROIDES, G_COROIDES),
        esclerotica(),
        nervio_optico(),
    ]
    for p in piezas:
        origen_al_centro(p)

    bpy.ops.wm.save_mainfile()
    print(f"[ojo_modelo_base] {len(piezas)} piezas creadas en {bpy.data.filepath}")


main()
