using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WPFModuloCuadre.Models;
using WPFModuloCuadre.Services;

namespace WPFModuloCuadre.ViewModels
{
    public partial class HistorialViewModel : ObservableObject
    {
        // Lista Lateral (Fechas)
        public ObservableCollection<TurnoHeaderDTO> ListaTurnos { get; } = new();

        // Turno Seleccionado
        private TurnoHeaderDTO _turnoSeleccionado;
        public TurnoHeaderDTO TurnoSeleccionado
        {
            get => _turnoSeleccionado;
            set
            {
                if (SetProperty(ref _turnoSeleccionado, value))
                {
                    CargarDetalle(value?.Id);
                }
            }
        }

        // Lista Principal (Tarjetas de Operarios)
        public ObservableCollection<OperarioReporteDTO> DetalleOperarios { get; } = new();

        // Totales Visuales del Reporte
        [ObservableProperty] private double _totalDiferenciaTurno;
        [ObservableProperty] private double _totalVentaTurno;

        public HistorialViewModel()
        {
            CargarLista();
        }

        private void CargarLista()
        {
            var repo = new DataRepository();
            var datos = repo.ObtenerHistorialTurnos();
            foreach (var item in datos) ListaTurnos.Add(item);
        }

        private void CargarDetalle(string turnoId)
        {
            DetalleOperarios.Clear();
            TotalDiferenciaTurno = 0;
            TotalVentaTurno = 0;

            if (string.IsNullOrEmpty(turnoId)) return;

            var repo = new DataRepository();
            var detalles = repo.ObtenerDetalleTurno(turnoId);

            double diff = 0;
            double venta = 0;

            foreach (var op in detalles)
            {
                DetalleOperarios.Add(op);
                diff += op.Diferencia;
                venta += op.TotalVenta;
            }

            TotalDiferenciaTurno = diff;
            TotalVentaTurno = venta;
        }
    }
}