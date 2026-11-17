using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtlasAnatomico.Pruebas
{
    /// <summary>
    /// Abre la escena del atlas en modo Play y genera las capturas de los informes
    /// (ver Assets/Atlas/Editor/CapturasInforme.cs). Por consola:
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter PruebaCapturas
    /// </summary>
    public class PruebaCapturas
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator GenerarCapturasDelInforme()
        {
#if UNITY_EDITOR
            // Sin visor, la plantilla MR registra errores propios (oclusión, seguimiento de manos)
            // que no afectan al atlas; no deben hacer fallar la prueba.
            LogAssert.ignoreFailingMessages = true;

            // Los scripts del atlas viven en Assembly-CSharp(-Editor), que un asmdef no puede referenciar.
            var tipo = Type.GetType("AtlasAnatomico.Editor.CapturasInforme, Assembly-CSharp-Editor");
            Assert.IsNotNull(tipo, "No se encontró CapturasInforme en Assembly-CSharp-Editor.");
            var escena = (string)tipo.GetField("Escena").GetValue(null);

            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                escena, new LoadSceneParameters(LoadSceneMode.Single));

            var pasos = (IEnumerator)tipo.GetMethod("Capturar").Invoke(null, null);
            while (pasos.MoveNext()) yield return pasos.Current;
#else
            yield break;
#endif
        }
    }
}
