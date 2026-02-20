using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq; // IMPORTANTE: Necesario para usar .OfType y .Sum
using System.Windows.Data;
using System.Windows.Media;
using WPFModuloCuadre.Models;

// EL NAMESPACE DEBE SER EXACTAMENTE ESTE:
namespace WPFModuloCuadre.Converters
{
    // Converter de Color
    public class DiferenciaColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double diff) return diff < -1.0 ? Brushes.Red : Brushes.DarkGreen;
            return Brushes.Black;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // Converter de Totales de Grupo (Normal)
    public class GroupTotalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // WPF envía un ReadOnlyObservableCollection con los items del grupo actual
            if (value is ReadOnlyObservableCollection<object> items)
            {
                // Filtramos los items para asegurarnos de que son del tipo PagoItem
                // y sumamos la propiedad Monto. 
                return items.OfType<PagoItem>().Sum(pago => pago.Monto);
            }

            return 0.0; // Valor por defecto si la colección está vacía o es inválida
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // NUEVO: MultiConverter para forzar la actualización de totales de grupo en tiempo real
    public class GroupTotalMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] es la colección de Items del grupo (ReadOnlyObservableCollection)
            // values[1] es la propiedad TotalPagos (solo se pasa como "disparador" para que WPF vuelva a sumar)

            if (values != null && values.Length > 0 && values[0] is ReadOnlyObservableCollection<object> items)
            {
                // Filtra y suma los elementos usando la clase PagoItem
                return items.OfType<PagoItem>().Sum(p => p.Monto);
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}