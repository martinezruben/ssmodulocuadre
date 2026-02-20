using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace WPFModuloCuadre.Models
{
    // Fix: Agregados roles faltantes para evitar error CS0117
    public enum RolOperario { Tienda, Pista }

    // Fix: Agregada clase Producto para evitar error CS0246
    public class Producto
    {
        public string Nombre { get; set; }
        public double Precio { get; set; }
    }

    public class BorradorDTO { 
        public string OperarioId { get; set; } 
        public double MontoLubricantes { get; set; } 
        public double MontoTienda { get; set; } 
    }

    public class CombustibleResumen
    {
        public string NombreCombustible { get; set; }
        public double GalonesOperario { get; set; }
        public double GalonesTurno { get; set; }
        public double ValorOperario { get; set; }
        public double ValorTurno { get; set; }
    }

    public partial class MangueraVM : ObservableObject
    {
        public int Numero { get; set; }
        public string Producto { get; set; }
        public double Precio { get; set; }
        public double LecturaInicial { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Subtotal))]
        [NotifyPropertyChangedFor(nameof(Galones))]
        private double _lecturaFinal;

        [ObservableProperty]
        private OperadorCuadreVM _operador;

        public double Galones => (LecturaFinal > LecturaInicial) ? (LecturaFinal - LecturaInicial) : 0;
        public double Subtotal => Galones * Precio;
    }

    public partial class DispensadorGroup : ObservableObject
    {
        public string Nombre { get; set; }
        public ObservableCollection<MangueraVM> Mangueras { get; set; } = new();
        public double TotalDispensador => Mangueras.Sum(m => m.Subtotal);
        public double TotalGalones => Mangueras.Sum(m => m.Galones);

        /// <summary>
        /// Método público para permitir que el ViewModel notifique 
        /// cambios en las propiedades calculadas.
        /// </summary>
        public void RefrescarCalculos()
        {
            // Notifica a la UI que debe volver a leer estas propiedades
            OnPropertyChanged(nameof(TotalDispensador));
            OnPropertyChanged(nameof(TotalGalones));
        }
    }

    public partial class OperadorCuadreVM : ObservableObject
    {
        [ObservableProperty] private string _id;
        [ObservableProperty] private string _nombre = "Nuevo Operario";
        [ObservableProperty] private RolOperario _rol = RolOperario.Pista;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalAEntregar))]
        [NotifyPropertyChangedFor(nameof(Diferencia))]
        private double _ventaTienda;

        public ObservableCollection<PagoItem> Pagos { get; set; } = new();
        public ObservableCollection<LubricanteVM> Lubricantes { get; set; } = new();
        public ObservableCollection<DispensadorGroup> DispensadoresReferencia { get; set; }

        public OperadorCuadreVM()
        {
            InicializarSuscripciones();
        }

        public OperadorCuadreVM(ObservableCollection<DispensadorGroup> manguerasGlobales) : this()
        {
            DispensadoresReferencia = manguerasGlobales;
        }

        private void InicializarSuscripciones()
        {
            Lubricantes.CollectionChanged += Lubricantes_CollectionChanged;
            Pagos.CollectionChanged += Pagos_CollectionChanged;
        }

        public double AsignacionCombustible => (DispensadoresReferencia == null) ? 0 :
            DispensadoresReferencia.SelectMany(d => d.Mangueras)
                                   .Where(m => m.Operador == this)
                                   .Sum(m => m.Subtotal);

        public double TotalLubricantes => Lubricantes.Sum(l => l.Subtotal);
        public double TotalPagos => Pagos.Sum(p => p.Monto);
        public double TotalAEntregar => AsignacionCombustible + TotalLubricantes + VentaTienda;
        public double Diferencia => TotalPagos - TotalAEntregar;

        public IEnumerable<CombustibleResumen> ResumenCombustible
        {
            get
            {
                if (DispensadoresReferencia == null) return new List<CombustibleResumen>();
                return DispensadoresReferencia.SelectMany(d => d.Mangueras)
                    .GroupBy(m => m.Producto)
                    .Select(g => new CombustibleResumen
                    {
                        NombreCombustible = g.Key,
                        GalonesOperario = g.Where(m => m.Operador == this).Sum(m => m.Galones),
                        GalonesTurno = g.Sum(m => m.Galones),
                        ValorOperario = g.Where(m => m.Operador == this).Sum(m => m.Subtotal),
                        ValorTurno = g.Sum(m => m.Subtotal)
                    });
            }
        }

        public void NotificarCambios()
        {
            OnPropertyChanged(nameof(AsignacionCombustible));
            OnPropertyChanged(nameof(TotalLubricantes));
            OnPropertyChanged(nameof(TotalPagos));
            OnPropertyChanged(nameof(TotalAEntregar));
            OnPropertyChanged(nameof(Diferencia));
            OnPropertyChanged(nameof(ResumenCombustible));
        }

        // --- LÓGICA DE EVENTOS (MAGIA PARA ACTUALIZAR TOTALES AL TECLEAR) ---

        private void Lubricantes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (LubricanteVM item in e.NewItems)
                    item.PropertyChanged += Lubricante_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (LubricanteVM item in e.OldItems)
                    item.PropertyChanged -= Lubricante_PropertyChanged;
            }
        }

        private void Lubricante_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Si el usuario modifica la cantidad, se recalcula todo en cadena
            if (e.PropertyName == nameof(LubricanteVM.Cantidad) ||
                e.PropertyName == nameof(LubricanteVM.Subtotal))
            {
                OnPropertyChanged(nameof(TotalLubricantes));
                OnPropertyChanged(nameof(TotalAEntregar));
                OnPropertyChanged(nameof(Diferencia));
            }
        }

        private void Pagos_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (PagoItem item in e.NewItems)
                    item.PropertyChanged += Pago_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (PagoItem item in e.OldItems)
                    item.PropertyChanged -= Pago_PropertyChanged;
            }
        }

        private void Pago_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Si el usuario modifica un pago, actualiza el balance general y el total de pagos
            if (e.PropertyName == nameof(PagoItem.Monto))
            {
                OnPropertyChanged(nameof(TotalPagos));
                OnPropertyChanged(nameof(Diferencia));
            }
        }
    }

    public partial class PagoItem : ObservableObject
    {
        [ObservableProperty]
        private string categoria; // "PAGOS", "BANCOS", "TARJETAS"

        [ObservableProperty]
        private string nombre;    // "Efectivo", "AZUL", "AMEX", etc.

        [ObservableProperty]
        private double monto;     // El valor que el usuario digita en el TextBox
    }

    public partial class LubricanteVM : ObservableObject
    {
        public string Nombre { get; set; }
        public double Precio { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Subtotal))]
        private int _cantidad;

        public double Subtotal => Cantidad * Precio;
    }
}