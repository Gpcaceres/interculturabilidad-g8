using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AtlasAnatomico
{
    public enum ModoVista { Completa, Desglosada, Informativa }

    /// <summary>
    /// Controla un órgano del atlas: las tres vistas (completa, desglosada, informativa),
    /// la separación de piezas, la selección y el resaltado.
    ///
    /// No depende de las unidades ni de los ejes con que se importó el FBX:
    /// mide la pieza de referencia (esclerótica) para fijar el tamaño y deduce el eje
    /// de desglose a partir de la posición de la primera pieza (córnea) respecto a la central.
    /// </summary>
    public class OrganoController : MonoBehaviour
    {
        [Header("Órgano")]
        public string nombreOrgano = DatosOjo.Nombre;
        public string kichwaOrgano = DatosOjo.Kichwa;
        [Tooltip("Prefijo de los objetos del modelo, p. ej. OJO_")]
        public string prefijo = DatosOjo.Prefijo;
        [Tooltip("Pieza que define el tamaño (la envoltura externa).")]
        public string piezaReferencia = "Esclerotica";
        [Tooltip("Pieza que queda en el centro al desglosar.")]
        public string piezaCentral = "Humor_Vitreo";
        [Tooltip("Pieza cuyo lado marca el frente del órgano.")]
        public string piezaFrontal = "Cornea";
        public List<ParteAnatomica> partes = new List<ParteAnatomica>();

        [Header("Presentación en RA")]
        [Tooltip("Tamaño en metros con el que se muestra (el ojo real mide 0,024 m).")]
        public float tamanoMostrado = 0.20f;
        [Tooltip("Separación entre capas en la vista desglosada, relativa al tamaño.")]
        public float separacionRelativa = 0.45f;
        public float distanciaAlUsuario = 0.6f;
        public float suavizado = 6f;

        [Header("Materiales")]
        [Tooltip("Material transparente para atenuar las piezas no seleccionadas.")]
        public Material materialFantasma;
        [Tooltip("Collider esférico para agarrar el órgano completo.")]
        public Collider colliderAgarre;

        public static readonly Dictionary<string, Color> ColoresTunica = new Dictionary<string, Color>
        {
            { "Fibrosa", new Color(0.25f, 0.55f, 0.95f) },
            { "Vascular", new Color(0.85f, 0.20f, 0.25f) },
            { "Nerviosa", new Color(0.98f, 0.80f, 0.15f) },
        };

        public event Action<ModoVista> ModoCambiado;
        public event Action<int> ParteSeleccionada;

        public ModoVista Modo { get; private set; } = ModoVista.Completa;
        public int Seleccion { get; private set; } = -1;
        public bool ColorTunicas { get; private set; }
        public float Separacion => m_SeparacionObjetivo;

        class Pieza
        {
            public ParteAnatomica datos;
            public Transform t;
            public Renderer[] renderers;
            public Material[][] originales;
            public Material resaltado;
            public Material tunica;
            public Collider[] colliders;
            public Vector3 posInicial;   // en el espacio local de este objeto
            public Vector3 escalaInicial;
        }

        readonly List<Pieza> m_Piezas = new List<Pieza>();
        Vector3 m_EjeFrontal = Vector3.forward;  // espacio local
        float m_Paso;
        float m_EscalaBase = 1f;
        float m_Separacion, m_SeparacionObjetivo;
        Quaternion? m_RotacionObjetivo;
        Vector3? m_PosicionObjetivo;
        bool m_Listo;

        void Awake()
        {
            transform.localScale = Vector3.one;
            foreach (var p in partes)
            {
                var t = BuscarHijo(transform, prefijo + p.id);
                if (t == null)
                {
                    Debug.LogWarning($"[Atlas] No se encontró la pieza {prefijo}{p.id} en {name}");
                    continue;
                }
                var pieza = new Pieza
                {
                    datos = p,
                    t = t,
                    renderers = t.GetComponentsInChildren<Renderer>(),
                    colliders = t.GetComponentsInChildren<Collider>(),
                    posInicial = transform.InverseTransformPoint(t.position),
                    escalaInicial = t.localScale,
                };
                pieza.originales = Array.ConvertAll(pieza.renderers, r => r.sharedMaterials);
                if (pieza.renderers.Length > 0)
                {
                    var baseMat = pieza.renderers[0].sharedMaterial;
                    pieza.resaltado = Clonar(baseMat, c => Color.Lerp(c, Color.white, 0.35f), 1f);
                    var colorTunica = ColoresTunica.TryGetValue(p.tunica ?? "", out var ct) ? ct : (Color?)null;
                    pieza.tunica = colorTunica.HasValue ? Clonar(baseMat, _ => colorTunica.Value, 1f) : null;
                }
                var interactuable = t.GetComponent<XRSimpleInteractable>();
                if (interactuable != null)
                {
                    int indice = m_Piezas.Count;
                    interactuable.selectEntered.AddListener(_ => Seleccionar(indice));
                }
                m_Piezas.Add(pieza);
            }

            // Tamaño: escala para que la pieza de referencia mida tamanoMostrado.
            var referencia = m_Piezas.Find(x => x.datos.id == piezaReferencia) ?? (m_Piezas.Count > 0 ? m_Piezas[0] : null);
            float tamanoModelo = 1f;
            if (referencia != null && referencia.renderers.Length > 0)
            {
                var b = referencia.renderers[0].bounds;
                tamanoModelo = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) / Mathf.Max(transform.lossyScale.x, 1e-6f);
            }
            m_Paso = tamanoModelo * separacionRelativa;

            // Eje frontal: de la pieza central hacia la frontal.
            var central = m_Piezas.Find(x => x.datos.id == piezaCentral);
            var frontal = m_Piezas.Find(x => x.datos.id == piezaFrontal);
            if (central != null && frontal != null)
            {
                var d = frontal.posInicial - central.posInicial;
                if (d.sqrMagnitude > 1e-10f) m_EjeFrontal = d.normalized;
            }

            if (tamanoModelo > 0f) m_EscalaBase = tamanoMostrado / tamanoModelo;
            transform.localScale = Vector3.one * m_EscalaBase;

            // Si el usuario agarra el órgano, deja de moverlo automáticamente.
            var agarre = GetComponent<XRGrabInteractable>();
            if (agarre != null)
                agarre.selectEntered.AddListener(_ => { m_PosicionObjetivo = null; m_RotacionObjetivo = null; });

            AjustarColliderAgarre(referencia);
            m_Listo = true;
            AplicarModo(ModoVista.Completa, false);
        }

        IEnumerator Start()
        {
            // Esperar a que el seguimiento de la cabeza se estabilice antes de colocar.
            yield return new WaitForSeconds(0.5f);
            TraerFrenteAlUsuario();
        }

        void Update()
        {
            if (!m_Listo) return;
            float k = 1f - Mathf.Exp(-suavizado * Time.deltaTime);

            if (!Mathf.Approximately(m_Separacion, m_SeparacionObjetivo))
            {
                m_Separacion = Mathf.Abs(m_Separacion - m_SeparacionObjetivo) < 0.001f
                    ? m_SeparacionObjetivo : Mathf.Lerp(m_Separacion, m_SeparacionObjetivo, k);
                foreach (var p in m_Piezas)
                {
                    var desplazamiento = -p.datos.paso * m_Paso * m_Separacion * m_EjeFrontal;
                    p.t.position = transform.TransformPoint(p.posInicial + desplazamiento);
                }
            }

            if (m_RotacionObjetivo.HasValue)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, m_RotacionObjetivo.Value, k);
                if (Quaternion.Angle(transform.rotation, m_RotacionObjetivo.Value) < 0.5f)
                    m_RotacionObjetivo = null;
            }
            if (m_PosicionObjetivo.HasValue)
            {
                transform.position = Vector3.Lerp(transform.position, m_PosicionObjetivo.Value, k);
                if (Vector3.Distance(transform.position, m_PosicionObjetivo.Value) < 0.002f)
                    m_PosicionObjetivo = null;
            }

            if (Seleccion >= 0 && Modo != ModoVista.Completa)
            {
                // Pulso suave en la pieza seleccionada.
                var p = m_Piezas[Seleccion];
                float pulso = 1f + 0.04f * Mathf.Sin(Time.time * 4f);
                p.t.localScale = p.escalaInicial * pulso;
            }
        }

        // ---------------------------------------------------------------- API para el menú

        public IReadOnlyList<ParteAnatomica> Partes => m_Piezas.ConvertAll(p => p.datos);

        public void CambiarModo(ModoVista modo) => AplicarModo(modo, true);

        public void FijarSeparacion(float valor) => m_SeparacionObjetivo = Mathf.Clamp01(valor);

        public void Seleccionar(int indice)
        {
            if (indice < 0 || indice >= m_Piezas.Count) indice = -1;
            if (Seleccion >= 0) m_Piezas[Seleccion].t.localScale = m_Piezas[Seleccion].escalaInicial;
            Seleccion = indice;
            if (Modo == ModoVista.Completa && indice >= 0)
            {
                // Seleccionar una pieza desde la vista completa abre la informativa.
                AplicarModo(ModoVista.Informativa, true);
            }
            ActualizarMateriales();
            ParteSeleccionada?.Invoke(Seleccion);
        }

        public void SeleccionarSiguiente(int direccion)
        {
            int n = m_Piezas.Count;
            if (n == 0) return;
            int i = Seleccion < 0 ? (direccion > 0 ? 0 : n - 1) : (Seleccion + direccion + n) % n;
            Seleccionar(i);
        }

        public void AlternarColorTunicas()
        {
            ColorTunicas = !ColorTunicas;
            ActualizarMateriales();
        }

        /// <summary>Caja que ocupa la pieza en el mundo (para colocar su etiqueta).</summary>
        public Bounds LimitesDe(int indice)
        {
            var p = m_Piezas[indice];
            var caja = new Bounds(p.t.position, Vector3.zero);
            foreach (var r in p.renderers) caja.Encapsulate(r.bounds);
            return caja;
        }

        /// <summary>Índice de la pieza a la que pertenece <paramref name="t"/>, o -1 si no es una pieza.</summary>
        public int IndiceDe(Transform t)
        {
            for (int i = 0; i < m_Piezas.Count; i++)
                if (t == m_Piezas[i].t || t.IsChildOf(m_Piezas[i].t)) return i;
            return -1;
        }

        /// <summary>
        /// Pieza más cercana que atraviesa el rayo, o -1. Funciona en cualquier vista:
        /// activa un momento los colliders de las piezas (en la vista completa están apagados).
        /// </summary>
        public int PiezaBajoRayo(Ray rayo, float distancia = 20f)
        {
            var encendidos = new List<Collider>();
            foreach (var p in m_Piezas)
                foreach (var col in p.colliders)
                    if (!col.enabled) { col.enabled = true; encendidos.Add(col); }
            bool agarre = colliderAgarre != null && colliderAgarre.enabled;
            if (agarre) colliderAgarre.enabled = false;

            int indice = -1;
            float masCerca = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(rayo, distancia))
            {
                int i = IndiceDe(hit.collider.transform);
                if (i >= 0 && hit.distance < masCerca) { masCerca = hit.distance; indice = i; }
            }

            foreach (var col in encendidos) col.enabled = false;
            if (agarre) colliderAgarre.enabled = true;
            return indice;
        }

        /// <summary>Gira el órgano según un arrastre en pantalla (modo de escritorio).</summary>
        public void Girar(Vector2 grados, Transform camara)
        {
            m_RotacionObjetivo = null;
            transform.Rotate(camara.up, -grados.x, Space.World);
            transform.Rotate(camara.right, grados.y, Space.World);
        }

        public void CambiarTamano(float factor)
        {
            var s = Mathf.Clamp(transform.localScale.x * factor, m_EscalaBase * 0.3f, m_EscalaBase * 4f);
            transform.localScale = Vector3.one * s;
        }

        public void Reiniciar()
        {
            transform.localScale = Vector3.one * m_EscalaBase;
            ColorTunicas = false;
            Seleccionar(-1);
            AplicarModo(ModoVista.Completa, true);
            TraerFrenteAlUsuario();
        }

        /// <summary>Coloca el órgano frente al usuario, orientado según la vista actual.</summary>
        public void TraerFrenteAlUsuario()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var c = cam.transform;
            var adelante = Vector3.ProjectOnPlane(c.forward, Vector3.up);
            if (adelante.sqrMagnitude < 1e-4f) adelante = Vector3.ProjectOnPlane(c.up, Vector3.up);
            adelante.Normalize();
            float distancia = Modo == ModoVista.Completa ? distanciaAlUsuario : distanciaAlUsuario * 1.35f;
            m_PosicionObjetivo = c.position + adelante * distancia + Vector3.down * 0.12f;
            m_RotacionObjetivo = RotacionParaModo(m_PosicionObjetivo.Value, c.position);
        }

        /// <summary>Punto a un lado del órgano donde conviene poner el menú.</summary>
        public Vector3 PuntoParaMenu(Vector3 posicionCamara)
        {
            var centro = m_PosicionObjetivo ?? transform.position;
            var haciaCam = Vector3.ProjectOnPlane(posicionCamara - centro, Vector3.up).normalized;
            var derecha = Vector3.Cross(haciaCam, Vector3.up);   // derecha vista por el usuario
            float ancho = tamanoMostrado * (Modo == ModoVista.Completa ? 1.6f : 3.4f);
            return centro + derecha * ancho + haciaCam * 0.15f;
        }

        // ---------------------------------------------------------------- Interno

        void AplicarModo(ModoVista modo, bool reorientar)
        {
            Modo = modo;
            switch (modo)
            {
                case ModoVista.Completa:
                    m_SeparacionObjetivo = 0f;
                    break;
                case ModoVista.Desglosada:
                    if (m_SeparacionObjetivo < 0.05f) m_SeparacionObjetivo = 1f;
                    break;
                case ModoVista.Informativa:
                    if (m_SeparacionObjetivo < 0.05f) m_SeparacionObjetivo = 0.6f;
                    if (Seleccion < 0 && m_Piezas.Count > 0) Seleccion = 0;
                    break;
            }

            // Completa: se agarra el órgano entero. Otras: se apunta a cada pieza.
            bool piezasActivas = modo != ModoVista.Completa;
            if (colliderAgarre != null) colliderAgarre.enabled = !piezasActivas;
            foreach (var p in m_Piezas)
                foreach (var col in p.colliders)
                    col.enabled = piezasActivas;

            if (modo == ModoVista.Completa && Seleccion >= 0)
            {
                m_Piezas[Seleccion].t.localScale = m_Piezas[Seleccion].escalaInicial;
            }

            if (reorientar && Camera.main != null)
                m_RotacionObjetivo = RotacionParaModo(transform.position, Camera.main.transform.position);

            ActualizarMateriales();
            ModoCambiado?.Invoke(modo);
            if (modo == ModoVista.Informativa) ParteSeleccionada?.Invoke(Seleccion);
        }

        /// <summary>
        /// Completa: el frente mira al usuario girado 30° (vista de tres cuartos).
        /// Desglosada/Informativa: el eje queda de lado, con la córnea a la izquierda del usuario,
        /// para que las capas se separen a lo ancho como en las láminas de referencia.
        /// </summary>
        Quaternion RotacionParaModo(Vector3 posicionOrgano, Vector3 posicionCamara)
        {
            var haciaCam = Vector3.ProjectOnPlane(posicionCamara - posicionOrgano, Vector3.up);
            if (haciaCam.sqrMagnitude < 1e-6f) haciaCam = Vector3.back;
            haciaCam.Normalize();
            Vector3 destino = Modo == ModoVista.Completa
                ? Quaternion.AngleAxis(-30f, Vector3.up) * haciaCam
                : Quaternion.AngleAxis(70f, Vector3.up) * haciaCam;    // córnea hacia la izquierda del usuario

            var arribaLocal = Mathf.Abs(Vector3.Dot(m_EjeFrontal, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            var desdeModelo = Quaternion.LookRotation(m_EjeFrontal, arribaLocal);
            var haciaMundo = Quaternion.LookRotation(destino, Vector3.up);
            return haciaMundo * Quaternion.Inverse(desdeModelo);
        }

        void ActualizarMateriales()
        {
            bool atenuar = Modo == ModoVista.Informativa && Seleccion >= 0;
            for (int i = 0; i < m_Piezas.Count; i++)
            {
                var p = m_Piezas[i];
                for (int r = 0; r < p.renderers.Length; r++)
                {
                    Material m;
                    if (atenuar && i != Seleccion && materialFantasma != null) m = materialFantasma;
                    else if (i == Seleccion && Modo != ModoVista.Completa && p.resaltado != null) m = p.resaltado;
                    else if (ColorTunicas && p.tunica != null) m = p.tunica;
                    else { p.renderers[r].sharedMaterials = p.originales[r]; continue; }
                    var mats = new Material[p.originales[r].Length];
                    for (int k = 0; k < mats.Length; k++) mats[k] = m;
                    p.renderers[r].sharedMaterials = mats;
                }
            }
        }

        void AjustarColliderAgarre(Pieza referencia)
        {
            if (colliderAgarre is SphereCollider esfera && referencia != null && referencia.renderers.Length > 0)
            {
                var b = referencia.renderers[0].bounds;
                esfera.center = transform.InverseTransformPoint(b.center);
                esfera.radius = transform.InverseTransformVector(b.extents).magnitude / Mathf.Sqrt(3f) * 1.1f;
            }
        }

        static Material Clonar(Material origen, Func<Color, Color> color, float alpha)
        {
            if (origen == null) return null;
            var m = new Material(origen) { name = origen.name + " (atlas)" };
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            var nuevo = color(c);
            nuevo.a = c.a * alpha;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", nuevo); else m.color = nuevo;
            return m;
        }

        static Transform BuscarHijo(Transform raiz, string nombre)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
            return null;
        }
    }
}
