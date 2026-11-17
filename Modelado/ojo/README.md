# Ojo (*ñawi*): diseño y recopilación de información

Documento vivo: aquí se va registrando lo que se decide y lo que se averigua sobre el modelo del ojo.
Responsable: Germán Cáceres.

> Estado: **modelo base (blockout) listo**: 9 piezas con medidas aproximadas y colores planos, 27 308 triángulos. Falta refinar formas y texturizar.

## Vista previa del modelo base

| Completa | Desglosada | Corte |
|---|---|---|
| ![Completa](previews/01-completa.png) | ![Desglosada](previews/02-desglosada.png) | ![Corte](previews/03-corte.png) |

El modelo lo genera `../scripts/ojo_modelo_base.py` y las imágenes salen de `../scripts/ojo_previews.py`:

```bash
blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/ojo_modelo_base.py -- --forzar   # regenera las piezas (borra las actuales)
blender -b Modelado/ojo/ojo.blend --python Modelado/scripts/ojo_previews.py                  # vuelve a renderizar las previews
```

**Atención:** `--forzar` borra las piezas `OJO_*` y las crea de nuevo. Una vez que empieces a editar a mano en Blender, **no vuelvas a correr el generador**. Desde ahí el `.blend` es la fuente de verdad.

### Medidas usadas en el modelo base (mm)

| Elemento | Valor | Elemento | Valor |
|---|---|---|---|
| Radio externo del globo | 12.0 | Grosor de esclerótica / coroides / retina | 0.6 / 0.25 / 0.25 |
| Radio de curvatura de la córnea | 7.8 | Diámetro / grosor de la córnea | 11.5 / 0.55 |
| Diámetro del iris / pupila | 12.0 / 3.5 | Diámetro / grosor del cristalino | 9.5 / 4.0 |
| Cuerpo ciliar | anillo de Ø ~14, 70 procesos | Diámetro del nervio óptico | 3.0, sale ~3 mm hacia el lado nasal (-X) |

Las capas se ajustan en Blender: grosor desde el modificador **Grosor** (Solidify) de cada pieza; colores desde los materiales `MAT_OJO_*`.

### Lo que falta para pasar del blockout al modelo final

- [ ] **Pupila negra:** ahora se ve clara porque el cristalino y el vítreo dejan ver la retina iluminada. Opciones: un disco oscuro detrás del iris que se oculte en la vista desglosada, o resolverlo en el shader de Unity.
- [ ] **Texturas:** venas en la esclerótica, fibras radiales en el iris, vasos en retina y coroides (ver `ref-05` y `ref-06`). Pintarlas o hornearlas a PNG en `texturas/`.
- [ ] Forma del cristalino más fiel (cara posterior más curva) y la fosa del vítreo.
- [ ] Marcas sobre la retina: mácula/fóvea y papila (punto ciego).
- [ ] UVs de cada pieza.
- [ ] Revisar las medidas con una fuente citable.

---

## 1. Las tres vistas

El ojo se presenta en realidad aumentada (passthrough del Quest) con tres modos. Ya están implementados en Unity: ver [`AtlasAnatomic/README.md`](../../AtlasAnatomic/README.md). El usuario cambia de modo desde un menú en la mano o con botones flotantes junto al modelo.

### Vista 1: Completa

El globo ocular entero, tal como se ve por fuera (referencias `ref-05` y `ref-06`).

- Se ven la esclerótica (blanca, con venas finas), la córnea (transparente y brillante), el iris y la pupila.
- El usuario puede **agarrarlo, rotarlo y escalarlo** con las manos o los controles.
- Al apuntar a una zona visible aparece una etiqueta corta (nombre en español y kichwa).
- **Objetivo:** que el estudiante reconozca el ojo y su forma real antes de ver el interior.

### Vista 2: Desglosada (explosionada)

Las capas se separan a lo largo del **eje óptico**, de delante hacia atrás (referencias `ref-01` a `ref-03`):

```
Córnea → Iris (con pupila) → Cristalino → Cuerpo ciliar → Humor vítreo → Retina → Coroides → Esclerótica → Nervio óptico
```

- **Separación progresiva:** un deslizador (0 = ojo cerrado, 1 = todo separado), o botones *Siguiente / Anterior* que sacan una capa por paso.
- Al seleccionar una pieza, esta se resalta, las demás se atenúan y aparece su nombre.
- Opción de **aislar** una pieza (ocultar las otras) para examinarla sola.
- **Objetivo:** entender el orden de las capas y qué hay dentro.

### Vista 3: Informativa

Al seleccionar una pieza (desde la vista 1 o la 2) se abre un **panel de información** junto al modelo:

- Nombre en español y en kichwa
- Qué es, su función y un dato clave (medida o número)
- Túnica a la que pertenece
- Botón de **audio** con la narración del texto (accesibilidad visual) y un tamaño de letra ajustable

Incluye además un **corte por túnicas** (referencia `ref-04`): el globo cortado muestra las tres túnicas en colores distintos.

- **Fibrosa** (externa): esclerótica y córnea
- **Vascular** o úvea (media): coroides, cuerpo ciliar e iris
- **Nerviosa** (interna): retina

**Objetivo:** aprender la función de cada parte, no solo su nombre.

---

## 2. Partes a modelar

Orden de explosión, de delante hacia atrás. Los nombres corresponden a los objetos y colecciones en `ojo.blend`.

| # | Objeto en Blender | Parte | Túnica / grupo | Forma base para modelar | Vista 1 |
|---|---|---|---|---|---|
| 1 | `OJO_Cornea` | Córnea | Fibrosa | Casquete esférico transparente, más curvo que el globo | Sí |
| 2 | `OJO_Iris` | Iris (+ pupila) | Vascular | Disco con agujero central (la pupila **no es objeto**, es el hueco) | Sí |
| 3 | `OJO_Cristalino` | Cristalino | Medio transparente | Lente biconvexa, más curva atrás | No |
| 4 | `OJO_Cuerpo_Ciliar` | Cuerpo ciliar | Vascular | Anillo con pliegues radiales (procesos ciliares) | No |
| 5 | `OJO_Humor_Vitreo` | Humor vítreo | Medio transparente | Esfera gelatinosa transparente que llena la cámara posterior | No |
| 6 | `OJO_Retina` | Retina | Nerviosa | Casquete esférico delgado (naranja), abierto por delante | No |
| 7 | `OJO_Coroides` | Coroides | Vascular | Casquete esférico (rojo oscuro, con vasos) | No |
| 8 | `OJO_Esclerotica` | Esclerótica | Fibrosa | Casquete esférico blanco con apertura frontal para la córnea | Sí |
| 9 | `OJO_Nervio_Optico` | Nervio óptico | Nerviosa | Cilindro curvo que sale por detrás, algo hacia la nariz | Sí |

Las capas 6, 7 y 8 son **concéntricas**: el mismo casquete con un *Solidify* de grosor distinto y un radio un poco mayor cada vez, sin huecos entre ellas.

**Fuera del alcance inicial (posible ampliación):** humor acuoso, mácula/fóvea y papila (podrían ir como marcas sobre la retina), músculos extraoculares, párpados y conjuntiva.

---

## 3. Información de cada parte (borrador para el panel)

Textos breves para la vista informativa. Las cifras son valores aproximados de un adulto, tomados de fuentes generales de anatomía. **Pendiente: revisar con una fuente citable** (p. ej. Netter, Moore o Gray) antes de la entrega.

| Parte | Kichwa | Qué es / función | Dato clave |
|---|---|---|---|
| Globo ocular | *ñawi* | Órgano de la visión: capta la luz y la convierte en señales nerviosas. | ~24 mm de diámetro; ~7 g |
| Córnea | pendiente | Ventana transparente frontal. Es la lente más potente del ojo: desvía la luz hacia dentro. | ~11.5 mm de diámetro; ~0.5 mm de grosor; aporta ~2/3 del enfoque |
| Iris | pendiente | Músculo pigmentado que da color al ojo y regula cuánta luz entra, como el diafragma de una cámara. | Su color depende de la melanina |
| Pupila | pendiente | Abertura del centro del iris por donde pasa la luz. | Se contrae con mucha luz y se dilata con poca: ~2 a 8 mm |
| Cristalino | pendiente | Lente flexible que cambia de forma para enfocar de cerca o de lejos (acomodación). | ~9–10 mm de diámetro; ~4 mm de grosor; con la edad pierde flexibilidad (presbicia) y puede opacarse (catarata) |
| Cuerpo ciliar | pendiente | Anillo muscular que sostiene el cristalino y cambia su forma; también produce el humor acuoso. | Une al cristalino mediante fibras (zónula de Zinn) |
| Humor vítreo | pendiente | Gel transparente que llena el interior y mantiene la forma del ojo. | ~4 ml; ~80 % del volumen del ojo; ~99 % agua |
| Retina | pendiente | Capa sensible a la luz con los fotorreceptores, que convierten la luz en impulsos nerviosos. | ~120 millones de bastones (poca luz) y ~6 millones de conos (color); la fóvea da la visión más nítida |
| Coroides | pendiente | Capa llena de vasos que nutre la retina; su pigmento oscuro absorbe la luz sobrante. | Parte de la úvea, junto al iris y el cuerpo ciliar |
| Esclerótica | pendiente | Capa externa blanca y resistente: protege el ojo y sirve de anclaje a los músculos que lo mueven. | Es "lo blanco del ojo"; cubre ~5/6 del globo |
| Nervio óptico | pendiente | Lleva las señales de la retina al cerebro. | ~1.2 millones de fibras; donde sale no hay fotorreceptores (punto ciego) |

**Kichwa:** solo *ñawi* (ojo) está confirmado. Los demás términos deben validarse con un hablante nativo o un docente de kichwa. Para partes sin término propio se puede usar una descripción (perífrasis) en kichwa junto al nombre en español. No se inventan términos.

---

## 4. Aspecto visual (según las referencias)

| Parte | Color / material | Notas para el shader (Principled BSDF) |
|---|---|---|
| Esclerótica | Blanco hueso con venas rojas finas | Textura de color con venas; *Roughness* ~0.4 |
| Córnea | Transparente | *Transmission* 1, *Roughness* ~0.05. En Unity, material transparente de URP |
| Iris | Azul/gris o café, con fibras radiales | Textura de color del iris; en el futuro, color seleccionable |
| Cristalino | Transparente, ligeramente azulado | Igual que la córnea |
| Cuerpo ciliar | Rojo/marrón, pliegues radiales | Los pliegues van en la geometría o en un *normal map* |
| Humor vítreo | Transparente, gelatinoso | Transparente con poca opacidad (*alpha* ~0.3) |
| Retina | Naranja, con vasos finos | Textura con vasos que salen de la papila |
| Coroides | Granate oscuro, con red de vasos | Textura de vasos |
| Nervio óptico | Amarillo pálido / crema, fibroso | *Normal map* de fibras longitudinales |

**Accesibilidad:** cada capa tiene un tono distinto y además se identifica por su etiqueta, nunca solo por el color. Los colores se revisarán con un simulador de daltonismo.

**Rendimiento en Quest:** las transparencias son costosas. Como mucho 3 o 4 piezas transparentes visibles a la vez. Presupuesto del ojo: ≤ 30 000 triángulos.

---

## 5. Decisiones técnicas para Unity

- **Eje óptico:** la córnea mira hacia **-Y** en Blender (lo que se ve en la vista *Front*, `Numpad 1`). Todas las piezas comparten ese eje.
- **Origen de cada pieza:** en su propio centro. Unity separa las piezas sumando un desplazamiento sobre el eje óptico.
- **Vista desglosada:** se calcula en Unity (posición inicial + factor × distancia de cada pieza), no con una animación de Blender. Así el deslizador funciona en tiempo real.
- **Corte por túnicas:** se resuelve en Unity con un plano de corte (shader). Si no alcanza el rendimiento, el plan B es una versión precortada en Blender (`OJO_Corte_*`).
- **Textos:** van en Unity Localization (tablas español/kichwa), no dentro del modelo.

---

## 6. Referencias visuales

En `referencias/`. Son imágenes de terceros encontradas en internet: sirven **solo como guía de modelado** y no se incluyen en la aplicación ni en la documentación entregada.

| Archivo | Muestra | Fuente |
|---|---|---|
| `ref-01-explosionada-rosa.png` | Explosionada con etiquetas en español | Internet, autor desconocido |
| `ref-02-explosionada-azul.png` | Explosionada con etiquetas en español | Internet, autor desconocido |
| `ref-03-explosionada-ingles.png` | Explosionada con etiquetas en inglés | Internet, autor desconocido |
| `ref-04-corte-tunicas.png` | Corte con las túnicas fibrosa, vascular y nerviosa | Internet, autor desconocido |
| `ref-05-globo-completo-gris.png` | Globo completo, iris gris/café | Anatomy for Sculptors |
| `ref-06-globo-completo-cafe.png` | Globo completo, iris café | Internet, autor desconocido |

---

## 7. Pendientes

- [ ] Validar los términos en kichwa de cada parte
- [ ] Contrastar las cifras de la sección 3 con una fuente citable y anotarla
- [ ] Decidir el control de la vista desglosada: deslizador, pasos o ambos
- [ ] Decidir si el corte por túnicas se hace con shader o con un modelo precortado
- [ ] Definir si la narración de audio se graba o se usa texto a voz
- [x] Modelo base de las 9 piezas
- [ ] Refinar y texturizar (ver "Lo que falta" arriba y el checklist en `../README.md`)
- [ ] Importar `Exportados/ojo.fbx` en Unity y comprobar escala, eje y transparencias

## Bitácora

- 2026-11-10: definidas las tres vistas (completa, desglosada, informativa con corte por túnicas). Se agregan el cuerpo ciliar y el orden de explosión según las referencias. Primer borrador de los textos informativos.
- 2026-11-10: modelo base generado por script (9 piezas, 27 308 triángulos), previews de las tres vistas y primer `ojo.fbx` exportado. Transparencias por alpha (no transmisión) para que funcionen en el Quest.
