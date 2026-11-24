using System.Collections.Generic;
using UnityEngine;

namespace AtlasAnatomico
{
    /// <summary>
    /// Contenido inicial de los pulmones. El constructor de escena lo copia al OrganoController.
    /// Cifras aproximadas de un adulto: pendiente contrastar con fuente citable.
    /// Kichwa: "surku" y los términos de las piezas quedan pendientes de un hablante nativo.
    /// </summary>
    public static class DatosPulmones
    {
        public const string Prefijo = "PUL_";
        public const string Nombre = "Pulmones";
        public const string Kichwa = "Surku";

        /// <summary>De ejes de Blender en cm (derecha del paciente -X, frente -Y, arriba +Z) a Unity en metros.</summary>
        static Vector3 Cm(float x, float y, float z) => new Vector3(-x, z, -y) * 0.01f;

        public static List<ParteAnatomica> Partes() => new List<ParteAnatomica>
        {
            new ParteAnatomica {
                id = "Traquea", nombre = "Tráquea", kichwa = "pendiente", tunica = "Vía aérea", desglose = Cm(0, 0, 9),
                descripcion = "Tubo que lleva el aire desde la laringe hasta los bronquios. Sus anillos de cartílago en forma de C la mantienen abierta.",
                dato = "Mide ~11 cm y tiene entre 16 y 20 anillos. Por detrás es blanda, junto al esófago." },
            new ParteAnatomica {
                id = "Bronquios", nombre = "Bronquios", kichwa = "pendiente", tunica = "Vía aérea", desglose = Cm(0, 0, 0),
                descripcion = "La tráquea se divide en dos bronquios principales, que se ramifican como un árbol cada vez más fino hasta los alvéolos.",
                dato = "Unas 23 generaciones de ramas. El bronquio derecho es más ancho y vertical." },
            new ParteAnatomica {
                id = "Pulmon_Der_Lobulo_Sup", nombre = "Lóbulo superior derecho", kichwa = "pendiente", tunica = "Pulmón derecho",
                desglose = Cm(-12, 0, 8), envoltura = true,
                descripcion = "Parte alta del pulmón derecho; su punta, el ápice, sobresale por encima de la clavícula.",
                dato = "La cisura horizontal lo separa del lóbulo medio." },
            new ParteAnatomica {
                id = "Pulmon_Der_Lobulo_Med", nombre = "Lóbulo medio", kichwa = "pendiente", tunica = "Pulmón derecho",
                desglose = Cm(-18, -3, -1), envoltura = true,
                descripcion = "Lóbulo más pequeño, en forma de cuña, al frente del pulmón derecho.",
                dato = "Solo existe en el derecho: 3 lóbulos, frente a 2 del izquierdo." },
            new ParteAnatomica {
                id = "Pulmon_Der_Lobulo_Inf", nombre = "Lóbulo inferior derecho", kichwa = "pendiente", tunica = "Pulmón derecho",
                desglose = Cm(-9, 4, -13), envoltura = true,
                descripcion = "Ocupa la parte posterior y baja del pulmón derecho y se apoya sobre el diafragma.",
                dato = "La cisura oblicua lo separa de los otros dos lóbulos." },
            new ParteAnatomica {
                id = "Pulmon_Izq_Lobulo_Sup", nombre = "Lóbulo superior izquierdo", kichwa = "pendiente", tunica = "Pulmón izquierdo",
                desglose = Cm(13, 0, 7), envoltura = true,
                descripcion = "Incluye el ápice y la língula. Su borde tiene un hueco para el corazón: la incisura cardíaca.",
                dato = "Por el corazón, el pulmón izquierdo es algo más pequeño que el derecho." },
            new ParteAnatomica {
                id = "Pulmon_Izq_Lobulo_Inf", nombre = "Lóbulo inferior izquierdo", kichwa = "pendiente", tunica = "Pulmón izquierdo",
                desglose = Cm(10, 4, -13), envoltura = true,
                descripcion = "Parte posterior y baja del pulmón izquierdo, sobre el diafragma.",
                dato = "Entre los dos pulmones hay cientos de millones de alvéolos: ~70 m² para intercambiar gases." },
            new ParteAnatomica {
                id = "Diafragma", nombre = "Diafragma", kichwa = "pendiente", tunica = "Músculo respiratorio", desglose = Cm(0, 0, -19),
                descripcion = "Músculo principal de la respiración. Al contraerse baja y se aplana: el tórax se agranda y entra el aire. Al relajarse sube y el aire sale.",
                dato = "Baja ~1,5 cm en reposo y hasta 10 cm en una inspiración profunda." },
        };
    }
}
