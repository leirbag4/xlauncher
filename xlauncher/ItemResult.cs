using System.Windows.Media;

public class ItemResult
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public string Type { get; set; } // "File", "Folder", "History"
    public SolidColorBrush ColorBrush { get; set; }

    // Propiedades nuevas para manejar la visibilidad del menú
    public bool IsHistoryItem { get; set; }
    public bool IsHistoryFile { get; set; } // True solo si es Historial Y es Archivo (para Rename)
}