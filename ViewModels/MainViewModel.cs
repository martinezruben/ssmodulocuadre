using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WPFModuloCuadre.Models;
using WPFModuloCuadre.Services;

namespace WPFModuloCuadre.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // --- PROPIEDADES DE ESTADO ---

        [ObservableProperty]
        private string _tituloVentana = "Cierre de Turno - Estación Central";

        [ObservableProperty]
        private DateTime _fechaActual = DateTime.Now;

        [ObservableProperty]
        private string _turnoSeleccionado;

        private readonly DataRepository _dbRepo = new DataRepository();

        public ObservableCollection<string> TurnosDisponibles { get; } = new()
        {
            "Mañana (06:00 - 14:00)",
            "Tarde (14:00 - 22:00)",
            "Noche (22:00 - 06:00)"
        };

        // --- COLECCIONES PRINCIPALES ---

        public ObservableCollection<DispensadorGroup> Dispensadores { get; set; } = new();
        public ObservableCollection<OperadorCuadreVM> OperadoresView { get; set; } = new();

        [ObservableProperty]
        private OperadorCuadreVM? _operadorSeleccionado;

        // --- TOTALES GLOBALES ---

        public double TotalVentaPista => Dispensadores.Sum(d => d.TotalDispensador);
        public double PendienteDeAsignar => TotalVentaPista - OperadoresView.Sum(o => o.AsignacionCombustible);
        public double DiferenciaTotalTurno => OperadoresView.Sum(o => o.Diferencia);

        // --- CONSTRUCTOR ---
        public MainViewModel()
        {
            // 1. Inicializar el estado básico
            _turnoSeleccionado = TurnosDisponibles[0];
            var db = new DataRepository();

            // 2. CARGA DE CONFIGURACIÓN DE PISTA
            // Reemplazamos la simulación por la data real de mangueras y dispensadores
            var pistaConfig = db.GetPistaConfig();
            Dispensadores.Clear();
            foreach (var disp in pistaConfig)
            {
                Dispensadores.Add(disp);
            }

            // 3. CARGA DE OPERARIOS DESDE LA DB
            // Invocamos el método de refresco que ya no es destructivo
            RefrescarOperariosDesdeDB();

            // 4. ESTADO INICIAL DE LA VISTA
            OperadorSeleccionado = OperadoresView.FirstOrDefault();

            // 5. SUSCRIPCIÓN DE EVENTOS
            // Esto asegura que los cálculos se actualicen al escribir
            foreach (var op in OperadoresView)
            {
                SuscribirCambiosOperador(op);
            }

            // 6. CÁLCULO INICIAL
            RecalcularTotalesGlobales();
        }

        // --- COMANDOS ---

        [RelayCommand]
        private void AgregarOperador()
        {
            // 1. Solicitar el nombre del nuevo operario (puedes usar un diálogo personalizado o InputBox)
            string nombrePrompt = Microsoft.VisualBasic.Interaction.InputBox("Ingrese el nombre completo del operario:", "Registrar Operario", "");

            if (string.IsNullOrWhiteSpace(nombrePrompt)) return;

            var nuevoId = Guid.NewGuid().ToString();
            var db = new DataRepository();

            // 2. Guardar permanentemente en la base de datos (Catálogo)
            // Asumimos que tienes un método 'GuardarNuevoOperario' en tu repositorio
            db.GuardarNuevoOperario(nuevoId, nombrePrompt, "Pista");

            // 3. Crear el objeto para la sesión actual
            var nuevoOp = new OperadorCuadreVM(Dispensadores)
            {
                Id = nuevoId, // Importante: Asignar el ID de la DB
                Nombre = nombrePrompt,
                Rol = RolOperario.Pista
            };

            // 4. Cargar catálogos (Clonamos lubricantes y pagos)
            foreach (var lub in db.GetLubricantes())
            {
                nuevoOp.Lubricantes.Add(new LubricanteVM { Nombre = lub.Nombre, Precio = lub.Precio, Cantidad = 0 });
            }

            foreach (var pago in db.GetMediosPago())
            {
                nuevoOp.Pagos.Add(new PagoItem { Categoria = pago.Categoria, Nombre = pago.Nombre, Monto = 0 });
            }

            // 5. Agregar a la vista actual
            OperadoresView.Add(nuevoOp);
            OperadorSeleccionado = nuevoOp;
        }

        [RelayCommand]
        private void EliminarOperador(OperadorCuadreVM? op)
        {
            if (op == null) return;

            var confirm = MessageBox.Show($"¿Está seguro de retirar a {op.Nombre} del sistema?\n\nNo aparecerá en futuros turnos, pero sus cuadres históricos se conservarán.",
                                          "Retirar Operario", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var db = new DataRepository();

            // 1. Desasignar de las mangueras en el turno actual
            foreach (var d in Dispensadores)
                foreach (var m in d.Mangueras)
                    if (m.Operador == op) m.Operador = null;

            // 2. Marcar como inactivo en la Base de Datos para que no vuelva a cargar
            // Asumimos que tienes el método 'RetirarOperario' en tu repositorio
            db.RetirarOperario(op.Id);

            // 3. Quitar de la lista visual
            OperadoresView.Remove(op);
            RecalcularTotalesGlobales();
        }

        [RelayCommand]
        private void GuardarTurno()
        {
            // Validaciones
            if (OperadoresView == null || OperadoresView.Count == 0)
            {
                MessageBox.Show("No hay operarios para guardar en este turno.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(TurnoSeleccionado))
            {
                MessageBox.Show("Debes seleccionar un turno (A, B, C...).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_dbRepo.ExisteCierre(FechaActual, TurnoSeleccionado))
            {
                MessageBox.Show($"Ya existe un cierre guardado para la fecha {FechaActual:dd/MM/yyyy} en el Turno {TurnoSeleccionado}.", "Turno Duplicado", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            var confirm = MessageBox.Show($"¿Estás seguro de cerrar el Turno {TurnoSeleccionado} con un balance de {DiferenciaTotalTurno:C}?", "Confirmar Cierre", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            // Ejecutar guardado (Nota: Ahora pasamos 'Dispensadores' como último parámetro)
            bool guardadoExitoso = _dbRepo.GuardarTurnoCompleto(FechaActual, TurnoSeleccionado, TotalVentaPista, OperadoresView, Dispensadores);

            if (guardadoExitoso)
            {
                MessageBox.Show("¡Turno cerrado y guardado exitosamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                // Llamamos a la función que resetea la pantalla
                PrepararNuevoTurno();
            }
            else
            {
                MessageBox.Show("Ocurrió un error al intentar guardar el turno.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void GuardarBorrador()
        {
            var db = new DataRepository();

            // Intentamos persistir el estado actual de la lista de operarios
            bool exito = db.GuardarEstadoBorrador(OperadoresView);

            if (exito)
            {
                MessageBox.Show("Cambios guardados correctamente como borrador.", "Seguro de Datos", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Hubo un error al intentar guardar el borrador.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

[RelayCommand]
private void CerrarTurno()
{
    var confirm = MessageBox.Show("¿Está seguro de finalizar el turno? Se actualizarán las lecturas iniciales y se limpiará la pantalla.", 
                                  "Confirmar Cierre", MessageBoxButton.YesNo, MessageBoxImage.Question);
    
    if (confirm != MessageBoxResult.Yes) return;

    var db = new DataRepository();
    
    // 1. Ejecutar el guardado histórico y actualización de lecturas
    bool exitoCierre = db.GuardarTurnoCompleto(FechaActual, TurnoSeleccionado, TotalVentaPista, OperadoresView, Dispensadores);

    if (exitoCierre)
    {
        // 2. IMPORTANTE: Si el cierre fue exitoso, el borrador ya no es necesario
        // Podemos hacerlo dentro de GuardarTurnoCompleto o aquí mediante un nuevo método
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            using (var tran = conn.BeginTransaction())
            {
                db.LimpiarBorradores(conn, tran);
                tran.Commit();
            }
        }

        MessageBox.Show("Turno cerrado exitosamente.", "Cierre Finalizado", MessageBoxButton.OK, MessageBoxImage.Information);
        
        // 3. Reiniciar la aplicación o limpiar la vista para el nuevo turno
        RefrescarOperariosDesdeDB(); 
    }
}

        private void CargarBorradoresSiExisten()
        {
            var db = new DataRepository();
            // Consultamos la tabla BorradorOperarios
            var borradores = db.ObtenerTodosLosBorradores();

            foreach (var b in borradores)
            {
                var op = OperadoresView.FirstOrDefault(o => o.Id == b.OperarioId);
                if (op != null)
                {
                    op.VentaTienda = b.MontoTienda;
                    // Aquí cargarías también los montos de los pagos guardados
                    // y las cantidades de lubricantes si decides persistirlas a ese nivel.
                }
            }
        }

        private void PrepararNuevoTurno()
        {
            // 1. Limpiar los vendedores de la pantalla
            OperadoresView.Clear();
            OperadorSeleccionado = null;

            // 2. Recargar dispensadores frescos desde la Base de Datos
            // Como el repositorio acaba de actualizar la LecturaAnterior, al consultar 
            // de nuevo, la LecturaInicial será exactamente la que dejaron, y la 
            // venta/galones arrancará matemáticamente en 0.
            var pistaFresca = _dbRepo.GetPistaConfig();
            Dispensadores.Clear();
            foreach (var d in pistaFresca)
            {
                Dispensadores.Add(d);
            }

            // 3. Limpiar variables de cabecera
            TurnoSeleccionado = null;

            // 4. Forzar el recálculo general (Todo quedará en $0.00)
            RecalcularTotalesGlobales();
        }

        [RelayCommand]
        private void VerHistorial()
        {
            var win = new HistorialWindow();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        [RelayCommand]
        private void AbrirLecturas()
        {
            // CORRECCIÓN CS1729: Asegúrate que LecturaPistaWindow tenga un constructor 
            // que acepte MainViewModel o usa el constructor vacío y asigna DataContext.
            var ventana = new LecturaPistaWindow();
            ventana.DataContext = this;

            if (Application.Current.MainWindow != null)
                ventana.Owner = Application.Current.MainWindow;

            ventana.ShowDialog();

            RecalcularTotalesGlobales();

            // CORRECCIÓN CS1540: En lugar de llamar a OnPropertyChanged (que es protegido),
            // llamamos a un método público en el modelo o simplemente notificamos el cambio aquí.
            foreach (var disp in Dispensadores)
            {
                disp.RefrescarCalculos(); // Este método debe ser PUBLIC en DispensadorGroup
            }
        }

        [RelayCommand]
        private void AdministrarEmpleados()
        {
            var win = new AdminEmpleadosWindow();
            win.Owner = Application.Current.MainWindow;

            // Mostramos la ventana de forma modal
            win.ShowDialog();

            // Al cerrar la ventana, refrescamos la lista de la pantalla principal
            RefrescarOperariosDesdeDB();
        }

        private void RefrescarOperariosDesdeDB()
        {
            var db = new DataRepository();
            var operariosEnDB = db.GetOperariosCatalogo();

            // 1. ELIMINAR: Quitar de la pantalla a los que ya no están activos
            var idsEnDB = operariosEnDB.Select(o => o.Id).ToHashSet(); // HashSet es más rápido para búsquedas
            var paraEliminar = OperadoresView.Where(ov => !idsEnDB.Contains(ov.Id)).ToList();

            foreach (var op in paraEliminar)
            {
                // Desasignar mangueras
                foreach (var d in Dispensadores)
                    foreach (var m in d.Mangueras)
                        if (m.Operador == op) m.Operador = null;

                OperadoresView.Remove(op);
            }

            // 2. AGREGAR O ACTUALIZAR
            foreach (var opDB in operariosEnDB)
            {
                var existente = OperadoresView.FirstOrDefault(ov => ov.Id == opDB.Id);

                if (existente == null)
                {
                    var nuevoOp = new OperadorCuadreVM(Dispensadores)
                    {
                        Id = opDB.Id,
                        Nombre = opDB.Nombre,
                        Rol = opDB.Rol
                    };

                    // Cargar catálogos iniciales con valores en cero
                    foreach (var lub in db.GetLubricantes())
                        nuevoOp.Lubricantes.Add(new LubricanteVM { Nombre = lub.Nombre, Precio = lub.Precio, Cantidad = 0 });

                    foreach (var pago in db.GetMediosPago())
                        nuevoOp.Pagos.Add(new PagoItem { Nombre = pago.Nombre, Categoria = pago.Categoria, Monto = 0 });

                    // IMPORTANTE: Suscribir para que los cálculos globales funcionen
                    SuscribirCambiosOperador(nuevoOp);

                    OperadoresView.Add(nuevoOp);
                }
                else
                {
                    // Actualizar datos básicos sin resetear lo digitado
                    existente.Nombre = opDB.Nombre;
                    existente.Rol = opDB.Rol;
                }
            }

            // 3. VALIDAR SELECCIÓN: Si el seleccionado ya no existe, marcar el primero
            if (OperadorSeleccionado == null || !OperadoresView.Contains(OperadorSeleccionado))
            {
                OperadorSeleccionado = OperadoresView.FirstOrDefault();
            }

            RecalcularTotalesGlobales();
        }

        // --- MÉTODOS AUXILIARES ---

        private void SuscribirCambiosOperador(OperadorCuadreVM op)
        {
            op.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OperadorCuadreVM.Diferencia) ||
                    e.PropertyName == nameof(OperadorCuadreVM.AsignacionCombustible))
                {
                    OnPropertyChanged(nameof(DiferenciaTotalTurno));
                    OnPropertyChanged(nameof(PendienteDeAsignar));
                }
            };
        }

        public void RecalcularTotalesGlobales()
        {
            OnPropertyChanged(nameof(TotalVentaPista));
            OnPropertyChanged(nameof(PendienteDeAsignar));
            OnPropertyChanged(nameof(DiferenciaTotalTurno));

            foreach (var op in OperadoresView)
            {
                op.NotificarCambios();
            }
        }
    }
}