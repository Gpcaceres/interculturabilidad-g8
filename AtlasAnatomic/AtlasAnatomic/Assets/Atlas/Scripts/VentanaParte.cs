using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AtlasAnatomico
{
    /// <summary>
    /// Ventana flotante con la descripción de la pieza seleccionada. Aparece debajo de la pieza
    /// (las etiquetas van encima), la sigue y mira al usuario. Se cierra con su botón X y vuelve
    /// a abrirse al seleccionar otra pieza. No se muestra en la vista completa.
    /// Se instala sola junto al OrganoController.
    /// </summary>
    public class VentanaParte : MonoBehaviour
    {
        [Tooltip("Metros por píxel de la ventana.")]
        public float escala = 0.0005f;
        public float anchoPx = 440f;
        [Tooltip("Distancia bajo la pieza, en metros.")]
        public float separacion = 0.03f;

        static readonly Color cFondo = new Color(0.07f, 0.09f, 0.13f, 0.95f);
        static readonly Color cBorde = new Color(0.13f, 0.47f, 0.85f, 1f);
        static readonly Color cTexto = new Color(0.94f, 0.95f, 0.97f, 1f);
        static readonly Color cTenue = new Color(0.65f, 0.70f, 0.78f, 1f);
        static readonly Color cDato = new Color(0.55f, 0.82f, 1f, 1f);

        OrganoController m_Organo;
        RectTransform m_Raiz;
        TMP_Text m_Nombre, m_Kichwa, m_Tunica, m_Descripcion, m_Dato;
        bool m_Cerrada;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            foreach (var organo in FindObjectsByType<OrganoController>(FindObjectsSortMode.None))
                if (organo.GetComponent<VentanaParte>() == null)
                    organo.gameObject.AddComponent<VentanaParte>();
        }

        void Start()
        {
            m_Organo = GetComponent<OrganoController>();
            Construir();
            m_Organo.ParteSeleccionada += _ => { m_Cerrada = false; Rellenar(); };
            m_Raiz.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            int i = m_Organo.Seleccion;
            bool mostrar = !m_Cerrada && cam != null && i >= 0 && i < m_Organo.Partes.Count
                           && m_Organo.Modo != ModoVista.Completa;
            if (m_Raiz.gameObject.activeSelf != mostrar)
            {
                m_Raiz.gameObject.SetActive(mostrar);
                if (mostrar) Rellenar();
            }
            if (!mostrar) return;

            var caja = m_Organo.LimitesDe(i);
            var pos = caja.center - Vector3.up * (caja.extents.y + separacion);
            m_Raiz.position = pos;
            var mirar = pos - cam.transform.position;
            mirar.y = 0f;
            if (mirar.sqrMagnitude > 1e-6f) m_Raiz.rotation = Quaternion.LookRotation(mirar, Vector3.up);
        }

        void OnDestroy()
        {
            if (m_Raiz != null) Destroy(m_Raiz.gameObject);
        }

        void Rellenar()
        {
            int i = m_Organo.Seleccion;
            if (i < 0 || i >= m_Organo.Partes.Count) return;
            var p = m_Organo.Partes[i];
            bool sinKichwa = string.IsNullOrWhiteSpace(p.kichwa) || p.kichwa.Trim().ToLower() == "pendiente";
            m_Nombre.text = p.nombre;
            m_Kichwa.text = sinKichwa ? "Kichwa: término por validar" : $"Kichwa: {p.kichwa}";
            m_Tunica.text = $"Túnica: {p.tunica}";
            m_Descripcion.text = p.descripcion;
            m_Dato.text = string.IsNullOrEmpty(p.dato) ? "" : $"Dato: {p.dato}";
            m_Dato.gameObject.SetActive(!string.IsNullOrEmpty(p.dato));
        }

        // ------------------------------------------------------------------ Construcción

        void Construir()
        {
            // Fuera de la jerarquía del órgano: así no gira ni escala con él.
            var go = new GameObject("Ventana de la pieza", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            m_Raiz = (RectTransform)go.transform;
            m_Raiz.localScale = Vector3.one * escala;
            m_Raiz.pivot = new Vector2(0.5f, 1f);   // cuelga bajo la pieza
            go.AddComponent<AmpliableConRueda>().maximo = 4f;

            // Borde azul y fondo oscuro.
            go.AddComponent<Image>().color = cBorde;
            var borde = go.AddComponent<VerticalLayoutGroup>();
            borde.padding = new RectOffset(3, 3, 3, 3);
            borde.childControlWidth = borde.childControlHeight = true;
            var ajuste = go.AddComponent<ContentSizeFitter>();
            ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_Raiz.sizeDelta = new Vector2(anchoPx, 0f);

            var cuerpo = new GameObject("Cuerpo", typeof(RectTransform), typeof(Image));
            cuerpo.transform.SetParent(go.transform, false);
            cuerpo.GetComponent<Image>().color = cFondo;
            var vl = cuerpo.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(18, 18, 12, 16);
            vl.spacing = 6;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandHeight = false;

            // Encabezado: nombre y botón de cerrar.
            var encabezado = new GameObject("Encabezado", typeof(RectTransform));
            encabezado.transform.SetParent(cuerpo.transform, false);
            var hl = encabezado.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childAlignment = TextAnchor.MiddleLeft;
            m_Nombre = Texto(encabezado.transform, 30, FontStyles.Bold, cTexto);
            m_Nombre.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Cerrar(encabezado.transform);

            m_Kichwa = Texto(cuerpo.transform, 19, FontStyles.Italic, cTenue);
            m_Tunica = Texto(cuerpo.transform, 19, FontStyles.Normal, cTenue);
            m_Descripcion = Texto(cuerpo.transform, 22, FontStyles.Normal, cTexto);
            m_Dato = Texto(cuerpo.transform, 20, FontStyles.Normal, cDato);
        }

        void Cerrar(Transform padre)
        {
            var go = new GameObject("Cerrar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            go.GetComponent<Image>().color = new Color(0.20f, 0.24f, 0.31f, 1f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 44f;
            go.AddComponent<Button>().onClick.AddListener(() => m_Cerrada = true);
            var t = Texto(go.transform, 26, FontStyles.Bold, cTexto);
            t.text = "X";
            t.alignment = TextAlignmentOptions.Center;
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static TMP_Text Texto(Transform padre, float tam, FontStyles estilo, Color color)
        {
            var go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = tam;
            t.fontStyle = estilo;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }
    }
}
