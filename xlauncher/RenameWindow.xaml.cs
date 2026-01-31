using System.Windows;
using System.Windows.Input;

namespace xlauncher
{
    public partial class RenameWindow : Window
    {
        public string NewName { get; private set; }

        public RenameWindow(string currentName)
        {
            InitializeComponent();
            NameInput.Text = currentName;
            NameInput.Focus();
            NameInput.SelectAll();
        }

        // Permitir arrastrar ventana
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(NameInput.Text))
            {
                NewName = NameInput.Text.Trim();
                DialogResult = true; // Cierra la ventana devolviendo true
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}