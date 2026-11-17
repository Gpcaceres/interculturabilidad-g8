"""Renderiza imágenes de vista previa del ojo para las tres vistas del diseño.

Uso (desde la raíz del repo):
    blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/ojo_previews.py

Genera en Modelado/ojo/previews/:
    01-completa.png     globo entero (vista 1)
    02-desglosada.png   capas separadas a lo largo del eje óptico (vista 2)
    03-corte.png        corte de un cuarto para ver las túnicas (vista 3)

No guarda cambios en el .blend: cámara, luces y cortes son temporales.
"""
import math
import os

import bpy
from mathutils import Vector

MM = 0.001
ORDEN = ["Cornea", "Iris", "Cristalino", "Cuerpo_Ciliar", "Humor_Vitreo",
         "Retina", "Coroides", "Esclerotica", "Nervio_Optico"]
# Desplazamiento (en "pasos") de cada pieza sobre el eje óptico en la vista desglosada.
PASOS = {"Cornea": -4, "Iris": -3, "Cristalino": -2, "Cuerpo_Ciliar": -1, "Humor_Vitreo": 0,
         "Retina": 1.5, "Coroides": 3.0, "Esclerotica": 4.5, "Nervio_Optico": 4.5}
SEPARACION = 14.0  # mm por paso

escena = bpy.context.scene
salida = os.path.join(os.path.dirname(bpy.data.filepath), "previews")
os.makedirs(salida, exist_ok=True)

piezas = {p: bpy.data.objects[f"OJO_{p}"] for p in ORDEN}
posiciones = {p: o.location.copy() for p, o in piezas.items()}
for o in bpy.data.objects:
    if "_REF_" in o.name:
        o.hide_render = True

# --- Render -------------------------------------------------------------------
for motor in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        escena.render.engine = motor
        break
    except TypeError:
        pass
if hasattr(escena, "eevee"):
    escena.eevee.taa_render_samples = 64
escena.render.resolution_x = 1600
escena.render.resolution_y = 1000
escena.render.film_transparent = False
escena.view_settings.view_transform = "Standard"  # colores fieles, como en Unity

mundo = bpy.data.worlds.new("Preview") if not escena.world else escena.world
escena.world = mundo
fondo = mundo.node_tree.nodes.get("Background")
fondo.inputs["Color"].default_value = (0.012, 0.018, 0.035, 1)
fondo.inputs["Strength"].default_value = 1.0


def luz(nombre, tipo, energia, pos, tam=0.1, color=(1, 1, 1)):
    datos = bpy.data.lights.new(nombre, tipo)
    datos.energy = energia
    datos.color = color
    if tipo == "AREA":
        datos.size = tam
    obj = bpy.data.objects.new(nombre, datos)
    escena.collection.objects.link(obj)
    obj.location = pos
    mirar(obj, Vector((0, 0, 0)))
    return obj


def mirar(obj, objetivo):
    direccion = objetivo - obj.location
    obj.rotation_euler = direccion.to_track_quat("-Z", "Y").to_euler()


cam_datos = bpy.data.cameras.new("Cam")
cam_datos.lens = 50
cam_datos.clip_start = 0.001
cam = bpy.data.objects.new("Cam", cam_datos)
escena.collection.objects.link(cam)
escena.camera = cam

luz("Clave", "AREA", 1.6, Vector((0.12, -0.15, 0.12)), 0.15)
luz("Relleno", "AREA", 0.6, Vector((-0.15, -0.10, 0.02)), 0.2, (0.8, 0.88, 1.0))
luz("Contra", "AREA", 1.5, Vector((0.0, 0.18, 0.10)), 0.1, (1.0, 0.9, 0.8))


def render(nombre, pos_cam, objetivo=(0, 0, 0)):
    cam.location = Vector(pos_cam)
    mirar(cam, Vector(objetivo))
    escena.render.filepath = os.path.join(salida, nombre)
    bpy.ops.render.render(write_still=True)
    print(f"[ojo_previews] {nombre}")


# 1. Completa: tres cuartos desde delante, como se ve el globo.
render("01-completa.png", (0.030, -0.065, 0.022))

# 2. Desglosada: piezas separadas sobre el eje óptico, vista lateral desde delante.
for p, o in piezas.items():
    o.location = posiciones[p] + Vector((0, PASOS[p] * SEPARACION * MM, 0))
cam_datos.lens = 35
render("02-desglosada.png", (0.190, -0.090, 0.055), (0, 0.004, 0))
for p, o in piezas.items():
    o.location = posiciones[p]

# 3. Corte: se quita el cuarto superior derecho (x > 0, z > 0) de todas las piezas.
cam_datos.lens = 50
caja_datos = bpy.data.meshes.new("Corte")
caja = bpy.data.objects.new("Corte", caja_datos)
escena.collection.objects.link(caja)
import bmesh  # noqa: E402
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bm.to_mesh(caja_datos)
bm.free()
caja.scale = (0.03, 0.08, 0.03)
caja.location = (0.015, 0.0, 0.015)
caja.hide_render = True
for o in piezas.values():
    m = o.modifiers.new("Corte", "BOOLEAN")
    m.operation = "DIFFERENCE"
    m.solver = "EXACT"
    m.object = caja
render("03-corte.png", (0.055, -0.045, 0.040), (0, 0.0, 0))
