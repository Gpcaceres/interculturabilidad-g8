using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AtlasAnatomico
{
    /// <summary>
    /// Modo de escritorio: sin visor (en el editor o en PC) el atlas se maneja con el ratón.
    /// - Clic en los botones del panel.
    /// - Clic en una pieza del ojo para seleccionarla y abrir su ventana (en la vista completa
    ///   pasa a la informativa, como al pulsar el botón de la pieza en el panel).
    /// - Arrastrar sobre el órgano para girarlo; rueda sobre el órgano para cambiar su tamaño.
    /// - Rueda sobre una ventana (panel, ventana de la pieza, etiqueta) para agrandarla o reducirla.
    /// - Arrastrar la barra gris inferior del panel para moverlo.
    /// La cámara se mueve con los controles de la simulación XR (clic derecho + WASD).
    /// Se instala solo al cargar la escena; en el Quest (Android) no se activa.
    /// </summary>
    public class ModoEscritorio : MonoBehaviour
    {
        [Tooltip("Grados por píxel al arrastrar.")]
        public float sensibilidadGiro = 0.4f;
        const float UmbralArrastre = 6f;   // píxeles

        OrganoController m_Organo;
        Transform m_Pulsado;
        Vector2 m_Inicio, m_Anterior;
        bool m_Arrastrando;

        MenuAtlas m_Menu;          // panel que se está moviendo, si se agarró su barra
        Plane m_PlanoMenu;
        Vector3 m_DesfaseMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            if (Application.isMobilePlatform) return;   // en el visor se usan manos o controles
            var organo = FindAnyObjectByType<OrganoController>();
            if (organo != null && organo.GetComponent<ModoEscritorio>() == null)
                organo.gameObject.AddComponent<ModoEscritorio>();
        }

        void Start()
        {
            m_Organo = GetComponent<OrganoController>();
            // La plantilla MR desactiva el ratón en la UI porque espera manos o controles.
            foreach (var modulo in FindObjectsByType<XRUIInputModule>(FindObjectsSortMode.None))
                modulo.enableMouseInput = true;
        }

        void Update()
        {
            var raton = Mouse.current;
            var cam = Camera.main;
            if (raton == null || cam == null || m_Organo == null) return;
            var pos = raton.position.ReadValue();

            if (MoverMenu(raton, cam, pos)) return;

            if (raton.leftButton.wasPressedThisFrame)
            {
                m_Pulsado = RayoAlOrgano(cam, pos);
                m_Inicio = m_Anterior = pos;
                m_Arrastrando = false;
            }
            else if (raton.leftButton.isPressed && m_Pulsado != null)
            {
                if (!m_Arrastrando && (pos - m_Inicio).magnitude > UmbralArrastre) m_Arrastrando = true;
                if (m_Arrastrando) m_Organo.Girar((pos - m_Anterior) * sensibilidadGiro, cam.transform);
                m_Anterior = pos;
            }
            else if (raton.leftButton.wasReleasedThisFrame && m_Pulsado != null)
            {
                if (!m_Arrastrando)
                {
                    // Un clic sin arrastre selecciona la pieza: abre su ventana, igual que su botón del panel.
                    int indice = m_Organo.PiezaBajoRayo(cam.ScreenPointToRay(pos));
                    if (indice >= 0) m_Organo.Seleccionar(indice);
                }
                m_Pulsado = null;
            }

            float rueda = raton.scroll.ReadValue().y;
            if (Mathf.Abs(rueda) > 0.01f)
            {
                float factor = rueda > 0 ? 1.1f : 1f / 1.1f;
                var ventana = VentanaBajoCursor(cam, pos);
                if (ventana != null) ventana.Ampliar(factor);
                else if (RayoAlOrgano(cam, pos) != null) m_Organo.CambiarTamano(factor);
            }
        }

        /// <summary>La ventana ampliable más cercana bajo el cursor, si hay alguna.</summary>
        static AmpliableConRueda VentanaBajoCursor(Camera cam, Vector2 pos)
        {
            AmpliableConRueda mejor = null;
            float masCerca = float.MaxValue;
            foreach (var v in AmpliableConRueda.Activas)
            {
                if (!v.Contiene(pos, cam)) continue;
                float d = Vector3.Distance(cam.transform.position, v.zona.position);
                if (d < masCerca) { masCerca = d; mejor = v; }
            }
            return mejor;
        }

        /// <summary>
        /// Arrastre del panel por su barra (el único collider del menú). El panel se desliza
        /// en un plano frente a la cámara y sigue mirando al usuario. Devuelve true mientras se mueve.
        /// </summary>
        bool MoverMenu(Mouse raton, Camera cam, Vector2 pos)
        {
            var rayo = cam.ScreenPointToRay(pos);
            if (raton.leftButton.wasPressedThisFrame && Physics.Raycast(rayo, out var hit, 20f))
            {
                m_Menu = hit.transform.GetComponentInParent<MenuAtlas>();
                if (m_Menu != null)
                {
                    m_Menu.MovidoAMano = true;
                    m_PlanoMenu = new Plane(-cam.transform.forward, hit.point);
                    m_DesfaseMenu = m_Menu.transform.position - hit.point;
                    return true;
                }
            }
            if (m_Menu == null) return false;

            if (!raton.leftButton.isPressed)
            {
                m_Menu = null;
                return false;
            }
            if (m_PlanoMenu.Raycast(rayo, out float d))
            {
                var nueva = rayo.GetPoint(d) + m_DesfaseMenu;
                var mirar = nueva - cam.transform.position;
                mirar.y *= 0.3f;
                m_Menu.transform.SetPositionAndRotation(nueva, Quaternion.LookRotation(mirar, Vector3.up));
            }
            return true;
        }

        Transform RayoAlOrgano(Camera cam, Vector2 pos)
        {
            if (!Physics.Raycast(cam.ScreenPointToRay(pos), out var hit, 20f)) return null;
            // hit.transform sería el del Rigidbody (la raíz del órgano); interesa el collider tocado.
            var tocado = hit.collider.transform;
            return tocado == transform || tocado.IsChildOf(transform) ? tocado : null;
        }
    }
}
