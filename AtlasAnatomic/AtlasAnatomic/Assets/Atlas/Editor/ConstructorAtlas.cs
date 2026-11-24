using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace AtlasAnatomico.Editor
{
    /// <summary>
    /// Menú "Atlas" del editor: arma el prefab del ojo y la escena del atlas a partir
    /// de la plantilla Mixed Reality, quitando lo que no corresponde al proyecto.
    /// Se puede volver a ejecutar: regenera prefab, materiales y escena.
    /// </summary>
    public static partial class ConstructorAtlas
    {
        const string RaizAtlas = "Assets/Atlas";
        const string RutaFbxOjo = RaizAtlas + "/Modelos/Ojo/ojo.fbx";
        const string RutaMateriales = RaizAtlas + "/Materiales/Ojo";
        const string RutaPrefabOjo = RaizAtlas + "/Prefabs/Ojo.prefab";
        const string EscenaPlantilla = "Assets/Scenes/SampleScene.unity";
        const string EscenaAtlas = "Assets/Scenes/AtlasAnatomico.unity";

        // parte: color (sRGB), suavidad, alpha (<1 = transparente)
        static readonly Dictionary<string, (Color color, float suavidad, float alpha)> Materiales =
            new Dictionary<string, (Color, float, float)>
            {
                { "Esclerotica",   (new Color(0.93f, 0.89f, 0.85f), 0.55f, 1f) },
                { "Cornea",        (new Color(0.90f, 0.96f, 1.00f), 0.95f, 0.18f) },
                { "Iris",          (new Color(0.27f, 0.47f, 0.70f), 0.40f, 1f) },
                { "Cristalino",    (new Color(0.82f, 0.90f, 1.00f), 0.90f, 0.40f) },
                { "Cuerpo_Ciliar", (new Color(0.58f, 0.22f, 0.18f), 0.35f, 1f) },
                { "Humor_Vitreo",  (new Color(0.80f, 0.90f, 0.97f), 0.85f, 0.22f) },
                { "Retina",        (new Color(0.96f, 0.58f, 0.20f), 0.45f, 1f) },
                { "Coroides",      (new Color(0.45f, 0.13f, 0.14f), 0.40f, 1f) },
                { "Nervio_Optico", (new Color(0.93f, 0.82f, 0.58f), 0.35f, 1f) },
            };

        [MenuItem("Atlas/1. Construir órganos y escena del Atlas", priority = 1)]
        public static void ConstruirTodo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigurarImportacionFbx();
            var fantasma = CrearMateriales();
            var prefab = CrearPrefabOjo(fantasma);
            if (prefab == null) return;
            CrearEscena(prefab, CrearPulmones());
            EditorUtility.DisplayDialog("Atlas Anatómico",
                "Listo.\n\n• Prefabs: " + RutaPrefabOjo + ", " + RutaPrefabPulmones + "\n• Escena: " + EscenaAtlas +
                "\n\nPulsa Play para probar con el simulador XR, o compila para el Quest.", "OK");
        }

        [MenuItem("Atlas/2. Abrir escena del Atlas", priority = 2)]
        public static void AbrirEscena()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(EscenaAtlas);
        }

        // ------------------------------------------------------------------ FBX

        static void ConfigurarImportacionFbx()
        {
            var importer = AssetImporter.GetAtPath(RutaFbxOjo) as ModelImporter;
            if (importer == null)
                throw new System.Exception("No se encontró " + RutaFbxOjo + ". Copia Modelado/Exportados/ojo.fbx allí.");
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // usamos materiales URP propios
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;          // necesario para los MeshCollider en dispositivo
            importer.useFileScale = true;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ Materiales

        static Material CrearMateriales()
        {
            CrearCarpeta(RutaMateriales);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var kv in Materiales)
                GuardarMaterial(shader, "MAT_OJO_" + kv.Key, kv.Value.color, kv.Value.suavidad, kv.Value.alpha);
            return GuardarMaterial(shader, "MAT_Fantasma", new Color(0.75f, 0.80f, 0.88f), 0.6f, 0.10f);
        }

        static Material GuardarMaterial(Shader shader, string nombre, Color color, float suavidad, float alpha,
                                         string carpeta = RutaMateriales)
        {
            var ruta = $"{carpeta}/{nombre}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, ruta);
            }
            mat.shader = shader;
            color.a = alpha;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", suavidad);
            mat.SetFloat("_Metallic", 0f);
            bool transparente = alpha < 1f;
            mat.SetFloat("_Surface", transparente ? 1f : 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", transparente ? 0f : 1f);
            mat.SetFloat("_SrcBlend", transparente ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            mat.SetFloat("_DstBlend", transparente ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", transparente ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_Cull", transparente ? (float)CullMode.Back : (float)CullMode.Back);
            if (transparente)
            {
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = (int)RenderQueue.Geometry;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ Prefab

        static GameObject CrearPrefabOjo(Material fantasma)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaFbxOjo);
            if (modelo == null)
            {
                EditorUtility.DisplayDialog("Atlas", "No se pudo cargar " + RutaFbxOjo, "OK");
                return null;
            }
            CrearCarpeta(RaizAtlas + "/Prefabs");

            var raiz = new GameObject("Ojo");
            try
            {
                var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
                instancia.name = "Modelo";
                var ctrl = raiz.AddComponent<OrganoController>();
                ctrl.nombreOrgano = DatosOjo.Nombre;
                ctrl.kichwaOrgano = DatosOjo.Kichwa;
                ctrl.prefijo = DatosOjo.Prefijo;
                ctrl.partes = DatosOjo.Partes();
                ctrl.materialFantasma = fantasma;

                int encontradas = 0;
                foreach (var parte in ctrl.partes)
                {
                    var t = instancia.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(x => x.name == DatosOjo.Prefijo + parte.id);
                    if (t == null) { Debug.LogWarning("[Atlas] Falta la pieza " + parte.id + " en el FBX"); continue; }
                    encontradas++;
                    var mat = AssetDatabase.LoadAssetAtPath<Material>($"{RutaMateriales}/MAT_OJO_{parte.id}.mat");
                    foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    var mf = t.GetComponent<MeshFilter>();
                    if (mf != null && t.GetComponent<MeshCollider>() == null)
                        t.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    if (t.GetComponent<XRSimpleInteractable>() == null)
                        t.gameObject.AddComponent<XRSimpleInteractable>();
                }
                if (encontradas == 0)
                {
                    EditorUtility.DisplayDialog("Atlas", "El FBX no contiene piezas OJO_*.", "OK");
                    return null;
                }

                // Agarre del ojo completo: esfera (el controlador ajusta su tamaño al iniciar).
                var esfera = raiz.AddComponent<SphereCollider>();
                esfera.radius = 0.013f;
                ctrl.colliderAgarre = esfera;
                var rb = raiz.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                var agarre = raiz.AddComponent<XRGrabInteractable>();
                agarre.colliders.Add(esfera);   // solo la esfera; las piezas tienen su propio interactuable
                agarre.movementType = XRBaseInteractable.MovementType.Instantaneous;
                agarre.throwOnDetach = false;
                agarre.useDynamicAttach = true;
                agarre.selectMode = InteractableSelectMode.Multiple;
                var transformador = raiz.AddComponent<XRGeneralGrabTransformer>();
                transformador.allowTwoHandedScaling = true;

                var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabOjo);
                Debug.Log($"[Atlas] Prefab del ojo creado con {encontradas} piezas: {RutaPrefabOjo}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        // ------------------------------------------------------------------ Escena

        static void CrearEscena(GameObject prefabOjo, GameObject prefabPulmones)
        {
            if (!System.IO.File.Exists(EscenaPlantilla))
            {
                EditorUtility.DisplayDialog("Atlas", "No se encontró la escena de la plantilla: " + EscenaPlantilla, "OK");
                return;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Se sobrescribe el archivo sin tocar su .meta: así conserva el guid que usa la configuración del build.
            System.IO.File.Copy(EscenaPlantilla, EscenaAtlas, true);
            AssetDatabase.ImportAsset(EscenaAtlas, ImportAssetOptions.ForceUpdate);
            var escena = EditorSceneManager.OpenScene(EscenaAtlas, OpenSceneMode.Single);

            // Fuera: contenido de demostración de la plantilla.
            foreach (var nombre in new[] { "Environment", "UI" })
                Eliminar(escena, nombre);

            // Dentro de MR Interaction Setup: tutorial inicial y generador de objetos de ejemplo.
            var mr = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "MR Interaction Setup");
            if (mr != null)
            {
                foreach (var nombre in new[] { "Goal Manager", "Object Spawner" })
                {
                    var t = mr.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nombre);
                    if (t != null) EliminarObjeto(t.gameObject);
                }
                foreach (var c in mr.GetComponentsInChildren<MonoBehaviour>(true)
                             .Where(c => c != null && (c.GetType().Name == "ARContactSpawnTrigger" ||
                                                       c.GetType().Name == "ARInteractorSpawnTrigger")).ToList())
                {
                    try { Object.DestroyImmediate(c); }
                    catch { c.enabled = false; }
                }
            }
            else Debug.LogWarning("[Atlas] No se encontró 'MR Interaction Setup' en la escena.");

            // Ojo y menú.
            var ojo = (GameObject)PrefabUtility.InstantiatePrefab(prefabOjo, escena);
            ojo.transform.position = new Vector3(0f, 1.3f, 0.6f);
            var menu = new GameObject("Menu Atlas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(menu, escena);
            menu.transform.position = new Vector3(0.35f, 1.3f, 0.65f);
            var menuAtlas = menu.AddComponent<MenuAtlas>();
            menuAtlas.organo = ojo.GetComponent<OrganoController>();
            menuAtlas.organos.Add(menuAtlas.organo);
            if (prefabPulmones != null)
            {
                var pulmones = (GameObject)PrefabUtility.InstantiatePrefab(prefabPulmones, escena);
                pulmones.transform.position = ojo.transform.position;
                pulmones.SetActive(false);   // el menú lo activa al elegirlo
                menuAtlas.organos.Add(pulmones.GetComponent<OrganoController>());
            }

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);

            // Primera escena del build.
            var escenas = EditorBuildSettings.scenes.Where(s => s.path != EscenaAtlas).ToList();
            escenas.Insert(0, new EditorBuildSettingsScene(EscenaAtlas, true));
            foreach (var s in escenas.Where(s => s.path == EscenaPlantilla)) s.enabled = false;
            EditorBuildSettings.scenes = escenas.ToArray();
            Debug.Log("[Atlas] Escena creada: " + EscenaAtlas);
        }

        static void Eliminar(Scene escena, string nombre)
        {
            foreach (var g in escena.GetRootGameObjects().Where(g => g.name == nombre).ToList())
                EliminarObjeto(g);
        }

        static void EliminarObjeto(GameObject g)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(g) && !PrefabUtility.IsOutermostPrefabInstanceRoot(g))
            {
                try { Object.DestroyImmediate(g); }
                catch { g.SetActive(false); }
                return;
            }
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(g))
                PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(g);
        }

        static void CrearCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta)) return;
            var padre = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
            CrearCarpeta(padre);
            AssetDatabase.CreateFolder(padre, System.IO.Path.GetFileName(ruta));
        }
    }
}
