"""Exporta el órgano abierto a FBX con los ajustes que Unity necesita.

Uso:
  - Dentro de Blender: pestaña Scripting -> abrir este archivo -> Run Script.
  - Por consola (desde la raíz del repo):
      blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/exportar_fbx.py

Salida: Modelado/Exportados/<organo>.fbx (el nombre sale del .blend).

Reglas:
  - Exporta solo mallas y armatures; nunca lo que esté en la colección _Referencias.
  - Hornea animación solo si hay shape keys animadas o acciones (caso pulmones).
  - Avisa si algún objeto tiene escala/rotación sin aplicar o si el total de
    triángulos supera el presupuesto para Meta Quest.
"""
import os

import bpy

PRESUPUESTO_TRIANGULOS = 30_000  # por órgano; ~100k para toda la escena en Quest
COLECCION_REF = "_Referencias"


def en_referencias(obj):
    return any(c.name == COLECCION_REF for c in obj.users_collection)


def tiene_animacion(objs):
    for o in objs:
        if o.animation_data and o.animation_data.action:
            return True
        sk = getattr(o.data, "shape_keys", None)
        if sk and sk.animation_data and sk.animation_data.action:
            return True
    return False


def contar_triangulos(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    total = 0
    for o in objs:
        if o.type != "MESH":
            continue
        malla = o.evaluated_get(dg).to_mesh()
        malla.calc_loop_triangles()
        total += len(malla.loop_triangles)
        o.evaluated_get(dg).to_mesh_clear()
    return total


def main():
    if not bpy.data.filepath:
        raise SystemExit("Guarda el .blend antes de exportar.")

    nombre = os.path.splitext(os.path.basename(bpy.data.filepath))[0]
    carpeta_modelado = os.path.dirname(os.path.dirname(bpy.data.filepath))
    salida = os.path.join(carpeta_modelado, "Exportados", f"{nombre}.fbx")
    os.makedirs(os.path.dirname(salida), exist_ok=True)

    objs = [o for o in bpy.context.scene.objects
            if o.type in {"MESH", "ARMATURE"} and not en_referencias(o)]
    if not objs:
        raise SystemExit("No hay mallas para exportar.")

    for o in objs:
        if any(abs(s - 1.0) > 1e-4 for s in o.scale) or any(abs(r) > 1e-4 for r in o.rotation_euler):
            print(f"[AVISO] {o.name}: escala/rotación sin aplicar (Ctrl+A > All Transforms)")

    tris = contar_triangulos(objs)
    estado = "OK" if tris <= PRESUPUESTO_TRIANGULOS else "EXCEDE el presupuesto, usa Decimate"
    print(f"[exportar_fbx] {tris} triángulos ({estado}, límite {PRESUPUESTO_TRIANGULOS})")

    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)

    animar = tiene_animacion(objs)
    bpy.ops.export_scene.fbx(
        filepath=salida,
        use_selection=True,
        object_types={"MESH", "ARMATURE"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        add_leaf_bones=False,
        bake_anim=animar,
        bake_anim_use_all_actions=False,
        path_mode="COPY",
        embed_textures=False,
    )
    print(f"[exportar_fbx] {len(objs)} objetos -> {salida} (animación: {'sí' if animar else 'no'})")

    # Copia al proyecto Unity si existe (Assets/Atlas/Modelos/<Organo>/).
    repo = os.path.dirname(carpeta_modelado)
    unity = os.path.join(repo, "AtlasAnatomic", "AtlasAnatomic", "Assets", "Atlas", "Modelos", nombre.capitalize())
    if os.path.isdir(os.path.dirname(unity)):
        import shutil
        os.makedirs(unity, exist_ok=True)
        shutil.copy2(salida, os.path.join(unity, f"{nombre}.fbx"))
        print(f"[exportar_fbx] copiado a Unity: {unity}  (luego: Atlas > 1. Construir... en Unity)")


main()
