using System.Collections.Generic;

namespace AtlasAnatomico
{
    /// <summary>
    /// Contenido inicial del ojo (borrador de Modelado/ojo/README.md, sección 3).
    /// El constructor de escena lo copia al OrganoController; desde ahí se edita en el Inspector.
    /// Cifras aproximadas de un adulto: pendiente contrastar con fuente citable.
    /// Kichwa: solo "ñawi" está validado; el resto queda pendiente de un hablante nativo.
    /// </summary>
    public static class DatosOjo
    {
        public const string Prefijo = "OJO_";
        public const string Nombre = "Ojo";
        public const string Kichwa = "Ñawi";

        public static List<ParteAnatomica> Partes() => new List<ParteAnatomica>
        {
            new ParteAnatomica {
                id = "Cornea", nombre = "Córnea", kichwa = "pendiente", tunica = "Fibrosa", paso = -4f,
                descripcion = "Ventana transparente al frente del ojo. Es la lente más potente: desvía la luz hacia dentro.",
                dato = "~11,5 mm de diámetro y 0,5 mm de grosor. Aporta cerca de 2/3 del enfoque." },
            new ParteAnatomica {
                id = "Iris", nombre = "Iris y pupila", kichwa = "pendiente", tunica = "Vascular", paso = -3f,
                descripcion = "Músculo pigmentado que da color al ojo. Su abertura central, la pupila, regula cuánta luz entra, como el diafragma de una cámara.",
                dato = "La pupila mide ~2 mm con mucha luz y hasta 8 mm con poca." },
            new ParteAnatomica {
                id = "Cristalino", nombre = "Cristalino", kichwa = "pendiente", tunica = "Medio transparente", paso = -2f,
                descripcion = "Lente flexible que cambia de forma para enfocar de cerca o de lejos (acomodación).",
                dato = "~9,5 mm de diámetro y 4 mm de grosor. Con la edad se endurece (presbicia) y puede opacarse (catarata)." },
            new ParteAnatomica {
                id = "Cuerpo_Ciliar", nombre = "Cuerpo ciliar", kichwa = "pendiente", tunica = "Vascular", paso = -1f,
                descripcion = "Anillo muscular que sostiene el cristalino y cambia su forma. También produce el humor acuoso.",
                dato = "Se une al cristalino por fibras finas: la zónula de Zinn." },
            new ParteAnatomica {
                id = "Humor_Vitreo", nombre = "Humor vítreo", kichwa = "pendiente", tunica = "Medio transparente", paso = 0f,
                descripcion = "Gel transparente que llena el interior del ojo y mantiene su forma.",
                dato = "~4 ml; ocupa cerca del 80 % del volumen del ojo y es 99 % agua." },
            new ParteAnatomica {
                id = "Retina", nombre = "Retina", kichwa = "pendiente", tunica = "Nerviosa", paso = 1.5f,
                descripcion = "Capa sensible a la luz. Sus fotorreceptores convierten la luz en impulsos nerviosos.",
                dato = "~120 millones de bastones (poca luz) y 6 millones de conos (color). La fóvea da la visión más nítida." },
            new ParteAnatomica {
                id = "Coroides", nombre = "Coroides", kichwa = "pendiente", tunica = "Vascular", paso = 3f,
                descripcion = "Capa llena de vasos sanguíneos que nutre la retina. Su pigmento oscuro absorbe la luz sobrante.",
                dato = "Junto con el iris y el cuerpo ciliar forma la úvea." },
            new ParteAnatomica {
                id = "Esclerotica", nombre = "Esclerótica", kichwa = "pendiente", tunica = "Fibrosa", paso = 4.5f,
                descripcion = "Capa externa blanca y resistente: protege el ojo y sirve de anclaje a los músculos que lo mueven.",
                dato = "Es «lo blanco del ojo» y cubre ~5/6 del globo." },
            new ParteAnatomica {
                id = "Nervio_Optico", nombre = "Nervio óptico", kichwa = "pendiente", tunica = "Nerviosa", paso = 4.5f,
                descripcion = "Lleva las señales de la retina al cerebro.",
                dato = "~1,2 millones de fibras. Donde sale no hay fotorreceptores: el punto ciego." },
        };
    }
}
