using System.Collections.Generic;
using UnityEngine;

namespace AtlasAnatomico
{
    /// <summary>
    /// Marca una ventana flotante (panel, ventana de la pieza, etiqueta) para que en el modo de
    /// escritorio se agrande o reduzca con la rueda del ratón cuando el cursor está sobre ella.
    /// Escala este transform respecto a su tamaño inicial, entre <see cref="minimo"/> y <see cref="maximo"/>.
    /// </summary>
    public class AmpliableConRueda : MonoBehaviour
    {
        [Tooltip("Zona que debe estar bajo el cursor (por defecto, este mismo RectTransform).")]
        public RectTransform zona;
        public float minimo = 0.6f;
        public float maximo = 3f;

        public static readonly List<AmpliableConRueda> Activas = new List<AmpliableConRueda>();

        float m_EscalaBase;
        float m_Factor = 1f;

        void Awake()
        {
            m_EscalaBase = transform.localScale.x;
            if (zona == null) zona = transform as RectTransform;
        }

        void OnEnable() => Activas.Add(this);
        void OnDisable() => Activas.Remove(this);

        public bool Contiene(Vector2 pantalla, Camera cam) =>
            zona != null && RectTransformUtility.RectangleContainsScreenPoint(zona, pantalla, cam);

        public void Ampliar(float factor)
        {
            m_Factor = Mathf.Clamp(m_Factor * factor, minimo, maximo);
            transform.localScale = Vector3.one * (m_EscalaBase * m_Factor);
        }
    }
}
