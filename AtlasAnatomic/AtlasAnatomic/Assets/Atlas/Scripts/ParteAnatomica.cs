using System;
using UnityEngine;

namespace AtlasAnatomico
{
    /// <summary>
    /// Datos de una pieza de un órgano: cómo se llama el objeto del modelo,
    /// qué se muestra en el panel informativo y cuánto se separa en la vista desglosada.
    /// </summary>
    [Serializable]
    public class ParteAnatomica
    {
        [Tooltip("Nombre del objeto en el modelo (sin prefijo). Ej.: Cornea -> busca OJO_Cornea.")]
        public string id;

        public string nombre;

        [Tooltip("Término en kichwa. Vacío o 'pendiente' si aún no está validado.")]
        public string kichwa;

        [Tooltip("Túnica o grupo: Fibrosa, Vascular, Nerviosa o Medio transparente.")]
        public string tunica;

        [TextArea(2, 6)] public string descripcion;
        [TextArea(1, 3)] public string dato;

        [Tooltip("Pasos que se desplaza en la vista desglosada. Negativo = hacia delante (lado de la córnea).")]
        public float paso;
    }
}
