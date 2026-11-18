using System;
using System.Collections.Generic;

namespace CopiaSegura.Core
{
    public sealed class OptionHelp
    {
        public string Key;
        public string Title;
        public string Explanation;
        public string Flag;

        public OptionHelp(string key, string title, string explanation, string flag)
        {
            Key = key; Title = title; Explanation = explanation; Flag = flag;
        }
    }

    // Textos de ayuda del paso 2 (los mismos que la maqueta aprobada).
    public static class HelpTexts
    {
        public static readonly List<OptionHelp> All = new List<OptionHelp>();

        static HelpTexts()
        {
            All.Add(new OptionHelp("subfolders", "Incluir subcarpetas, también vacías",
                "Copia toda la estructura de carpetas completa.", "/E"));
            All.Add(new OptionHelp("dates", "Conservar fechas y atributos",
                "Los archivos mantienen su fecha de creación y modificación originales.", "/COPY:DAT /DCOPY:DAT"));
            All.Add(new OptionHelp("junctions", "Saltar enlaces de carpetas de Windows",
                "Los perfiles de usuario tienen accesos directos internos (\"Mis documentos\"…) que provocan bucles infinitos. Recomendado dejarlo activo.", "/XJ"));
            All.Add(new OptionHelp("restartable", "Poder reanudar si se corta",
                "Si se interrumpe, al volver a ejecutar continúa donde se quedó. Algo más lento en archivos grandes.", "/Z"));
            All.Add(new OptionHelp("retries", "Reintentos ante un archivo problemático",
                "Cuántas veces insistir y cuánto esperar antes de seguir con el siguiente. Menos reintentos = menos castigo al disco.", "/R /W"));
            All.Add(new OptionHelp("threads", "Velocidad (hilos de copia)",
                "Más hilos = más rápido, pero fuerza más el disco. Con 2 se cuida el disco de origen.", "/MT"));
            All.Add(new OptionHelp("log", "Guardar registro (log) de la copia",
                "Archivo de texto con cada archivo copiado y cada error. Imprescindible para comprobar después. Si se reanuda, el registro se añade al anterior.", "/UNILOG+ /TEE"));
            All.Add(new OptionHelp("mirror", "Espejo: borrar en destino lo que no esté en origen",
                "Puede BORRAR archivos del destino. Solo para sincronizar copias. Desbloquear exige confirmación escrita.", "/MIR /PURGE"));
            All.Add(new OptionHelp("move", "Mover en lugar de copiar",
                "BORRA el origen tras copiar. Nunca recomendado con datos sin otra copia.", "/MOVE"));
            All.Add(new OptionHelp("doubtful", "Disco dudoso",
                "Para discos con sospecha de fallo: 1 hilo y 1 reintento para forzar lo mínimo. Si hay ruidos, desconexiones o errores de lectura, puede convenir clonar el disco antes de copiar archivos.", ""));
        }

        public static OptionHelp Get(string key)
        {
            foreach (OptionHelp h in All) if (h.Key == key) return h;
            return null;
        }
    }
}
