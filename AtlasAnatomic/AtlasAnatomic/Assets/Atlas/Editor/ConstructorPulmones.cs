using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace AtlasAnatomico.Editor
{
    /// <summary>Pulmones: importación, materiales con textura y prefab con respiración y flujo de aire.</summary>
    public static partial class ConstructorAtlas
    {
        const string RaizPulmones = RaizAtlas + "/Modelos/Pulmones";
        const string RutaFbxPulmones = RaizPulmones + "/pulmones.fbx";
        const string RutaMatPulmones = RaizAtlas + "/Materiales/Pulmones";
        const string RutaPrefabPulmones = RaizAtlas + "/Prefabs/Pulmones.prefab";

        static GameObject CrearPulmones()
        {
            if (!(AssetImporter.GetAtPath(RutaFbxPulmones) is ModelImporter importer))
            {
                Debug.LogWarning("[Atlas] No se encontró " + RutaFbxPulmones + ": la escena queda solo con el ojo.");
                return null;
            }
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;        // la respiración la mueve Respiracion.cs
            importer.importBlendShapes = true;
            importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.useFileScale = true;
            importer.SaveAndReimport();

            CrearCarpeta(RutaMatPulmones);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { RaizPulmones }))
            {
                var ruta = AssetDatabase.GUIDToAssetPath(guid);
                var tex = (TextureImporter)AssetImporter.GetAtPath(ruta);
                tex.textureType = ruta.EndsWith("_Normal.png") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                tex.maxTextureSize = 1024;
                tex.mipmapEnabled = true;
                tex.anisoLevel = 4;
                tex.SaveAndReimport();
            }

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaFbxPulmones);
            CrearCarpeta(RaizAtlas + "/Prefabs");
            var raiz = new GameObject("Pulmones");
            try
            {
                var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
                instancia.name = "Modelo";
                var ctrl = raiz.AddComponent<OrganoController>();
                ctrl.nombreOrgano = DatosPulmones.Nombre;
                ctrl.kichwaOrgano = DatosPulmones.Kichwa;
                ctrl.prefijo = DatosPulmones.Prefijo;
                ctrl.partes = DatosPulmones.Partes();
                ctrl.tituloPartes = "Partes de los pulmones";
                ctrl.nombreGrupo = "Grupo";
                ctrl.textoColorear = "Colorear grupos";
                ctrl.piezaReferencia = "Diafragma";
                ctrl.piezaCentral = "Bronquios";
                ctrl.piezaFrontal = "Pulmon_Der_Lobulo_Med";
                ctrl.desgloseRadial = true;
                ctrl.frenteLocal = Vector3.forward;
                ctrl.anguloCompleta = -18f;
                ctrl.anguloDesglose = 0f;
                ctrl.factorMenu = new Vector2(1.35f, 2.1f);
                ctrl.tamanoMostrado = 0.30f;
                ctrl.distanciaAlUsuario = 0.8f;
                ctrl.materialFantasma = AssetDatabase.LoadAssetAtPath<Material>(RutaMateriales + "/MAT_Fantasma.mat");
                ctrl.materialPorDentro = GuardarMaterial(Shader.Find("Universal Render Pipeline/Lit"), "MAT_PUL_Por_Dentro",
                    new Color(0.95f, 0.62f, 0.62f), 0.75f, 0.16f, RutaMatPulmones);

                foreach (var parte in ctrl.partes)
                {
                    var t = instancia.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(x => x.name == DatosPulmones.Prefijo + parte.id);
                    if (t == null) { Debug.LogWarning("[Atlas] Falta la pieza " + parte.id + " en el FBX"); continue; }
                    var mat = MaterialPulmon(parte.id);
                    var r = t.GetComponent<Renderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    Mesh malla;
                    if (r is SkinnedMeshRenderer piel)
                    {
                        piel.updateWhenOffscreen = true;   // los límites siguen a la respiración
                        malla = piel.sharedMesh;
                    }
                    else malla = t.GetComponent<MeshFilter>().sharedMesh;
                    t.gameObject.AddComponent<MeshCollider>().sharedMesh = malla;
                    t.gameObject.AddComponent<XRSimpleInteractable>();
                }

                raiz.AddComponent<Respiracion>();
                var aire = raiz.AddComponent<FlujoAire>();
                aire.recorridos = AssetDatabase.LoadAssetAtPath<TextAsset>(RaizPulmones + "/pulmones_flujo.json");
                aire.material = MaterialAire();

                // Agarre de los pulmones completos, como en el ojo.
                var esfera = raiz.AddComponent<SphereCollider>();
                esfera.radius = 0.13f;
                ctrl.colliderAgarre = esfera;
                var rb = raiz.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                var agarre = raiz.AddComponent<XRGrabInteractable>();
                agarre.colliders.Add(esfera);
                agarre.movementType = XRBaseInteractable.MovementType.Instantaneous;
                agarre.throwOnDetach = false;
                agarre.useDynamicAttach = true;
                agarre.selectMode = InteractableSelectMode.Multiple;
                raiz.AddComponent<XRGeneralGrabTransformer>().allowTwoHandedScaling = true;

                return PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabPulmones);
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        static Material MaterialPulmon(string id)
        {
            bool via = id == "Traquea" || id == "Bronquios";
            var mat = GuardarMaterial(Shader.Find("Universal Render Pipeline/Lit"), "MAT_PUL_" + id,
                Color.white, via ? 0.55f : 0.48f, 1f, RutaMatPulmones);
            var nombre = via ? "PUL_Vias" : "PUL_" + id;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{RaizPulmones}/{nombre}_BaseColor.png"));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RaizPulmones}/{nombre}_Normal.png");
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            // Emisión negra: deja lista la variante del shader que usa el resaltado.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (via) mat.SetFloat("_Cull", (float)CullMode.Off);   // se ve el interior del tubo
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Punto de luz suave, aditivo, para las partículas de aire.</summary>
        static Material MaterialAire()
        {
            const int lado = 64;
            const string rutaTex = RutaMatPulmones + "/aire.asset";
            AssetDatabase.DeleteAsset(rutaTex);
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float d = new Vector2(x - (lado - 1) * 0.5f, y - (lado - 1) * 0.5f).magnitude / (lado * 0.5f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, rutaTex);

            const string ruta = RutaMatPulmones + "/MAT_PUL_Aire.mat";
            var shader = Shader.Find("Atlas/Aire aditivo");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, ruta);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Intensidad", 1.6f);
            mat.renderQueue = -1;   // la del shader
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
