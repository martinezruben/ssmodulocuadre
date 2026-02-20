using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WPFModuloCuadre.Models;
using WPFModuloCuadre.Services;

namespace WPFModuloCuadre
{
    public partial class HistorialWindow : Window
    {
        private DataRepository _repo = new DataRepository();

        public List<TurnoHeaderDTO> Turnos { get; set; }

        public HistorialWindow()
        {
            InitializeComponent();
            CargarListaTurnos();
            this.DataContext = this;
        }

        private void CargarListaTurnos()
        {
            // Busca los últimos 50 turnos en SQLite
            Turnos = _repo.ObtenerHistorialTurnos();
            ListTurnos.ItemsSource = Turnos;
        }

        private void ListTurnos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListTurnos.SelectedItem is TurnoHeaderDTO seleccionado)
            {
                // Actualiza la cabecera del reporte
                TxtTotalPista.Text = seleccionado.TotalVentaBombas.ToString("C");

                // Busca el detalle profundo (operarios y sus pagos agrupados)
                var detalle = _repo.ObtenerDetalleTurno(seleccionado.Id);
                ItemsDetalleOperarios.ItemsSource = detalle;
            }
        }
    }
}