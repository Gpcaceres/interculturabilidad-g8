using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        public IEnumerator GenerarCapturasDelInforme() => Ejecutar("Capturar");

        [UnityTest, Timeout(300000)]
        public IEnumerator PulmonesRespiranYSeDesglosan() => Ejecutar("CapturarPulmones");

        static IEnumerator Ejecutar(string metodo)
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

            // Los componentes que se instalan solos lo hacen con la primera escena de Play, no con esta.
            foreach (var nombre in new[] { "ModoEscritorio", "EtiquetasOrgano", "VentanaParte" })
                Type.GetType($"AtlasAnatomico.{nombre}, Assembly-CSharp")
                    .GetMethod("Instalar", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

            // Las fotos y esperas son corrutinas anidadas: se recorren a mano para no perder sus excepciones.
            var pila = new Stack<IEnumerator>();
            pila.Push((IEnumerator)tipo.GetMethod(metodo).Invoke(null, null));
            while (pila.Count > 0)
            {
                if (!pila.Peek().MoveNext()) { pila.Pop(); continue; }
                if (pila.Peek().Current is IEnumerator anidada) pila.Push(anidada);
                else yield return pila.Peek().Current;
            }
#else
            yield break;
#endif
        }
    }
}
