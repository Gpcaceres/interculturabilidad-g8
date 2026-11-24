using System;
using UnityEngine;

namespace AtlasAnatomico
{
    /// <summary>
    /// Partículas que recorren la tráquea y el árbol bronquial al ritmo de <see cref="Respiracion"/>:
    /// azules hacia dentro al inhalar (oxígeno) y naranjas hacia fuera al exhalar (dióxido de carbono).
    /// Los recorridos salen de pulmones_flujo.json (lo genera pulmones_modelo_base.py) y siguen la
    /// deformación de los bronquios. Se ocultan al desglosar, porque las piezas dejan de coincidir.
    /// </summary>
    [RequireComponent(typeof(Respiracion))]
    public class FlujoAire : MonoBehaviour
    {
        [Serializable] class Ruta { public float[] reposo; public float[] inhalado; }
        [Serializable] class Datos { public Ruta[] rutas; }

        public TextAsset recorridos;
        public Material material;
        public int cantidad = 420;
        [Tooltip("Diámetro de cada partícula, en metros del modelo.")]
        public float tamano = 0.0065f;
        [Tooltip("Recorridos completos por cada inspiración.")]
        public float alcance = 1.15f;

        static readonly Color cEntra = new Color(0.30f, 0.78f, 1f);
        static readonly Color cSale = new Color(1f, 0.55f, 0.25f);

        public bool Visible { get; private set; } = true;
        public int Recorridos => m_Reposo?.Length ?? 0;

        OrganoController m_Organo;
        Respiracion m_Respiracion;
        ParticleSystem m_Sistema;
        ParticleSystem.Particle[] m_Particulas;
        Vector3[][] m_Reposo, m_Inhalado;
        int[] m_Ruta;
        float[] m_Avance, m_Velocidad;
        Vector3[] m_Desvio;
        float m_Tinte, m_Opacidad;

        void Start()
        {
            m_Organo = GetComponent<OrganoController>();
            m_Respiracion = GetComponent<Respiracion>();
            if (recorridos == null) { enabled = false; return; }

            var datos = JsonUtility.FromJson<Datos>(recorridos.text);
            m_Reposo = Array.ConvertAll(datos.rutas, r => Puntos(r.reposo));
            m_Inhalado = Array.ConvertAll(datos.rutas, r => Puntos(r.inhalado));

            m_Ruta = new int[cantidad];
            m_Avance = new float[cantidad];
            m_Velocidad = new float[cantidad];
            m_Desvio = new Vector3[cantidad];
            for (int i = 0; i < cantidad; i++)
            {
                m_Ruta[i] = UnityEngine.Random.Range(0, m_Reposo.Length);
                m_Avance[i] = UnityEngine.Random.value;
                m_Velocidad[i] = UnityEngine.Random.Range(0.75f, 1.25f);
                m_Desvio[i] = UnityEngine.Random.insideUnitSphere;
            }
            CrearSistema();
        }

        public void Alternar() => Visible = !Visible;

        void LateUpdate()
        {
            if (m_Sistema == null) return;
            bool juntas = m_Organo.SeparacionActual < 0.02f;
            // Al detenerse el aire (pausa entre respiraciones) las partículas se apagan.
            float objetivo = Visible && juntas ? Mathf.Clamp01(Mathf.Abs(m_Respiracion.Caudal) * 6f) : 0f;
            m_Opacidad = Mathf.Lerp(m_Opacidad, objetivo, 1f - Mathf.Exp(-6f * Time.deltaTime));
            m_Tinte = Mathf.Lerp(m_Tinte, m_Respiracion.Caudal >= 0f ? 1f : 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));

            float paso = m_Respiracion.Caudal * Time.deltaTime * alcance;
            float aire = m_Respiracion.Aire;
            var color = Color.Lerp(cSale, cEntra, m_Tinte);
            for (int i = 0; i < cantidad; i++)
            {
                m_Avance[i] += paso * m_Velocidad[i];
                if (m_Avance[i] > 1f || m_Avance[i] < 0f)
                {
                    m_Avance[i] = Mathf.Repeat(m_Avance[i], 1f);
                    m_Ruta[i] = UnityEngine.Random.Range(0, m_Reposo.Length);
                }
                float s = m_Avance[i];
                // El haz es ancho en la tráquea y se estrecha hacia las puntas.
                float ancho = Mathf.Lerp(0.0062f, 0.0006f, Mathf.Sqrt(s));
                m_Particulas[i].position = Punto(m_Ruta[i], s, aire) + m_Desvio[i] * ancho;
                m_Particulas[i].startSize = tamano * Mathf.Lerp(1f, 0.6f, s);
                // Se desvanecen en los extremos del recorrido para que no aparezcan de golpe.
                color.a = m_Opacidad * Mathf.Clamp01(s * 12f) * Mathf.Clamp01((1f - s) * 8f);
                m_Particulas[i].startColor = color;
                m_Particulas[i].remainingLifetime = m_Particulas[i].startLifetime = 1000f;
            }
            m_Sistema.SetParticles(m_Particulas, cantidad);
        }

        Vector3 Punto(int ruta, float s, float aire)
        {
            var r = m_Reposo[ruta];
            var h = m_Inhalado[ruta];
            float f = s * (r.Length - 1);
            int i = Mathf.Min((int)f, r.Length - 2);
            f -= i;
            return Vector3.Lerp(Vector3.Lerp(r[i], r[i + 1], f), Vector3.Lerp(h[i], h[i + 1], f), aire);
        }

        static Vector3[] Puntos(float[] v)
        {
            var p = new Vector3[v.Length / 3];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector3(v[3 * i], v[3 * i + 1], v[3 * i + 2]);
            return p;
        }

        void CrearSistema()
        {
            var go = new GameObject("Flujo de aire");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            m_Sistema = go.AddComponent<ParticleSystem>();
            m_Sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var principal = m_Sistema.main;
            principal.maxParticles = cantidad;
            principal.simulationSpace = ParticleSystemSimulationSpace.Local;
            principal.scalingMode = ParticleSystemScalingMode.Hierarchy;
            principal.playOnAwake = false;
            principal.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            principal.startSpeed = 0f;
            var emision = m_Sistema.emission;
            emision.enabled = false;
            var forma = m_Sistema.shape;
            forma.enabled = false;
            var dibujo = go.GetComponent<ParticleSystemRenderer>();
            dibujo.sharedMaterial = material;
            dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dibujo.maxParticleSize = 1f;
            m_Particulas = new ParticleSystem.Particle[cantidad];
            m_Sistema.Play();
        }
    }
}
