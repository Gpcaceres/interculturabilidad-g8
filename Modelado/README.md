# Modelado 3D: Atlas Anatómico Inmersivo

Modelos en Blender 5.3 de los tres órganos del Atlas Anatómico en **realidad aumentada** (Unity 6, plantilla Mixed Reality, Meta Quest 3/2). La app está en [`../AtlasAnatomic`](../AtlasAnatomic/README.md).

| Órgano | Kichwa | Responsable | Archivo | Requisito en Unity |
|---|---|---|---|---|
| Ojo | *ñawi* | Germán | `ojo/ojo.blend` | Vista explosionada por capas |
| Pulmones | *surku* | Carlos | `pulmones/pulmones.blend` | Animación de respiración |
| Riñón | *rurun* | Samir | `rinon/rinon.blend` | Corte transversal |

## Estructura

```
Modelado/
├── <organo>/
│   ├── <organo>.blend     # un archivo por órgano, cada integrante trabaja el suyo
│   ├── texturas/          # PNG horneados (BaseColor, Normal), máx. 2048 px
│   └── referencias/       # imágenes anatómicas de referencia (con fuente/licencia)
├── Exportados/            # FBX generados para Unity (no editar a mano)
└── scripts/
    ├── configurar_escena.py   # prepara unidades, colecciones y referencia de escala
    ├── exportar_fbx.py        # exporta a FBX con los ajustes de Unity
    ├── ojo_modelo_base.py     # genera el blockout de las 9 piezas del ojo
    └── ojo_previews.py        # renderiza las previews del ojo (completa, desglosada, corte)
```

Los `.blend` ya vienen configurados: plantilla **General**, unidades métricas (escala 1.0, medida en cm), sin cubo/cámara/luz, una colección por parte anatómica y una colección `_Referencias` con un volumen en alambre del tamaño real del órgano.

## Convenciones

- **Nombres:** `<PREFIJO>_<Parte>` en español sin tildes (`OJO_Cornea`, `PUL_Pulmon_Der_Lobulo_Sup`, `RIN_Corteza`). Unity busca las piezas por este nombre; el texto en kichwa va en Unity Localization, no en Blender.
- **Una parte = un objeto** dentro de su colección. No unir (`Ctrl+J`) partes distintas.
- **Escala real:** el modelo debe coincidir con el volumen de `_Referencias` (ojo ~2.4 cm, pulmones ~26×15×25 cm, riñón ~11×6×3 cm).
- **Origen:** en el centro del órgano para la pieza principal; cada parte con su origen en su propio centro (`Set Origin > Origin to Geometry`) para poder separarla.
- **Materiales:** solo *Principled BSDF* con texturas de imagen. Los nodos procedurales no pasan a Unity: hornearlos (*Bake*) a PNG.
- **Presupuesto:** ≤ 30 000 triángulos por órgano (≈ 100 000 en toda la escena del Quest). Usa *Decimate* o retopología si te pasas.
- `_Referencias` nunca se exporta.

## Requisitos específicos

**Ojo (vistas completa, desglosada e informativa; detalle en [`ojo/README.md`](ojo/README.md)):** 9 piezas como objetos separados, en orden de explosión: córnea, iris, cristalino, cuerpo ciliar, humor vítreo, retina, coroides, esclerótica, nervio óptico. Modela cada capa con grosor (*Solidify*) y sin huecos entre ellas. Unity desplaza cada pieza a lo largo de su eje.

**Pulmones (respiración):** dos *Shape Keys* por lóbulo, `Basis` (exhalación) y `Inhalar` (≈ +15 % de volumen, expansión hacia abajo y hacia los lados). Anima el valor 0→1→0 en un ciclo de 4 s (96 frames a 24 fps). El diafragma baja al inhalar. El exportador detecta la animación y la incluye.

**Riñón (corte transversal):** modela el interior real (corteza, pirámides medulares, pelvis renal), no solo la superficie. Deja las caras interiores cerradas para que el corte en Unity (shader de plano de corte) no muestre huecos. Opcional: una versión ya cortada a la mitad como objetos `RIN_Corte_*`.

## Flujo de trabajo

1. Abre tu `.blend` y modela cada parte dentro de su colección.
2. Antes de exportar: selecciona todo y aplica `Ctrl+A > All Transforms` (en los pulmones, aplica solo *Scale* si ya hay shape keys).
3. Exporta: pestaña **Scripting**, abre `scripts/exportar_fbx.py` y pulsa **Run Script**. O por consola, desde la raíz del repo:
   ```bash
   blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/exportar_fbx.py
   ```
   La consola muestra el conteo de triángulos y avisa si hay transformaciones sin aplicar.
4. El exportador también copia el FBX al proyecto Unity (`AtlasAnatomic/AtlasAnatomic/Assets/Atlas/Modelos/<Organo>/`). En Unity: **Atlas → 1. Construir ojo y escena del Atlas**.
5. Haz commit del `.blend` y del FBX.

Si creas un `.blend` nuevo o se borran colecciones, regenera la estructura (no borra nada existente):
```bash
blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/configurar_escena.py -- ojo
```

## Checklist por órgano

- [ ] Referencias anatómicas guardadas en `referencias/` (con fuente)
- [ ] Todas las partes modeladas, cada una en su colección y con su nombre
- [ ] Escala comparada con `_Referencias`
- [ ] Origen de cada parte en su centro
- [ ] UVs desplegadas sin solapes
- [ ] Materiales Principled BSDF; texturas horneadas en `texturas/`
- [ ] ≤ 30 000 triángulos (revisar la salida del exportador)
- [ ] Transformaciones aplicadas
- [ ] Requisito específico listo (explosionada / respiración / corte)
- [ ] FBX exportado en `Exportados/` y probado en Unity
- [ ] Colores contrastados y distinguibles para daltonismo (accesibilidad visual)

## Git

Los `.blend`, `.fbx` y las texturas se guardan con **Git LFS** (ver `.gitattributes`). Cada integrante debe ejecutar una vez:
```bash
git lfs install
```
Los `.blend` son binarios y no se pueden fusionar: trabaja solo en tu archivo y avisa al grupo antes de tocar uno ajeno.
