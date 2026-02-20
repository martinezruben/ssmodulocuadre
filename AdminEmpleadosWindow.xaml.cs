using System.Windows;
using System.Windows.Controls;
using WPFModuloCuadre.Models;
using WPFModuloCuadre.Services;


namespace WPFModuloCuadre 
{
    public partial class AdminEmpleadosWindow : Window
    {
        private DataRepository _repo = new DataRepository();
        private OperadorCuadreVM _empleadoSeleccionado;

        public AdminEmpleadosWindow()
        {
            InitializeComponent();
            RefrescarLista();
        }

        private void RefrescarLista()
        {
            // Cargamos los operarios activos de la DB
            ListEmpleados.ItemsSource = _repo.GetOperariosCatalogo();
            LimpiarFormulario();
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text)) return;

            string rolStr = (ComboRol.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (_empleadoSeleccionado == null) // Es NUEVO
            {
                _repo.GuardarNuevoOperario(Guid.NewGuid().ToString(), TxtNombre.Text, rolStr);
            }
            else // Es EDICIÓN
            {
                _repo.ActualizarOperario(_empleadoSeleccionado.Id, TxtNombre.Text, rolStr);
            }

            RefrescarLista();
            MessageBox.Show("Datos sincronizados con la base de datos.");
        }

        private void ListEmpleados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _empleadoSeleccionado = ListEmpleados.SelectedItem as OperadorCuadreVM;
            if (_empleadoSeleccionado != null)
            {
                TxtNombre.Text = _empleadoSeleccionado.Nombre;
                ComboRol.Text = _empleadoSeleccionado.Rol.ToString();
            }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_empleadoSeleccionado == null) return;
            _repo.RetirarOperario(_empleadoSeleccionado.Id);
            RefrescarLista();
        }

        private void BtnNuevo_Click(object sender, RoutedEventArgs e) => LimpiarFormulario();

        private void LimpiarFormulario()
        {
            _empleadoSeleccionado = null;
            TxtNombre.Text = "";
            ComboRol.SelectedIndex = 0;
        }
    }
}