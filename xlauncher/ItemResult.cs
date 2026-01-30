using System.Windows.Media;

namespace xlauncher
{
    public class ItemResult
    {
        public string Name { get; set; }        // Nombre a mostrar (ej: "Windows")
        public string FullPath { get; set; }    // Ruta completa (ej: "C:\Windows")
        public string Type { get; set; }        // "File", "Folder", "History"
        public SolidColorBrush ColorBrush { get; set; } // Color del texto (Violeta o Azul)
    }
}