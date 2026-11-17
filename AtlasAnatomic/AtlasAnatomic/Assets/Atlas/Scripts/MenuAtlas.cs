using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AtlasAnatomico
{
    /// <summary>
    /// Panel flotante del Atlas Anatómico (se construye por código al iniciar).
    /// Secciones: órgano, vista, separación y piezas, información y utilidades.
    /// Se usa con rayo o tocando con el dedo (poke); se puede mover agarrando la barra inferior.
    /// </summary>
    public class MenuAtlas : MonoBehaviour
    {
        public OrganoController organo;
        [Tooltip("Metros por píxel del panel.")]
        public float escalaPanel = 0.0006f;
        public float anchoPx = 560f;

        // Paleta
        static readonly Color cFondo = new Color(0.07f, 0.09f, 0.13f, 0.94f);
        static readonly Color cSeccion = new Color(0.12f, 0.15f, 0.20f, 1f);
        static readonly Color cBoton = new Color(0.20f, 0.24f, 0.31f, 1f);
        static readonly Color cActivo = new Color(0.13f, 0.47f, 0.85f, 1f);
        static readonly Color cDeshab = new Color(0.16f, 0.17f, 0.20f, 1f);
        static readonly Color cTexto = new Color(0.94f, 0.95f, 0.97f, 1f);
        static readonly Color cTenue = new Color(0.65f, 0.70f, 0.78f, 1f);

        RectTransform m_Panel;
        readonly List<(TMP_Text t, float tam)> m_Textos = new List<(TMP_Text, float)>();
        float m_EscalaTexto = 1f;

        readonly Dictionary<ModoVista, Image> m_BotonesModo = new Dictionary<ModoVista, Image>();
        readonly List<Image> m_BotonesParte = new List<Image>();
        GameObject m_GrupoDesglose, m_GrupoPartes, m_GrupoInfo;
        Slider m_Slider;
        TMP_Text m_Ayuda, m_InfoNombre, m_InfoKichwa, m_InfoTunica, m_InfoDescripcion, m_InfoDato, m_TextoTunicas;
        Image m_BotonTunicas;

        IEnumerator Start()
        {
            if (organo == null) organo = FindAnyObjectByType<OrganoController>();
            Construir();
            if (organo != null)
            {
                organo.ModoCambiado += _ => Refrescar();
                organo.ParteSeleccionada += _ => Refrescar();
            }
            Refrescar();
            yield return new WaitForSeconds(0.7f);
            Colocar();
        }

        /// <summary>Pone el panel junto al órgano, mirando al usuario.</summary>
        public void Colocar()
        {
            var cam = Camera.main;
            if (cam == null || organo == null) return;
            var pos = organo.PuntoParaMenu(cam.transform.position);
            transform.position = pos;
            var mirar = pos - cam.transform.position;
            mirar.y *= 0.3f;
            transform.rotation = Quaternion.LookRotation(mirar, Vector3.up);
        }

        // ------------------------------------------------------------------ Construcción

        void Construir()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(anchoPx, 100f);
            rt.localScale = Vector3.one * escalaPanel;

            m_Panel = Caja("Panel", rt, cFondo);
            var vl = Vertical(m_Panel, 14, new RectOffset(22, 22, 22, 18));
            var fitter = m_Panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_Panel.anchorMin = m_Panel.anchorMax = new Vector2(0.5f, 1f);
            m_Panel.pivot = new Vector2(0.5f, 1f);
            m_Panel.sizeDelta = new Vector2(anchoPx, 0f);
            m_Panel.anchoredPosition = new Vector2(0f, 300f);

            // Encabezado
            Texto(m_Panel, "Atlas Anatómico", 40, FontStyles.Bold, cTexto);
            Texto(m_Panel, "Laboratorio de anatomía en realidad aumentada", 20, FontStyles.Normal, cTenue);

            // Órgano
            Titulo(m_Panel, "Órgano");
            var filaOrg = Fila(m_Panel, 64);
            Boton(filaOrg, $"{organo?.nombreOrgano ?? "Ojo"}\n<size=70%>{organo?.kichwaOrgano ?? "Ñawi"}</size>", null, true, cActivo);
            Boton(filaOrg, "Pulmones\n<size=70%>Próximamente</size>", null, false);
            Boton(filaOrg, "Riñón\n<size=70%>Próximamente</size>", null, false);

            // Vista
            Titulo(m_Panel, "Vista");
            var filaVista = Fila(m_Panel, 60);
            foreach (ModoVista modo in Enum.GetValues(typeof(ModoVista)))
            {
                var m = modo;
                m_BotonesModo[m] = Boton(filaVista, NombreModo(m), () => organo?.CambiarModo(m));
            }
            m_Ayuda = Texto(m_Panel, "", 19, FontStyles.Italic, cTenue);

            // Separación
            m_GrupoDesglose = Seccion(m_Panel, out var desglose);
            Texto(desglose, "Separación de capas", 20, FontStyles.Bold, cTexto);
            m_Slider = Deslizador(desglose, v => organo?.FijarSeparacion(v));
            var filaNav = Fila(desglose, 52);
            Boton(filaNav, "◄ Pieza anterior", () => organo?.SeleccionarSiguiente(-1));
            Boton(filaNav, "Pieza siguiente ►", () => organo?.SeleccionarSiguiente(1));

            // Piezas
            m_GrupoPartes = Seccion(m_Panel, out var piezas);
            Texto(piezas, "Partes del ojo", 20, FontStyles.Bold, cTexto);
            var partes = organo != null ? organo.Partes : (IReadOnlyList<ParteAnatomica>)new List<ParteAnatomica>();
            RectTransform fila = null;
            for (int i = 0; i < partes.Count; i++)
            {
                if (i % 3 == 0) fila = Fila(piezas, 54);
                int indice = i;
                m_BotonesParte.Add(Boton(fila, partes[i].nombre, () => organo?.Seleccionar(indice), true, null, 19));
            }

            // Información
            m_GrupoInfo = Seccion(m_Panel, out var info);
            m_InfoNombre = Texto(info, "", 32, FontStyles.Bold, cTexto);
            m_InfoKichwa = Texto(info, "", 20, FontStyles.Italic, cTenue);
            m_InfoTunica = Texto(info, "", 20, FontStyles.Normal, cTenue);
            m_InfoDescripcion = Texto(info, "", 22, FontStyles.Normal, cTexto);
            m_InfoDato = Texto(info, "", 21, FontStyles.Normal, new Color(0.55f, 0.82f, 1f));
            var filaTun = Fila(info, 50);
            m_BotonTunicas = Boton(filaTun, "Colorear túnicas", () => { organo?.AlternarColorTunicas(); Refrescar(); });
            m_TextoTunicas = Texto(info, "<color=#408CF2>■</color> Fibrosa   <color=#D9333F>■</color> Vascular   <color=#FACC26>■</color> Nerviosa", 19, FontStyles.Normal, cTenue);

            // Utilidades
            Titulo(m_Panel, "Ajustes");
            var filaUtil = Fila(m_Panel, 54);
            Boton(filaUtil, "Traer aquí", () => { organo?.TraerFrenteAlUsuario(); StartCoroutine(ColocarLuego()); });
            Boton(filaUtil, "Reiniciar", () => { organo?.Reiniciar(); StartCoroutine(ColocarLuego()); });
            var filaTam = Fila(m_Panel, 54);
            Boton(filaTam, "Tamaño −", () => organo?.CambiarTamano(1f / 1.25f));
            Boton(filaTam, "Tamaño +", () => organo?.CambiarTamano(1.25f));
            Boton(filaTam, "A−", () => EscalarTexto(1f / 1.15f));
            Boton(filaTam, "A+", () => EscalarTexto(1.15f));

            // Barra para agarrar y mover el panel
            var asa = Caja("Asa (agarrar para mover)", m_Panel, new Color(0.55f, 0.60f, 0.70f, 0.9f));
            var le = asa.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 16; le.preferredWidth = 220; le.flexibleWidth = 0;
            vl.childAlignment = TextAnchor.UpperCenter;
            var colAsa = asa.gameObject.AddComponent<BoxCollider>();
            StartCoroutine(AjustarColliderAsa(asa, colAsa));

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // Al añadirse, el XRGrabInteractable toma los colliders hijos: solo existe el del asa.
            var agarre = gameObject.AddComponent<XRGrabInteractable>();
            agarre.movementType = XRBaseInteractable.MovementType.Instantaneous;
            agarre.throwOnDetach = false;
            agarre.useDynamicAttach = true;
        }

        IEnumerator AjustarColliderAsa(RectTransform asa, BoxCollider col)
        {
            yield return null;   // esperar al layout
            col.size = new Vector3(asa.rect.width, Mathf.Max(asa.rect.height, 40f), 30f);
        }

        IEnumerator ColocarLuego()
        {
            yield return new WaitForSeconds(0.4f);
            Colocar();
        }

        // ------------------------------------------------------------------ Estado

        void Refrescar()
        {
            if (organo == null) return;
            var modo = organo.Modo;
            foreach (var kv in m_BotonesModo)
                kv.Value.color = kv.Key == modo ? cActivo : cBoton;

            m_GrupoDesglose.SetActive(modo != ModoVista.Completa);
            m_GrupoPartes.SetActive(modo != ModoVista.Completa);
            m_GrupoInfo.SetActive(modo == ModoVista.Informativa);
            m_Slider.SetValueWithoutNotify(organo.Separacion);

            m_Ayuda.text = modo switch
            {
                ModoVista.Completa => "Agarra el ojo para girarlo; con dos manos cambias su tamaño.",
                ModoVista.Desglosada => "Mueve el deslizador para separar las capas. Apunta a una pieza para resaltarla.",
                _ => "Elige una pieza para ver su información. Las demás se vuelven transparentes.",
            };

            var partes = organo.Partes;
            for (int i = 0; i < m_BotonesParte.Count; i++)
                m_BotonesParte[i].color = i == organo.Seleccion ? cActivo : cBoton;

            m_BotonTunicas.color = organo.ColorTunicas ? cActivo : cBoton;
            m_TextoTunicas.gameObject.SetActive(organo.ColorTunicas);

            if (organo.Seleccion >= 0 && organo.Seleccion < partes.Count)
            {
                var p = partes[organo.Seleccion];
                m_InfoNombre.text = p.nombre;
                bool sinKichwa = string.IsNullOrWhiteSpace(p.kichwa) || p.kichwa.Trim().ToLower() == "pendiente";
                m_InfoKichwa.text = sinKichwa ? "Kichwa: término por validar" : $"Kichwa: {p.kichwa}";
                m_InfoTunica.text = $"Túnica: {p.tunica}";
                m_InfoDescripcion.text = p.descripcion;
                m_InfoDato.text = string.IsNullOrEmpty(p.dato) ? "" : $"Dato: {p.dato}";
            }
            else
            {
                m_InfoNombre.text = "Elige una pieza";
                m_InfoKichwa.text = m_InfoTunica.text = m_InfoDescripcion.text = m_InfoDato.text = "";
            }
        }

        void EscalarTexto(float factor)
        {
            m_EscalaTexto = Mathf.Clamp(m_EscalaTexto * factor, 0.75f, 1.8f);
            foreach (var (t, tam) in m_Textos) t.fontSize = tam * m_EscalaTexto;
        }

        static string NombreModo(ModoVista m) => m switch
        {
            ModoVista.Completa => "Completa",
            ModoVista.Desglosada => "Desglosada",
            _ => "Informativa",
        };

        // ------------------------------------------------------------------ Ayudas de UI

        static RectTransform Caja(string nombre, Transform padre, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        static VerticalLayoutGroup Vertical(RectTransform rt, float espacio, RectOffset relleno)
        {
            var vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = espacio;
            vl.padding = relleno;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            return vl;
        }

        GameObject Seccion(RectTransform padre, out RectTransform contenido)
        {
            contenido = Caja("Sección", padre, cSeccion);
            Vertical(contenido, 10, new RectOffset(14, 14, 12, 14));
            return contenido.gameObject;
        }

        RectTransform Fila(RectTransform padre, float alto)
        {
            var go = new GameObject("Fila", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            go.AddComponent<LayoutElement>().preferredHeight = alto;
            return (RectTransform)go.transform;
        }

        void Titulo(RectTransform padre, string texto) =>
            Texto(padre, texto.ToUpperInvariant(), 18, FontStyles.Bold, new Color(0.45f, 0.70f, 1f));

        TMP_Text Texto(Transform padre, string texto, float tam, FontStyles estilo, Color color)
        {
            var go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = texto;
            t.fontSize = tam;
            t.fontStyle = estilo;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            m_Textos.Add((t, tam));
            return t;
        }

        Image Boton(Transform padre, string texto, Action alPulsar, bool habilitado = true, Color? color = null, float tam = 21)
        {
            var rt = Caja("Botón " + texto.Split('\n')[0], padre, habilitado ? (color ?? cBoton) : cDeshab);
            var img = rt.GetComponent<Image>();
            var b = rt.gameObject.AddComponent<Button>();
            b.interactable = habilitado;
            var colores = b.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colores.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colores.disabledColor = new Color(1f, 1f, 1f, 0.6f);
            b.colors = colores;
            if (alPulsar != null) b.onClick.AddListener(() => alPulsar());
            var t = Texto(rt, texto, tam, FontStyles.Bold, habilitado ? cTexto : cTenue);
            t.alignment = TextAlignmentOptions.Center;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 2); trt.offsetMax = new Vector2(-6, -2);
            return img;
        }

        Slider Deslizador(RectTransform padre, Action<float> alCambiar)
        {
            var raiz = new GameObject("Deslizador", typeof(RectTransform));
            raiz.transform.SetParent(padre, false);
            raiz.AddComponent<LayoutElement>().preferredHeight = 44;
            var slider = raiz.AddComponent<Slider>();

            var fondo = Caja("Fondo", raiz.transform, cBoton);
            Estirar(fondo, new Vector2(0, 0.35f), new Vector2(1, 0.65f));
            var area = new GameObject("Relleno", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(raiz.transform, false);
            Estirar(area, new Vector2(0, 0.35f), new Vector2(1, 0.65f));
            var relleno = Caja("Barra", area, cActivo);
            Estirar(relleno, Vector2.zero, Vector2.one);
            var areaAsa = new GameObject("Area asa", typeof(RectTransform)).GetComponent<RectTransform>();
            areaAsa.SetParent(raiz.transform, false);
            Estirar(areaAsa, Vector2.zero, Vector2.one);
            areaAsa.offsetMin = new Vector2(16, 0); areaAsa.offsetMax = new Vector2(-16, 0);
            var asa = Caja("Asa", areaAsa, Color.white);
            asa.sizeDelta = new Vector2(32, 0);

            slider.fillRect = relleno;
            slider.handleRect = asa;
            slider.targetGraphic = asa.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.onValueChanged.AddListener(v => alCambiar(v));
            return slider;
        }

        static void Estirar(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
