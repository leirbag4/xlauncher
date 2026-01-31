using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices; // Necesario para Hotkey y Blur
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;     // Necesario para Hotkey y Blur
using System.Windows.Media;

namespace xlauncher
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<ItemResult> Items { get; set; }

        private readonly SolidColorBrush _violetaBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B49AE2"));
        private readonly SolidColorBrush _historyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7D46B7"));

        private HistoryManager _historyManager;
        private bool _isNavigating = false;
        private IntPtr _windowHandle; // Para manejar el Hotkey

        public MainWindow()
        {
            InitializeComponent();
            _historyManager = new HistoryManager();
            Items = new ObservableCollection<ItemResult>();
            ResultList.ItemsSource = Items;
            InputBox.Focus();
        }

        // =========================================================
        // 1. INICIALIZACIÓN Y REGISTRO DE HOTKEY Y BLUR
        // =========================================================
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 1. Activar Blur (lo que ya tenías)
            EnableBlur();

            // 2. Registrar Hotkey Global
            _windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(_windowHandle);
            source.AddHook(HwndHook);

            // Registro: ID=9000, MOD_ALT=0x0001, VK_SPACE=0x20
            // Si prefieres ALT + Q, cambia VK_SPACE por 0x51 (Q)
            RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_ALT, VK_SPACE);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Limpiar Hotkey al cerrar real
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            base.OnClosed(e);
        }

        // Escuchar mensajes de Windows para detectar el atajo
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindow(); // Mostrar u ocultar
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ToggleWindow()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                this.Activate();
                this.Topmost = true;  // Asegurar que quede arriba
                this.Topmost = false; // Truco para forzar foco a veces
                this.Topmost = true;
                InputBox.Focus();
                InputBox.SelectAll();
            }
        }

        // =========================================================
        // LÓGICA PRINCIPAL (Modificada para usar Hide en vez de Shutdown)
        // =========================================================

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // CAMBIO IMPORTANTE: Ocultar en vez de cerrar para seguir escuchando el atajo
                this.Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Down) { MoverSeleccion(1); e.Handled = true; }
            else if (e.Key == Key.Up) { MoverSeleccion(-1); e.Handled = true; }
            else if (e.Key == Key.Tab) { AutoCompletarSeleccion(); e.Handled = true; }
            else if (e.Key == Key.Enter) { ProcesarEnter(); e.Handled = true; }
        }

        private void EjecutarArchivo(ItemResult item)
        {
            try
            {
                bool isFolder = Directory.Exists(item.FullPath);
                _historyManager.AddOrUpdate(item.FullPath, isFolder);

                Process.Start(new ProcessStartInfo { FileName = item.FullPath, UseShellExecute = true });

                // CAMBIO IMPORTANTE: Ocultar tras ejecutar
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void ProcesarEnter()
        {
            // Pequeño truco: Comando 'exit' para cerrar la app de verdad
            if (InputBox.Text.ToLower() == "exit" || InputBox.Text.ToLower() == "/exit")
            {
                Application.Current.Shutdown();
                return;
            }

            if (ResultList.SelectedItem is ItemResult item)
            {
                if (item.Type == "Folder" && !item.IsHistoryItem)
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

        // --- RESTO DE LÓGICA SIN CAMBIOS ---

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
            string nombreBusqueda = Path.GetFileName(queryOriginal);
            if (!string.IsNullOrEmpty(nombreBusqueda))
            {
                var historyMatches = _historyManager.Search(nombreBusqueda);
                foreach (var h in historyMatches)
                {
                    string displayName = !string.IsNullOrEmpty(h.Alias) ? h.Alias : h.FileName;
                    string suffix = !string.IsNullOrEmpty(h.Alias) ? " (Alias)" : " (Historial)";
                    Items.Add(new ItemResult
                    {
                        Name = displayName + suffix,
                        FullPath = h.FullPath,
                        Type = "History",
                        ColorBrush = _historyBrush,
                        IsHistoryItem = true,
                        IsHistoryFile = !h.IsFolder
                    });
                }
            }
            try
            {
                string directorioBusqueda = "";
                string filtro = "";
                if (pathDisco.EndsWith("\\")) { directorioBusqueda = pathDisco; filtro = ""; }
                else { directorioBusqueda = Path.GetDirectoryName(pathDisco); filtro = Path.GetFileName(pathDisco); }

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

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (ResultList.SelectedItem is ItemResult selectedItem)
            {
                string rawName = selectedItem.Name.Replace(" (Historial)", "").Replace(" (Alias)", "").Trim();
                RenameWindow rw = new RenameWindow(rawName);
                rw.Owner = this;
                if (rw.ShowDialog() == true)
                {
                    _historyManager.UpdateAlias(selectedItem.FullPath, rw.NewName);
                    InputBox_TextChanged(null, null);
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ResultList.SelectedItem is ItemResult selectedItem)
            {
                _historyManager.Remove(selectedItem.FullPath);
                InputBox_TextChanged(null, null);
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
                if (item.IsHistoryItem) InputBox.Text = item.FullPath;
                else
                {
                    bool usarShortPath = InputBox.Text.StartsWith("\\");
                    if (usarShortPath && item.FullPath.Length > 2) InputBox.Text = item.FullPath.Substring(2);
                    else InputBox.Text = item.FullPath;
                    if (item.Type == "Folder" && !InputBox.Text.EndsWith("\\")) InputBox.Text += "\\";
                }
                InputBox.CaretIndex = InputBox.Text.Length;
                _isNavigating = false;
                InputBox_TextChanged(null, null);
            }
        }

        private void ResultList_PreviewKeyDown(object sender, KeyEventArgs e) { InputBox.Focus(); }

        // =========================================================
        // BLUR & HOTKEY HELPERS
        // =========================================================

        private void EnableBlur()
        {
            var windowHelper = new WindowInteropHelper(this);
            var accent = new AccentPolicy();
            accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData();
            data.Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY;
            data.SizeOfData = accentStructSize;
            data.Data = accentPtr;
            SetWindowCompositionAttribute(windowHelper.Handle, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        // Constantes para Hotkey
        private const int HOTKEY_ID = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_SPACE = 0x20; // Tecla Espacio
        // private const uint VK_Q = 0x51;  // Tecla Q (Por si quieres cambiarlo)

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData { public WindowCompositionAttribute Attribute; public IntPtr Data; public int SizeOfData; }
        internal enum WindowCompositionAttribute { WCA_ACCENT_POLICY = 19 }
        internal enum AccentState { ACCENT_DISABLED = 0, ACCENT_ENABLE_GRADIENT = 1, ACCENT_ENABLE_TRANSPARENTGRADIENT = 2, ACCENT_ENABLE_BLURBEHIND = 3, ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, ACCENT_INVALID_STATE = 5 }
        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy { public AccentState AccentState; public int AccentFlags; public int GradientColor; public int AnimationId; }
    }
}