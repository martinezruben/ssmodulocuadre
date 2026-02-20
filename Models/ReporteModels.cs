using System.Collections.Generic;

namespace WPFModuloCuadre.Models
{
    // DTO para el detalle de pago en el historial
    public class PagoHistorialDTO
    {
        public string Categoria { get; set; }
        public double Monto { get; set; }
    }

    // DTO del Operario (Agregamos la lista de pagos)
    public class OperarioReporteDTO
    {
        public string Id { get; set; } // Necesario para buscar sus pagos
        public string NombreOperario { get; set; }
        public string Rol { get; set; }
        public double MontoCombustibleAsignado { get; set; }
        public double MontoLubricantes { get; set; }
        public double MontoTienda { get; set; }
        public double TotalPagos { get; set; }
        public double Diferencia { get; set; }

        // NUEVO: Lista de pagos detallada
        public List<PagoHistorialDTO> PagosDetallados { get; set; } = new();

        public double TotalVenta => MontoCombustibleAsignado + MontoLubricantes + MontoTienda;
    }

    public class TurnoHeaderDTO
    {
        public string Id { get; set; }
        public string Fecha { get; set; }
        public string Turno { get; set; }
        public double TotalVentaBombas { get; set; }
        public string DisplayTitle => $"{System.DateTime.Parse(Fecha):dd/MM/yyyy} - {Turno}";
    }
}