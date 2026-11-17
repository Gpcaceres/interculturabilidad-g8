"""Prepara un .blend de órgano con la convención del proyecto.

Uso (desde la raíz del repo):
    blender -b Modelado/<organo>/<organo>.blend --python Modelado/scripts/configurar_escena.py -- <organo>
    blender -b --python Modelado/scripts/configurar_escena.py -- <organo>    (crea el .blend si no existe)

Órganos: ojo, pulmones, rinon

Qué hace:
  - Unidades métricas, escala 1.0 (1 m en Blender = 1 unidad en Unity), medida en centímetros.
  - Quita el cubo, la cámara y la luz por defecto (Unity pone las suyas).
  - Crea una colección por parte anatómica, con nombres <ORGANO>_<Parte>.
  - Crea la colección _Referencias (no se exporta) con un volumen de escala real en alambre.
  - Ajusta el clip de la vista 3D para trabajar piezas pequeñas.
Es idempotente: no borra colecciones ni objetos que ya existan.
"""
import os
import sys

import bpy

# Partes por órgano. Cada parte será una colección y luego un objeto separado
# (necesario para la vista explosionada, la animación y el corte en Unity).
ORGANOS = {
    "ojo": {
        "prefijo": "OJO",
        # Orden de la vista explosionada, de delante hacia atrás (ver ojo/README.md).
        "partes": [
            "Cornea", "Iris", "Cristalino", "Cuerpo_Ciliar", "Humor_Vitreo",
            "Retina", "Coroides", "Esclerotica", "Nervio_Optico",
        ],
        # Globo ocular adulto: ~24 mm de diámetro.
        "referencia": ("esfera", (0.024, 0.024, 0.024)),
    },
    "pulmones": {
        "prefijo": "PUL",
        "partes": [
            "Traquea", "Bronquios",
            "Pulmon_Der_Lobulo_Sup", "Pulmon_Der_Lobulo_Med", "Pulmon_Der_Lobulo_Inf",
            "Pulmon_Izq_Lobulo_Sup", "Pulmon_Izq_Lobulo_Inf",
            "Diafragma",
        ],
        # Conjunto pulmonar: ~26 cm ancho x 15 cm fondo x 25 cm alto.
        "referencia": ("caja", (0.26, 0.15, 0.25)),
    },
    "rinon": {
        "prefijo": "RIN",
        "partes": [
            "Capsula_Renal", "Corteza", "Medula_Piramides", "Pelvis_Renal",
            "Ureter", "Arteria_Renal", "Vena_Renal",
        ],
        # Riñón adulto: ~11 x 6 x 3 cm.
        "referencia": ("esfera", (0.06, 0.03, 0.11)),
    },
}

COLECCION_REF = "_Referencias"


def organo_desde_args():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args or args[0] not in ORGANOS:
        raise SystemExit(f"Indica el órgano: {', '.join(ORGANOS)}")
    return args[0]


def obtener_coleccion(nombre, padre):
    col = bpy.data.collections.get(nombre)
    if col is None:
        col = bpy.data.collections.new(nombre)
    if col.name not in padre.children:
        padre.children.link(col)
    return col


def limpiar_por_defecto():
    for nombre in ("Cube", "Camera", "Light"):
        obj = bpy.data.objects.get(nombre)
        if obj is not None:
            bpy.data.objects.remove(obj, do_unlink=True)
    vieja = bpy.data.collections.get("Collection")
    if vieja is not None and not vieja.objects and not vieja.children:
        bpy.data.collections.remove(vieja)


def crear_referencia(cfg, col_ref):
    nombre = f"{cfg['prefijo']}_REF_Escala"
    if bpy.data.objects.get(nombre):
        return
    forma, (x, y, z) = cfg["referencia"]
    if forma == "esfera":
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, segments=32, ring_count=16)
    else:
        bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.active_object
    obj.name = nombre
    obj.dimensions = (x, y, z)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.display_type = "WIRE"
    obj.hide_select = True
    obj.hide_render = True
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col_ref.objects.link(obj)


def ajustar_vistas():
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                for space in area.spaces:
                    if space.type == "VIEW_3D":
                        space.clip_start = 0.0005
                        space.clip_end = 100.0
                        space.overlay.grid_scale = 0.01  # cuadrícula de 1 cm


def main():
    organo = organo_desde_args()
    cfg = ORGANOS[organo]
    escena = bpy.context.scene

    us = escena.unit_settings
    us.system = "METRIC"
    us.scale_length = 1.0
    us.length_unit = "CENTIMETERS"

    limpiar_por_defecto()

    raiz = escena.collection
    col_organo = obtener_coleccion(cfg["prefijo"], raiz)
    for parte in cfg["partes"]:
        obtener_coleccion(f"{cfg['prefijo']}_{parte}", col_organo)

    col_ref = obtener_coleccion(COLECCION_REF, raiz)
    crear_referencia(cfg, col_ref)

    ajustar_vistas()

    destino = bpy.data.filepath
    if not destino:
        repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        destino = os.path.join(repo, organo, f"{organo}.blend")
    bpy.ops.wm.save_as_mainfile(filepath=destino)
    print(f"[configurar_escena] {organo} listo en {destino}")


main()
