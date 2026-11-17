# AtlasAnatomic: aplicación de realidad aumentada (Unity)

Proyecto Unity **6000.6.8f1** creado con la plantilla **Mixed Reality (MR)**: AR Foundation, OpenXR con Meta, XR Interaction Toolkit 3 y URP. Dispositivo objetivo: **Meta Quest 3** (passthrough a color); también funciona en el Quest 2 (passthrough en blanco y negro).

El proyecto está en `AtlasAnatomic/AtlasAnatomic/`. Todo lo propio del atlas vive en `Assets/Atlas/`. El resto es de la plantilla.

```
Assets/Atlas/
├── Modelos/Ojo/ojo.fbx          # copia de Modelado/Exportados/ojo.fbx
├── Materiales/Ojo/              # materiales URP (los genera el constructor)
├── Prefabs/Ojo.prefab           # ojo listo para la escena (lo genera el constructor)
├── Scripts/
│   ├── ParteAnatomica.cs        # datos de una pieza (nombre, kichwa, túnica, texto, paso de desglose)
│   ├── DatosOjo.cs              # contenido inicial de las 9 piezas del ojo
│   ├── OrganoController.cs      # vistas, desglose, selección, resaltado, túnicas
│   └── MenuAtlas.cs             # panel flotante (se construye por código)
└── Editor/ConstructorAtlas.cs   # menú "Atlas" del editor
Assets/Scenes/AtlasAnatomico.unity   # escena del atlas (la genera el constructor)
```

## Primer uso

1. Abre el proyecto en Unity y espera a que compile.
2. En la barra de menús: **Atlas → 1. Construir ojo y escena del Atlas**. El constructor:
   - configura la importación de `ojo.fbx` y crea los materiales URP (córnea, cristalino y vítreo transparentes);
   - crea `Ojo.prefab`: cada pieza con collider y selección por rayo o pellizco, y la esfera de agarre del ojo completo (con dos manos se cambia el tamaño);
   - copia la escena de la plantilla a `AtlasAnatomico.unity` y le **quita lo que no es del atlas**: el entorno de demostración, el panel "Player Settings", el tutorial inicial (Goal Manager / Coaching UI), el generador de objetos de ejemplo y los disparadores que creaban objetos al tocar superficies;
   - deja el ojo y el **Menu Atlas** en la escena, y pone la escena como la primera del build.
3. Pulsa **Play** para probar con el simulador, o compila para el Quest (*File → Build Profiles → Android*).

El constructor se puede ejecutar de nuevo cuando cambie el FBX: regenera materiales, prefab y escena. **Los cambios hechos a mano en `AtlasAnatomico.unity` se pierden al regenerarla.** Los textos de las piezas se editan en el Inspector del componente `OrganoController` (en el prefab `Ojo`).

## Qué hace la aplicación

**Menú del atlas** (panel flotante, se usa con rayo o tocándolo con el dedo; se mueve agarrando la barra inferior):

| Sección | Contenido |
|---|---|
| Órgano | Ojo (*Ñawi*). Pulmones y Riñón aparecen como "Próximamente". |
| Vista | **Completa** · **Desglosada** · **Informativa** |
| Separación de capas | Deslizador 0–1 y botones *Pieza anterior / siguiente* (desglosada e informativa) |
| Partes del ojo | 9 botones, uno por pieza |
| Información | Nombre, kichwa, túnica, descripción, dato clave y el botón *Colorear túnicas* (fibrosa/vascular/nerviosa) |
| Ajustes | Traer aquí · Reiniciar · Tamaño −/+ · Texto A−/A+ (accesibilidad) |

**Vistas:**
- **Completa:** el ojo de unos 20 cm (unas 8 veces el tamaño real), en vista de tres cuartos. Se agarra y se gira. Con dos manos se escala.
- **Desglosada:** el ojo gira de lado (córnea a la izquierda) y las capas se separan a lo largo del eje óptico. Apuntando a una pieza y pellizcando, se resalta.
- **Informativa:** la pieza elegida se resalta, las demás se vuelven transparentes y el panel muestra su información.

## Notas técnicas

- `OrganoController` no depende de las unidades del FBX. Mide la esclerótica para fijar el tamaño mostrado (`tamanoMostrado`, en metros) y deduce el eje de desglose desde la posición de la córnea respecto al vítreo.
- Las piezas se buscan por nombre (`OJO_<id>`). Para otro órgano: exportar su FBX con el mismo convenio, crear un `DatosPulmones.cs` y extender el constructor.
- Transparencias por alpha (URP Lit, *Surface: Transparent*), sin refracción, para que corran en el Quest.

## Pendiente

- Colocar el ojo sobre una mesa detectada (planos AR), además de "frente al usuario".
- Corte por túnicas con plano de corte (shader).
- Textos en kichwa validados y migración a Unity Localization.
- Narración por audio de la información (accesibilidad visual).
- Probar en el Quest 3 y medir el rendimiento.
