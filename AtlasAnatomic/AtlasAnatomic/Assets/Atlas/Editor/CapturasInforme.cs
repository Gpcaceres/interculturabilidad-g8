using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AtlasAnatomico.Editor
{
    /// <summary>
    /// Recorre las vistas del atlas en modo Play y guarda una captura de cada una
    /// para los informes (Documentartion/.../Img/capturas).
    /// La ejecuta la prueba PlayMode "PruebaCapturas" (Assets/Atlas/Tests); se puede lanzar
    /// desde el Test Runner o por consola con -runTests -testPlatform PlayMode.
    /// Sin visor no hay passthrough: el fondo es un color liso.
    /// </summary>
    public static class CapturasInforme
    {
        public const string Escena = "Assets/Scenes/AtlasAnatomico.unity";
        const int Ancho = 1920, Alto = 1080;
        const float Fov = 40f;
        static string s_Carpeta;
        const int CapaCaptura = 31;   // capa libre: la cámara de capturas solo ve esta
        static readonly Color Fondo = new Color(0.20f, 0.22f, 0.26f);

        /// <summary>Carpeta de salida: variable ATLAS_CAPTURAS o la del Laboratorio 1 2P.</summary>
        public static string Carpeta
        {
            get
            {
                if (s_Carpeta != null) return s_Carpeta;
                var env = Environment.GetEnvironmentVariable("ATLAS_CAPTURAS");
                if (!string.IsNullOrEmpty(env)) return env;
                return Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Documentartion/Laboratorio 1 2P/Img/capturas"));
            }
        }

        public static IEnumerator Capturar()
        {
            Directory.CreateDirectory(Carpeta);
            // El órgano se coloca a los 0,5 s y el menú a los 0,7 s; luego se suavizan.
            yield return new WaitForSeconds(2.5f);

            var organo = UnityEngine.Object.FindAnyObjectByType<OrganoController>();
            var menu = UnityEngine.Object.FindAnyObjectByType<MenuAtlas>();
            if (organo == null || menu == null)
                throw new Exception("[Atlas] La escena no tiene OrganoController o MenuAtlas.");

            yield return Foto("01-vista-completa", organo, menu, true);

            organo.CambiarModo(ModoVista.Desglosada);
            yield return Recolocar(organo, menu);
            yield return Foto("02-vista-desglosada", organo, menu, true);
            yield return Foto("03-detalle-desglosado", organo, menu, false);

            organo.Seleccionar(IndiceDe(organo, "Cristalino"));
            organo.CambiarModo(ModoVista.Informativa);
            yield return Recolocar(organo, menu);
            yield return Foto("04-vista-informativa", organo, menu, true);
            yield return FotoPanel("04b-panel-informativo", organo, menu);

            organo.Seleccionar(-1);
            organo.CambiarModo(ModoVista.Desglosada);
            organo.AlternarColorTunicas();
            yield return Recolocar(organo, menu);
            yield return Foto("05-tunicas", organo, menu, true);

            organo.AlternarColorTunicas();
            organo.Seleccionar(IndiceDe(organo, "Retina"));
            organo.CambiarModo(ModoVista.Informativa);
            var escalarTexto = typeof(MenuAtlas).GetMethod("EscalarTexto", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 3; i++) escalarTexto?.Invoke(menu, new object[] { 1.15f });
            yield return Recolocar(organo, menu);
            yield return FotoPanel("06-texto-ampliado", organo, menu);

            Debug.Log("[Atlas] Capturas guardadas en " + Carpeta);
        }

        /// <summary>
        /// Recorre los pulmones como lo haría un usuario, comprueba que la respiración y el aire
        /// funcionan y guarda las capturas en Modelado/pulmones/previews/unity.
        /// </summary>
        public static IEnumerator CapturarPulmones()
        {
            s_Carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Modelado/pulmones/previews/unity"));
            Directory.CreateDirectory(Carpeta);
            yield return new WaitForSeconds(2.5f);
            var menu = UnityEngine.Object.FindAnyObjectByType<MenuAtlas>();
            Exigir(menu != null && menu.organos.Count == 2, "El menú no tiene los dos órganos.");
            var ojo = menu.organos[0];
            var organo = menu.organos[1];
            menu.CambiarOrgano(organo);
            yield return new WaitForSeconds(2.5f);
            Exigir(!ojo.gameObject.activeSelf && organo.gameObject.activeSelf, "No se cambió de órgano.");
            Exigir(organo.Partes.Count == 8, $"Se esperaban 8 piezas y hay {organo.Partes.Count}.");

            var resp = organo.GetComponent<Respiracion>();
            Exigir(resp.MallasAnimadas == 7, $"Se esperaban 7 piezas que respiran y hay {resp.MallasAnimadas}.");
            Exigir(organo.GetComponent<FlujoAire>().Recorridos > 50, "Faltan los recorridos del aire.");

            // La respiración se mueve sola y el diafragma baja al inhalar.
            float min = 1f, max = 0f;
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                min = Mathf.Min(min, resp.Aire);
                max = Mathf.Max(max, resp.Aire);
                yield return null;
            }
            Exigir(max - min > 0.5f, $"La respiración no se anima ({min:F2}-{max:F2}).");
            int diafragma = IndiceDe(organo, "Diafragma");
            resp.enabled = false;
            resp.Aplicar(0f);
            float arriba = MasAlto(organo, diafragma);
            yield return Foto("01-completa-espirado", organo, menu, true);
            resp.Aplicar(1f);
            float abajo = MasAlto(organo, diafragma);
            yield return Foto("02-completa-inspirado", organo, menu, false);
            Debug.Log($"[Atlas] El diafragma baja {(arriba - abajo) * 1000f:F1} mm.");
            Exigir(arriba - abajo > 0.003f, $"El diafragma no baja al inhalar ({arriba - abajo:F4} m).");
            resp.enabled = true;

            organo.AlternarPorDentro();
            for (float t = 0f; t < 6f && resp.Caudal < 0.4f; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.45f);
            yield return Foto("03-por-dentro-inhala", organo, menu, false);
            int azules = Contar("03-por-dentro-inhala", c => c.b > c.r + 0.2f && c.b > 0.6f);
            for (float t = 0f; t < 6f && resp.Caudal > -0.3f; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            yield return Foto("04-por-dentro-exhala", organo, menu, false);
            // El aire tiene que verse: azul sobre la tráquea al inhalar, y no al exhalar.
            int azulesAlExhalar = Contar("04-por-dentro-exhala", c => c.b > c.r + 0.2f && c.b > 0.6f);
            Debug.Log($"[Atlas] Píxeles azules: {azules} al inhalar, {azulesAlExhalar} al exhalar.");
            Exigir(azules > azulesAlExhalar + 600, $"No se ve el flujo de aire ({azules} frente a {azulesAlExhalar} píxeles).");
            var flujo = organo.GetComponentInChildren<ParticleSystem>();
            var particulas = new ParticleSystem.Particle[1000];
            int n = flujo.GetParticles(particulas), dentro = 0;
            var caja = organo.LimitesDe(0);
            for (int i = 1; i < organo.Partes.Count; i++) caja.Encapsulate(organo.LimitesDe(i));
            caja.Expand(0.01f);
            for (int i = 0; i < n; i++)
                if (caja.Contains(flujo.transform.TransformPoint(particulas[i].position))) dentro++;
            Exigir(n > 100 && dentro == n, $"Partículas fuera de los pulmones: {n - dentro} de {n}.");
            organo.AlternarPorDentro();

            organo.CambiarModo(ModoVista.Desglosada);
            yield return Recolocar(organo, menu);
            yield return Foto("05-desglosada", organo, menu, true);

            organo.CambiarModo(ModoVista.Informativa);
            organo.Seleccionar(IndiceDe(organo, "Pulmon_Izq_Lobulo_Sup"));
            yield return Recolocar(organo, menu);
            yield return Foto("06-informativa", organo, menu, true);
            yield return FotoPanel("06b-panel", organo, menu);

            organo.Seleccionar(-1);
            organo.CambiarModo(ModoVista.Desglosada);
            organo.AlternarColorTunicas();
            yield return Recolocar(organo, menu);
            yield return Foto("07-grupos", organo, menu, true);

            // Volver al ojo: debe seguir funcionando.
            menu.CambiarOrgano(ojo);
            yield return new WaitForSeconds(2.5f);
            Exigir(ojo.gameObject.activeSelf && !organo.gameObject.activeSelf, "No se volvió al ojo.");
            yield return Foto("08-vuelta-al-ojo", ojo, menu, true);
            s_Carpeta = null;
        }

        /// <summary>Punto más alto de una pieza ya deformada por sus blend shapes.</summary>
        static float MasAlto(OrganoController organo, int indice)
        {
            var id = organo.prefijo + organo.Partes[indice].id;
            foreach (var malla in organo.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (malla.name != id) continue;
                var m = new Mesh();
                malla.BakeMesh(m, true);
                float alto = float.MinValue;
                foreach (var v in m.vertices) alto = Mathf.Max(alto, malla.transform.TransformPoint(v).y);
                return alto;
            }
            throw new Exception("[Atlas] No es una malla con blend shapes: " + id);
        }

        static int Contar(string nombre, Func<Color, bool> cumple)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(Carpeta, nombre + ".png")));
            int n = 0;
            foreach (var c in tex.GetPixels()) if (cumple(c)) n++;
            UnityEngine.Object.Destroy(tex);
            return n;
        }

        static void Exigir(bool condicion, string mensaje)
        {
            if (!condicion) throw new Exception("[Atlas] " + mensaje);
        }

        static int IndiceDe(OrganoController organo, string id)
        {
            var partes = organo.Partes;
            for (int i = 0; i < partes.Count; i++)
                if (partes[i].id == id) return i;
            return -1;
        }

        /// <summary>Lo mismo que pulsar "Traer aquí": recoloca órgano y menú según la vista.</summary>
        static IEnumerator Recolocar(OrganoController organo, MenuAtlas menu)
        {
            organo.TraerFrenteAlUsuario();
            yield return new WaitForSeconds(0.3f);
            menu.Colocar();
            yield return new WaitForSeconds(2f);
        }

        /// <summary>Órgano y (opcionalmente) panel, vistos desde la cabeza del usuario.</summary>
        static IEnumerator Foto(string nombre, OrganoController organo, MenuAtlas menu, bool conMenu)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            var caja = new Bounds(organo.transform.position, Vector3.zero);
            foreach (var r in organo.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer)) caja.Encapsulate(r.bounds);
            if (conMenu)
                foreach (var e in EsquinasPanel(menu)) caja.Encapsulate(e);

            var cabeza = Camera.main != null ? Camera.main.transform.position : caja.center - Vector3.forward;
            var direccion = (caja.center - cabeza).normalized;

            float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float aspecto = (float)Ancho / Alto;
            float distancia = Mathf.Max(caja.extents.y / tanV, Mathf.Max(caja.extents.x, caja.extents.z) / (tanV * aspecto)) * 1.12f
                              + caja.extents.magnitude * 0.5f;
            Renderizar(nombre, organo, conMenu ? menu : null, caja.center - direccion * distancia, direccion, Ancho, Alto);
        }

        /// <summary>Primer plano frontal del panel, para que su texto se lea en el informe.</summary>
        static IEnumerator FotoPanel(string nombre, OrganoController organo, MenuAtlas menu)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            var esquinas = EsquinasPanel(menu);   // inferior izq., superior izq., superior der., inferior der.
            var centro = (esquinas[0] + esquinas[2]) * 0.5f;
            float ancho = Vector3.Distance(esquinas[0], esquinas[3]);
            float alto = Vector3.Distance(esquinas[0], esquinas[1]);
            var normal = menu.transform.forward;  // el lado visible del Canvas mira hacia -forward

            int px = 1200, py = Mathf.RoundToInt(px * alto / ancho);
            float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float distancia = alto * 0.5f / tanV * 1.06f;
            Renderizar(nombre, organo, menu, centro - normal * distancia, normal, px, py);
        }

        static Vector3[] EsquinasPanel(MenuAtlas menu)
        {
            var esquinas = new Vector3[4];
            foreach (var rt in menu.GetComponentsInChildren<RectTransform>())
                if (rt.name == "Panel") { rt.GetWorldCorners(esquinas); break; }
            return esquinas;
        }

        /// <summary>
        /// Renderiza solo el órgano y el menú (en una capa aparte) para que no salgan
        /// las manos o controles del simulador XR, y guarda el PNG. Sin menú (null), solo el órgano.
        /// </summary>
        static void Renderizar(string nombre, OrganoController organo, MenuAtlas menu,
                               Vector3 posicion, Vector3 direccion, int ancho, int alto)
        {
            var capas = new System.Collections.Generic.Dictionary<GameObject, int>();
            foreach (var t in organo.GetComponentsInChildren<Transform>(true)) capas[t.gameObject] = t.gameObject.layer;
            if (menu != null)
                foreach (var t in menu.GetComponentsInChildren<Transform>(true)) capas[t.gameObject] = t.gameObject.layer;
            // Las etiquetas y la ventana de la pieza viven fuera de la jerarquía del órgano.
            foreach (var c in UnityEngine.Object.FindObjectsByType<AmpliableConRueda>(FindObjectsSortMode.None))
                if (c.GetComponent<MenuAtlas>() == null)
                    foreach (var t in c.GetComponentsInChildren<Transform>(true)) capas[t.gameObject] = t.gameObject.layer;
            foreach (var g in capas.Keys) g.layer = CapaCaptura;

            var go = new GameObject("Camara capturas");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 0.01f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Fondo;
            cam.cullingMask = 1 << CapaCaptura;
            cam.aspect = (float)ancho / alto;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            go.transform.SetPositionAndRotation(posicion, Quaternion.LookRotation(direccion, Vector3.up));

            var rtSalida = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rtSalida;
            var pedido = new UniversalRenderPipeline.SingleCameraRequest { destination = rtSalida };
            if (RenderPipeline.SupportsRenderRequest(cam, pedido)) RenderPipeline.SubmitRenderRequest(cam, pedido);
            else cam.Render();

            var anterior = RenderTexture.active;
            RenderTexture.active = rtSalida;
            var tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            tex.Apply();
            RenderTexture.active = anterior;
            File.WriteAllBytes(Path.Combine(Carpeta, nombre + ".png"), tex.EncodeToPNG());

            cam.targetTexture = null;
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(go);
            rtSalida.Release();
            foreach (var kv in capas) kv.Key.layer = kv.Value;
        }
    }
}
