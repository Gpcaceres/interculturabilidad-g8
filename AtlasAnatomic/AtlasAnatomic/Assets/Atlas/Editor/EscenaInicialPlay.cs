using UnityEditor;
using UnityEditor.SceneManagement;

namespace AtlasAnatomico.Editor
{
    /// <summary>
    /// Hace que Play arranque siempre con la escena del atlas, aunque en el editor
    /// esté abierta otra (por ejemplo SampleScene, la demo de la plantilla MR).
    /// Se desactiva desde el menú Atlas → 3. Play con la escena del Atlas.
    /// </summary>
    [InitializeOnLoad]
    public static class EscenaInicialPlay
    {
        const string Escena = "Assets/Scenes/AtlasAnatomico.unity";
        const string RutaMenu = "Atlas/3. Play con la escena del Atlas";
        const string Clave = "AtlasAnatomico.PlayConEscenaAtlas";

        static bool Activo
        {
            get => EditorPrefs.GetBool(Clave, true);
            set => EditorPrefs.SetBool(Clave, value);
        }

        static EscenaInicialPlay()
        {
            EditorApplication.delayCall += Aplicar;
            EditorApplication.playModeStateChanged += estado =>
            {
                if (estado == PlayModeStateChange.ExitingEditMode || estado == PlayModeStateChange.EnteredEditMode) Aplicar();
            };
        }

        /// <summary>El Test Runner entra en Play con su propia escena: si se reemplaza, las pruebas no arrancan.</summary>
        static bool EnPruebas =>
            System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-runTests") >= 0 ||
            EditorSceneManager.GetActiveScene().name.StartsWith("InitTestScene");

        static void Aplicar()
        {
            EditorSceneManager.playModeStartScene = Activo && !EnPruebas
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(Escena)
                : null;
        }

        [MenuItem(RutaMenu, priority = 3)]
        static void Alternar()
        {
            Activo = !Activo;
            Aplicar();
        }

        [MenuItem(RutaMenu, true)]
        static bool AlternarValidar()
        {
            Menu.SetChecked(RutaMenu, Activo);
            return true;
        }
    }
}
