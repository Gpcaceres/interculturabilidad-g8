using System.Collections.Generic;
using UnityEngine;

namespace AtlasAnatomico
{
    public enum RitmoRespiracion { Pausa, Reposo, Profunda, Ejercicio }

    /// <summary>
    /// Anima la respiración: mueve la blend shape "Inhalar" de todas las piezas a la vez.
    /// La inspiración es más corta y activa; la espiración, más larga y pasiva, con una pausa al final.
    /// Los cambios de ritmo se suavizan para que no haya saltos.
    /// </summary>
    public class Respiracion : MonoBehaviour
    {
        const string Clave = "Inhalar";

        /// <summary>Respiraciones por minuto y profundidad (1 = blend shape completa) de cada ritmo.</summary>
        static readonly Dictionary<RitmoRespiracion, (float porMinuto, float profundidad)> Ritmos =
            new Dictionary<RitmoRespiracion, (float, float)>
            {
                { RitmoRespiracion.Pausa, (0f, 0f) },
                { RitmoRespiracion.Reposo, (14f, 0.65f) },
                { RitmoRespiracion.Profunda, (7f, 1f) },
                { RitmoRespiracion.Ejercicio, (34f, 1f) },
            };

        public RitmoRespiracion ritmoInicial = RitmoRespiracion.Reposo;

        public RitmoRespiracion Ritmo { get; private set; }
        /// <summary>Cuánto aire hay ahora: 0 = espirado, 1 = inspiración máxima.</summary>
        public float Aire { get; private set; }
        /// <summary>Velocidad del aire por segundo; positiva al inhalar, negativa al exhalar.</summary>
        public float Caudal { get; private set; }

        readonly List<(SkinnedMeshRenderer malla, int indice)> m_Mallas = new List<(SkinnedMeshRenderer, int)>();
        float m_Fase, m_PorMinuto, m_Profundidad;

        void Awake()
        {
            foreach (var malla in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = malla.sharedMesh;
                for (int i = 0; m != null && i < m.blendShapeCount; i++)
                    // Según el exportador, el nombre puede venir con prefijo ("Key.Inhalar").
                    if (m.GetBlendShapeName(i).EndsWith(Clave)) m_Mallas.Add((malla, i));
            }
            CambiarRitmo(ritmoInicial);
            m_PorMinuto = Ritmos[Ritmo].porMinuto;
            m_Profundidad = Ritmos[Ritmo].profundidad;
        }

        public int MallasAnimadas => m_Mallas.Count;

        public void CambiarRitmo(RitmoRespiracion ritmo) => Ritmo = ritmo;

        void Update()
        {
            var (porMinuto, profundidad) = Ritmos[Ritmo];
            float k = 1f - Mathf.Exp(-2.5f * Time.deltaTime);
            m_PorMinuto = Mathf.Lerp(m_PorMinuto, porMinuto, k);
            m_Profundidad = Mathf.Lerp(m_Profundidad, profundidad, k);
            m_Fase = Mathf.Repeat(m_Fase + m_PorMinuto / 60f * Time.deltaTime, 1f);

            float anterior = Aire;
            Aplicar(Curva(m_Fase) * m_Profundidad);
            if (Time.deltaTime > 1e-5f) Caudal = (Aire - anterior) / Time.deltaTime;
        }

        /// <summary>Fija el aire directamente (0–1) sobre todas las piezas.</summary>
        public void Aplicar(float aire)
        {
            Aire = aire;
            foreach (var (malla, indice) in m_Mallas) malla.SetBlendShapeWeight(indice, aire * 100f);
        }

        static float Curva(float fase)
        {
            const float finInspiracion = 0.40f, finEspiracion = 0.90f;
            if (fase < finInspiracion) return Mathf.SmoothStep(0f, 1f, fase / finInspiracion);
            if (fase < finEspiracion) return Mathf.SmoothStep(1f, 0f, (fase - finInspiracion) / (finEspiracion - finInspiracion));
            return 0f;
        }

        public static string Nombre(RitmoRespiracion r) => r switch
        {
            RitmoRespiracion.Pausa => "Pausa",
            RitmoRespiracion.Reposo => "Reposo",
            RitmoRespiracion.Profunda => "Profunda",
            _ => "Ejercicio",
        };
    }
}
