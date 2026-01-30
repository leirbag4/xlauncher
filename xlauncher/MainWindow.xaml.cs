using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace xlauncher
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<ItemResult> Items { get; set; }

        private readonly SolidColorBrush _violetaBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A2BE2")); // Archivos normales
        private readonly SolidColorBrush _historyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5E5AEE")); // Historial (Violeta azulado)

        private HistoryManager _historyManager;
        private bool _isNavigating = false;

        public MainWindow()
        {
            InitializeComponent();

            _historyManager = new HistoryManager(); // Cargar historial
            Items = new ObservableCollection<ItemResult>();
            ResultList.ItemsSource = Items;

            InputBox.Focus();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isNavigating) return;

            string query = InputBox.Text;

            if (string.IsNullOrWhiteSpace(query))
            {
                Items.Clear();
                return;
            }

            // Normalizamos path para búsqueda en disco
            string pathProcesado = query;
            if (query.StartsWith("\\")) pathProcesado = "c:" + query;

            ActualizarListado(query, pathProcesado);
        }

        private void ActualizarListado(string queryOriginal, string pathDisco)
        {
            Items.Clear();

            // 1. BUSCAR EN HISTORIAL (Primero)
            // Solo buscamos en historial si el usuario NO está escribiendo una ruta compleja (con barras intermedias)
            // o si queremos que busque por nombre de archivo simple (ej: "chrom" -> "chrome.exe")
            string nombreBusqueda = Path.GetFileName(queryOriginal);
            if (!string.IsNullOrEmpty(nombreBusqueda))
            {
                var historyMatches = _historyManager.Search(nombreBusqueda);
                foreach (var h in historyMatches)
                {
                    Items.Add(new ItemResult
                    {
                        Name = h.FileName + " (Historial)", // Agregamos sufijo visual opcional o lo dejamos limpio
                        FullPath = h.FullPath,
                        Type = "History",
                        ColorBrush = _historyBrush // Color Violeta-Azul distinto
                    });
                }
            }

            // 2. BUSCAR EN DISCO (Después)
            try
            {
                string directorioBusqueda = "";
                string filtro = "";

                if (pathDisco.EndsWith("\\"))
                {
                    directorioBusqueda = pathDisco;
                    filtro = "";
                }
                else
                {
                    directorioBusqueda = Path.GetDirectoryName(pathDisco);
                    filtro = Path.GetFileName(pathDisco);
                }

                if (!string.IsNullOrEmpty(directorioBusqueda) && Directory.Exists(directorioBusqueda))
                {
                    var dirInfo = new DirectoryInfo(directorioBusqueda);
                    // Usamos EnumerationOptions para manejar errores de acceso de forma más limpia en .NET Core/5+
                    // Si usas .NET Framework viejo, el try-catch externo ya lo cubre.
                    var fileSystemInfos = dirInfo.EnumerateFileSystemInfos($"{filtro}*");

                    int count = 0;
                    foreach (var item in fileSystemInfos)
                    {
                        // Evitar duplicados si ya salió en el historial
                        if (Items.Any(x => x.FullPath == item.FullName)) continue;

                        if (count >= 20) break; // Límite para rendimiento

                        bool isFolder = (item.Attributes & FileAttributes.Directory) == FileAttributes.Directory;

                        Items.Add(new ItemResult
                        {
                            Name = item.Name,
                            FullPath = item.FullName,
                            Type = isFolder ? "Folder" : "File",
                            ColorBrush = _violetaBrush
                        });
                        count++;
                    }
                }
            }
            catch { }

            // Seleccionar automáticamente el primer elemento
            if (Items.Count > 0) ResultList.SelectedIndex = 0;
        }

        // --- TECLADO ---

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Application.Current.Shutdown();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                MoverSeleccion(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                MoverSeleccion(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                AutoCompletarSeleccion();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ProcesarEnter();
                e.Handled = true;
            }
        }

        private void MoverSeleccion(int direccion)
        {
            if (Items.Count == 0) return;
            int nuevoIndice = ResultList.SelectedIndex + direccion;
            if (nuevoIndice < 0) nuevoIndice = 0;
            if (nuevoIndice >= Items.Count) nuevoIndice = Items.Count - 1;
            ResultList.SelectedIndex = nuevoIndice;
            ResultList.ScrollIntoView(ResultList.SelectedItem);
        }

        private void AutoCompletarSeleccion()
        {
            if (ResultList.SelectedItem is ItemResult item)
            {
                _isNavigating = true;

                // Lógica solicitada: TAB copia el PATH completo al input
                if (item.Type == "History")
                {
                    // Si viene del historial, ponemos el path completo directo para ejecutar
                    InputBox.Text = item.FullPath;
                }
                else
                {
                    // Lógica visual para navegación de carpetas (\Windows...)
                    bool usarShortPath = InputBox.Text.StartsWith("\\");
                    if (usarShortPath && item.FullPath.Length > 2)
                        InputBox.Text = item.FullPath.Substring(2); // Quita "c:"
                    else
                        InputBox.Text = item.FullPath;

                    if (item.Type == "Folder" && !InputBox.Text.EndsWith("\\"))
                    {
                        InputBox.Text += "\\";
                    }
                }

                InputBox.CaretIndex = InputBox.Text.Length;
                _isNavigating = false;

                // Refrescamos la lista para mostrar el contenido de la nueva ruta
                InputBox_TextChanged(null, null);
            }
        }

        private void ProcesarEnter()
        {
            if (ResultList.SelectedItem is ItemResult item)
            {
                if (item.Type == "Folder")
                {
                    AutoCompletarSeleccion();
                }
                else
                {
                    EjecutarArchivo(item);
                }
            }
            else
            {
                // Si el usuario dio enter sin seleccionar nada de la lista,
                // intentamos ejecutar lo que haya escrito en el input
                string manualPath = InputBox.Text;
                if (manualPath.StartsWith("\\")) manualPath = "c:" + manualPath;

                if (File.Exists(manualPath))
                {
                    EjecutarArchivo(new ItemResult { FullPath = manualPath, Type = "File" });
                }
            }
        }

        private void EjecutarArchivo(ItemResult item)
        {
            try
            {
                // 1. Guardar en Historial antes de ejecutar
                _historyManager.AddOrUpdate(item.FullPath);

                // 2. Ejecutar
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir: " + ex.Message);
            }
        }

        private void ResultList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            InputBox.Focus();
        }
    }
}