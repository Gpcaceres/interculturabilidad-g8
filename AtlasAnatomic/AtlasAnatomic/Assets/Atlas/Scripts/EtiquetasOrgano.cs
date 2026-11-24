using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AtlasAnatomico
{
    /// <summary>
    /// Etiquetas flotantes sobre cada pieza del órgano, unidas a ella por una línea.
    /// Se ven en las vistas desglosada e informativa cuando las capas están separadas;
    /// se alternan en dos alturas para no encimarse y siempre miran al usuario.
    /// Se instala sola junto al OrganoController y se activa o desactiva desde el menú.
    /// </summary>
    public class EtiquetasOrgano : MonoBehaviour
    {
        [Tooltip("Metros por píxel de cada etiqueta.")]
        public float escala = 0.0005f;
        [Tooltip("Altura sobre la pieza, en metros; las etiquetas impares suben una fila más.")]
        public float altura = 0.04f;
        public float alturaFila = 0.035f;
        [Tooltip("Separación mínima de capas para mostrarlas.")]
        public float separacionMinima = 0.3f;

        static readonly Color cFondo = new Color(0.07f, 0.09f, 0.13f, 0.9f);
        static readonly Color cActivo = new Color(0.13f, 0.47f, 0.85f, 0.95f);
        static readonly Color cLinea = new Color(0.85f, 0.88f, 0.95f, 0.8f);

        public bool Visibles { get; private set; } = true;

        class Etiqueta
        {
            public Transform raiz;
            public Image fondo;
            public LineRenderer linea;
        }

        OrganoController m_Organo;
        readonly List<Etiqueta> m_Etiquetas = new List<Etiqueta>();
        Material m_MaterialLinea;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            foreach (var organo in FindObjectsByType<OrganoController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (organo.GetComponent<EtiquetasOrgano>() == null)
                    organo.gameObject.AddComponent<EtiquetasOrgano>();
        }

        void Start()
        {
            m_Organo = GetComponent<OrganoController>();
            m_MaterialLinea = new Material(Shader.Find("Sprites/Default"));
            var partes = m_Organo.Partes;
            for (int i = 0; i < partes.Count; i++)
                m_Etiquetas.Add(Crear(partes[i]));
        }

        public void Alternar() => Visibles = !Visibles;

        void LateUpdate()
        {
            var cam = Camera.main;
            bool mostrar = Visibles && cam != null && m_Organo.Modo != ModoVista.Completa
                           && m_Organo.Separacion >= separacionMinima;

            for (int i = 0; i < m_Etiquetas.Count; i++)
            {
                var e = m_Etiquetas[i];
                e.raiz.gameObject.SetActive(mostrar);
                e.linea.enabled = mostrar;
                if (!mostrar) continue;

                var caja = m_Organo.LimitesDe(i);
                var tope = caja.center + Vector3.up * caja.extents.y;
                var pos = tope + Vector3.up * (m_Organo.desgloseRadial ? altura * 0.35f : altura + (i % 2) * alturaFila);
                // En el desglose radial, un poco hacia el usuario: que no la tape la pieza vecina.
                if (m_Organo.desgloseRadial)
                    pos += (cam.transform.position - pos).normalized * 0.10f;
                e.raiz.position = pos;
                var mirar = pos - cam.transform.position;
                mirar.y = 0f;
                if (mirar.sqrMagnitude > 1e-6f) e.raiz.rotation = Quaternion.LookRotation(mirar, Vector3.up);

                e.linea.SetPosition(0, tope);
                e.linea.SetPosition(1, pos - Vector3.up * 0.008f);
                e.fondo.color = i == m_Organo.Seleccion ? cActivo : cFondo;
            }
        }

        void OnDisable()   // al cambiar de órgano
        {
            foreach (var e in m_Etiquetas)
                if (e.raiz != null) e.raiz.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            foreach (var e in m_Etiquetas)
                if (e.raiz != null) Destroy(e.raiz.gameObject);
            if (m_MaterialLinea != null) Destroy(m_MaterialLinea);
        }

        Etiqueta Crear(ParteAnatomica parte)
        {
            // Fuera de la jerarquía del órgano: así no gira ni escala con él.
            var go = new GameObject("Etiqueta " + parte.nombre, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one * escala;
            rt.pivot = new Vector2(0.5f, 0f);
            go.AddComponent<AmpliableConRueda>();

            var fondo = go.AddComponent<Image>();
            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(14, 14, 6, 6);
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            var ajuste = go.AddComponent<ContentSizeFitter>();
            ajuste.horizontalFit = ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Texto(go.transform, parte.nombre, 28, FontStyles.Bold);
            bool conKichwa = !string.IsNullOrWhiteSpace(parte.kichwa) && parte.kichwa.Trim().ToLower() != "pendiente";
            if (conKichwa) Texto(go.transform, parte.kichwa, 22, FontStyles.Italic);

            var linea = new GameObject("Línea").AddComponent<LineRenderer>();
            linea.transform.SetParent(go.transform, false);
            linea.useWorldSpace = true;
            linea.positionCount = 2;
            linea.widthMultiplier = 0.0015f;
            linea.material = m_MaterialLinea;
            linea.startColor = linea.endColor = cLinea;
            linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return new Etiqueta { raiz = go.transform, fondo = fondo, linea = linea };
        }

        static void Texto(Transform padre, string texto, float tam, FontStyles estilo)
        {
            var go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = texto;
            t.fontSize = tam;
            t.fontStyle = estilo;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
        }
    }
}
