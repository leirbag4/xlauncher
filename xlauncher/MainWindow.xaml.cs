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

        private readonly SolidColorBrush _violetaBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B49AE2"));
        //private readonly SolidColorBrush _historyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5E5AEE"));
        // Antes era #5E5AEE. Ahora usamos un tono que armoniza con el violeta oscuro
        private readonly SolidColorBrush _historyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7D46B7"));
        private HistoryManager _historyManager;
        private bool _isNavigating = false;

        public MainWindow()
        {
            InitializeComponent();
            _historyManager = new HistoryManager();
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

            string pathProcesado = query;
            if (query.StartsWith("\\")) pathProcesado = "c:" + query;

            ActualizarListado(query, pathProcesado);
        }

        private void ActualizarListado(string queryOriginal, string pathDisco)
        {
            Items.Clear();

            // 1. HISTORIAL
            string nombreBusqueda = Path.GetFileName(queryOriginal);
            if (!string.IsNullOrEmpty(nombreBusqueda))
            {
                var historyMatches = _historyManager.Search(nombreBusqueda);
                foreach (var h in historyMatches)
                {
                    // Determinar el nombre a mostrar (Alias o Nombre real)
                    string displayName = !string.IsNullOrEmpty(h.Alias) ? h.Alias : h.FileName;
                    string suffix = !string.IsNullOrEmpty(h.Alias) ? " (Alias)" : " (Historial)";

                    Items.Add(new ItemResult
                    {
                        Name = displayName + suffix,
                        FullPath = h.FullPath,
                        Type = "History",
                        ColorBrush = _historyBrush,

                        // Flags para el menu contextual
                        IsHistoryItem = true,
                        IsHistoryFile = !h.IsFolder // Solo renombramos archivos
                    });
                }
            }

            // 2. DISCO
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
                    var fileSystemInfos = dirInfo.EnumerateFileSystemInfos($"{filtro}*");

                    int count = 0;
                    foreach (var item in fileSystemInfos)
                    {
                        if (Items.Any(x => x.FullPath == item.FullName)) continue;
                        if (count >= 20) break;

                        bool isFolder = (item.Attributes & FileAttributes.Directory) == FileAttributes.Directory;

                        Items.Add(new ItemResult
                        {
                            Name = item.Name,
                            FullPath = item.FullName,
                            Type = isFolder ? "Folder" : "File",
                            ColorBrush = _violetaBrush,

                            // Flags para el menu contextual
                            IsHistoryItem = false,
                            IsHistoryFile = false
                        });
                        count++;
                    }
                }
            }
            catch { }

            if (Items.Count > 0) ResultList.SelectedIndex = 0;
        }

        // --- MENU CONTEXTUAL ---

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (ResultList.SelectedItem is ItemResult selectedItem)
            {
                // Limpiamos el nombre para mostrar en el input (quitamos "(Historial)")
                string rawName = selectedItem.Name.Replace(" (Historial)", "").Replace(" (Alias)", "").Trim();

                RenameWindow rw = new RenameWindow(rawName);
                // Centramos respecto a la app principal
                rw.Owner = this;

                if (rw.ShowDialog() == true)
                {
                    // Actualizamos en la BD
                    _historyManager.UpdateAlias(selectedItem.FullPath, rw.NewName);
                    // Refrescamos la lista para ver el cambio
                    InputBox_TextChanged(null, null);
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ResultList.SelectedItem is ItemResult selectedItem)
            {
                _historyManager.Remove(selectedItem.FullPath);
                InputBox_TextChanged(null, null); // Refrescar lista
            }
        }

        // --- TECLADO / EJECUCIÓN ---

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Application.Current.Shutdown(); e.Handled = true; }
            else if (e.Key == Key.Down) { MoverSeleccion(1); e.Handled = true; }
            else if (e.Key == Key.Up) { MoverSeleccion(-1); e.Handled = true; }
            else if (e.Key == Key.Tab) { AutoCompletarSeleccion(); e.Handled = true; }
            else if (e.Key == Key.Enter) { ProcesarEnter(); e.Handled = true; }
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

                if (item.IsHistoryItem)
                {
                    InputBox.Text = item.FullPath;
                }
                else
                {
                    bool usarShortPath = InputBox.Text.StartsWith("\\");
                    if (usarShortPath && item.FullPath.Length > 2)
                        InputBox.Text = item.FullPath.Substring(2);
                    else
                        InputBox.Text = item.FullPath;

                    if (item.Type == "Folder" && !InputBox.Text.EndsWith("\\"))
                    {
                        InputBox.Text += "\\";
                    }
                }

                InputBox.CaretIndex = InputBox.Text.Length;
                _isNavigating = false;
                InputBox_TextChanged(null, null);
            }
        }

        private void ProcesarEnter()
        {
            if (ResultList.SelectedItem is ItemResult item)
            {
                if (item.Type == "Folder" && !item.IsHistoryItem) // Si es carpeta de disco entramos
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
                string manualPath = InputBox.Text;
                if (manualPath.StartsWith("\\")) manualPath = "c:" + manualPath;
                if (File.Exists(manualPath))
                {
                    EjecutarArchivo(new ItemResult { FullPath = manualPath, Type = "File", Name = Path.GetFileName(manualPath) });
                }
            }
        }

        private void EjecutarArchivo(ItemResult item)
        {
            try
            {
                // Detectar si es carpeta para marcarlo correctamente en historial
                bool isFolder = Directory.Exists(item.FullPath);

                _historyManager.AddOrUpdate(item.FullPath, isFolder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void ResultList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            InputBox.Focus();
        }
    }
}