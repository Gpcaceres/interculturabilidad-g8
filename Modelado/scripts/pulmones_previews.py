"""Renderiza las vistas previas de los pulmones en Modelado/pulmones/previews/.

Uso (desde la raíz del repo):
    blender -b Modelado/pulmones/pulmones.blend --python Modelado/scripts/pulmones_previews.py

    01-completa.png     vista anterior
    02-desglosada.png   piezas separadas desde el centro
    03-arbol.png        lóbulos translúcidos: se ve el árbol bronquial
    04-inhalacion.png   shape key "Inhalar" al máximo
    05-medial.png       cara interna del pulmón derecho: hilio y cisuras

No guarda cambios en el .blend: cámara, luces y materiales son temporales.
"""
import os

import bpy
from mathutils import Vector

esc = bpy.context.scene
salida = os.path.join(os.path.dirname(bpy.data.filepath), "previews")
os.makedirs(salida, exist_ok=True)
piezas = {o.name[4:]: o for o in bpy.data.objects if o.name.startswith("PUL_") and "_REF_" not in o.name}
posiciones = {n: o.location.copy() for n, o in piezas.items()}
# Desglose radial en cm; la versión de Unity está en DatosPulmones.cs.
DESGLOSE = {
    "Traquea": (0, 0, 9), "Bronquios": (0, 0, 0),
    "Pulmon_Der_Lobulo_Sup": (-12, 0, 8), "Pulmon_Der_Lobulo_Med": (-18, -3, -1),
    "Pulmon_Der_Lobulo_Inf": (-9, 4, -13),
    "Pulmon_Izq_Lobulo_Sup": (13, 0, 7), "Pulmon_Izq_Lobulo_Inf": (10, 4, -13),
    "Diafragma": (0, 0, -19),
}
for o in bpy.data.objects:
    if "_REF_" in o.name:
        o.hide_render = True

esc.render.engine = "BLENDER_EEVEE"
esc.eevee.taa_render_samples = 64
esc.render.resolution_x, esc.render.resolution_y = 1600, 1200
esc.view_settings.view_transform = "Standard"
esc.world = esc.world or bpy.data.worlds.new("Preview")
fondo = esc.world.node_tree.nodes.get("Background")
fondo.inputs["Color"].default_value = (0.02, 0.028, 0.05, 1)
fondo.inputs["Strength"].default_value = 1.0


def mirar(obj, objetivo):
    obj.rotation_euler = (Vector(objetivo) - obj.location).to_track_quat("-Z", "Y").to_euler()


def luz(energia, p, tam, color=(1, 1, 1)):
    d = bpy.data.lights.new("L", "AREA")
    d.energy, d.size, d.color = energia, tam, color
    o = bpy.data.objects.new("L", d)
    esc.collection.objects.link(o)
    o.location = p
    mirar(o, (0, 0, 0))


luz(22, (-0.6, -0.9, 0.7), 0.5)
luz(9, (0.9, -0.6, 0.1), 0.8, (0.8, 0.88, 1))
luz(18, (0.2, 0.9, 0.6), 0.5, (1, 0.88, 0.8))
cam_datos = bpy.data.cameras.new("C")
cam_datos.lens, cam_datos.clip_start = 60, 0.01
cam = bpy.data.objects.new("C", cam_datos)
esc.collection.objects.link(cam)
esc.camera = cam


def render(nombre, p, objetivo=(0, 0, 0.015)):
    cam.location = p
    mirar(cam, objetivo)
    esc.render.filepath = os.path.join(salida, nombre)
    bpy.ops.render.render(write_still=True)
    print(f"[pulmones_previews] {nombre}")


def inhalar(v):
    for o in piezas.values():
        if o.data.shape_keys:
            o.data.shape_keys.animation_data_clear()
            o.data.shape_keys.key_blocks["Inhalar"].value = v


inhalar(0)
render("01-completa.png", (0.10, -0.85, 0.10))

for n, o in piezas.items():
    o.location = posiciones[n] + Vector(DESGLOSE[n]) * 0.01
render("02-desglosada.png", (0.15, -1.75, 0.15), (0, 0, -0.02))
for n, o in piezas.items():
    o.location = posiciones[n]

inhalar(1)
render("04-inhalacion.png", (0.10, -0.85, 0.10))
inhalar(0)

for n, o in piezas.items():
    o.hide_render = not n.startswith("Pulmon_Der")
render("05-medial.png", (0.62, -0.25, 0.02), (-0.06, 0, 0))
for o in piezas.values():
    o.hide_render = False

for n, o in piezas.items():
    if n.startswith("Pulmon"):
        m = o.material_slots[0].material
        m.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.16
        m.surface_render_method = "BLENDED"
render("03-arbol.png", (0.10, -0.85, 0.10))
