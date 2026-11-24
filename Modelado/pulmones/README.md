# Pulmones (*surku*): modelo base

Responsable: Carlos Rivera. Estado: **modelo base listo** (28 639 triángulos, 8 piezas, texturas horneadas y respiración), integrado en Unity.

| Completa | Árbol bronquial | Desglosada |
|---|---|---|
| ![](previews/01-completa.png) | ![](previews/03-arbol.png) | ![](previews/02-desglosada.png) |

Capturas de Unity en `previews/unity/`.

```bash
blender -b Modelado/pulmones/pulmones.blend --python Modelado/scripts/pulmones_modelo_base.py -- --forzar
blender -b Modelado/pulmones/pulmones.blend --python Modelado/scripts/pulmones_previews.py
blender -b Modelado/pulmones/pulmones.blend --python Modelado/scripts/exportar_fbx.py
```

Después, en Unity: **Atlas → 1. Construir órganos y escena del Atlas**.

**Atención:** `--forzar` borra las piezas `PUL_*`. Si vas a refinar a mano, no vuelvas a correr el generador: desde ahí el `.blend` es la fuente de verdad.

- **Respiración:** shape key `Inhalar` en todas las piezas menos la tráquea (+15,9 % de volumen). Todas usan el mismo campo de desplazamiento, así no se abren huecos. Si se mueven vértices a mano, hay que revisar también la clave.
- **Flujo de aire:** `Exportados/pulmones_flujo.json` guarda 105 recorridos tráquea → puntas. Se genera junto con el árbol bronquial; si se edita el árbol a mano, dejará de coincidir.

## Pendiente
- [ ] Revisar formas y medidas con una fuente citable.
- [ ] Validar *surku* y los términos de las piezas con un hablante nativo.
- [ ] Vasos pulmonares (arterias y venas), laringe y alvéolos.
- [ ] Probar rendimiento en el Quest.
