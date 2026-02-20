using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WPFModuloCuadre.Models;

namespace WPFModuloCuadre.Services
{
    public class DataRepository
    {
        private const string DbName = "EstacionFinal.db";
        public string ConnectionString => $"Data Source={DbName}";

        public void InicializarBaseDeDatos()
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Open();

            db.Execute(@"
                CREATE TABLE IF NOT EXISTS Productos (Id INTEGER PRIMARY KEY, Nombre TEXT, Precio REAL, Tipo TEXT);
                CREATE TABLE IF NOT EXISTS Mangueras (Id INTEGER PRIMARY KEY, DispensadorNo INTEGER, Numero INTEGER, ProductoNombre TEXT, LecturaAnterior REAL);
                CREATE TABLE IF NOT EXISTS MediosPago (Id INTEGER PRIMARY KEY, Nombre TEXT, Categoria TEXT);
                CREATE TABLE IF NOT EXISTS TurnosGlobales (Id TEXT PRIMARY KEY, SucursalId INTEGER, Fecha TEXT, Turno TEXT, TotalVentaBombas REAL, Sincronizado INTEGER);
                CREATE TABLE IF NOT EXISTS Operarios ( Id TEXT PRIMARY KEY, Nombre TEXT NOT NULL, Rol TEXT DEFAULT 'Pista', Activo INTEGER DEFAULT 1);
                
                CREATE TABLE IF NOT EXISTS CuadresOperarios (
                    Id TEXT PRIMARY KEY, TurnoGlobalId TEXT, NombreOperario TEXT, Rol TEXT, 
                    MontoCombustibleAsignado REAL, MontoLubricantes REAL, MontoTienda REAL, 
                    TotalPagos REAL, Diferencia REAL, FOREIGN KEY(TurnoGlobalId) REFERENCES TurnosGlobales(Id)
                );

                -- NUEVA TABLA
                CREATE TABLE IF NOT EXISTS PagosHistorial (
                    Id TEXT PRIMARY KEY,
                    CuadreOperarioId TEXT,
                    Categoria TEXT,
                    Monto REAL,
                    FOREIGN KEY(CuadreOperarioId) REFERENCES CuadresOperarios(Id)
                );

                -- TABLA DE BORRADOR: Para guardar cambios sin cerrar el turno
                    CREATE TABLE IF NOT EXISTS BorradorOperarios (
                        OperarioId TEXT PRIMARY KEY,
                        MontoLubricantes REAL,
                        MontoTienda REAL,
                        MontoPagos TEXT, -- Se puede guardar como JSON o String para los múltiples medios de pago
                        FOREIGN KEY(OperarioId) REFERENCES Operarios(Id)
                    );

            ");

            InicializarDatosSemilla(db);
        }

        public bool ExisteCierre(DateTime fecha, string turno)
        {
            using var db = new SqliteConnection(ConnectionString);
            int count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM TurnosGlobales WHERE Fecha = @F AND Turno = @T AND SucursalId = @S",
                new { F = fecha.ToString("yyyy-MM-dd"), T = turno, S = ConfigService.SucursalId });
            return count > 0;
        }

        // Añadimos el parámetro dispensadores al final de la firma
        public bool GuardarTurnoCompleto(DateTime fecha, string turno, double totalPista, IEnumerable<OperadorCuadreVM> operarios, IEnumerable<DispensadorGroup> dispensadores)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Open();
            using var tran = db.BeginTransaction();

            try
            {
                string turnoId = Guid.NewGuid().ToString();

                // 1. Guardar Header (Sin cambios)
                db.Execute("INSERT INTO TurnosGlobales (Id, SucursalId, Fecha, Turno, TotalVentaBombas, Sincronizado) VALUES (@Id, @S, @F, @T, @V, 0)",
                    new { Id = turnoId, S = ConfigService.SucursalId, F = fecha.ToString("yyyy-MM-dd"), T = turno, V = totalPista }, tran);

                // MODIFICACIÓN: Agregamos 'OperarioId' a la tabla CuadresOperarios para mantener la referencia histórica
                string sqlOp = @"INSERT INTO CuadresOperarios 
                        (Id, TurnoGlobalId, OperarioId, NombreOperario, Rol, MontoCombustibleAsignado, MontoLubricantes, MontoTienda, TotalPagos, Diferencia) 
                        VALUES (@Id, @TId, @OpId, @Nom, @Rol, @Comb, @Lubs, @Tienda, @Pagos, @Dif)";

                string sqlPago = "INSERT INTO PagosHistorial (Id, CuadreOperarioId, Categoria, Monto) VALUES (@Id, @OpId, @Cat, @Monto)";

                // 2. Guardar Operarios y Pagos
                foreach (var op in operarios)
                {
                    string transaccionOpId = Guid.NewGuid().ToString();

                    // Guardamos tanto el op.Id (GUID del catálogo) como el op.Nombre (Texto)
                    db.Execute(sqlOp, new
                    {
                        Id = transaccionOpId,
                        TId = turnoId,
                        OpId = op.Id, // El ID que acabas de agregar a OperadorCuadreVM
                        Nom = op.Nombre,
                        Rol = op.Rol.ToString(),
                        Comb = op.AsignacionCombustible,
                        Lubs = op.TotalLubricantes,
                        Tienda = op.VentaTienda,
                        Pagos = op.TotalPagos,
                        Dif = op.Diferencia
                    }, tran);

                    foreach (var pago in op.Pagos)
                    {
                        if (pago.Monto > 0)
                        {
                            db.Execute(sqlPago, new { Id = Guid.NewGuid().ToString(), OpId = transaccionOpId, Cat = pago.Categoria, Monto = pago.Monto }, tran);
                        }
                    }
                }

                // 3. ACTUALIZAR LECTURAS (Sin cambios)
                string sqlUpdateManguera = "UPDATE Mangueras SET LecturaAnterior = @L WHERE Numero = @N AND ProductoNombre = @P";
                foreach (var disp in dispensadores)
                {
                    foreach (var m in disp.Mangueras)
                    {
                        db.Execute(sqlUpdateManguera, new { L = m.LecturaFinal, N = m.Numero, P = m.Producto }, tran);
                    }
                }

                tran.Commit();
                return true;
            }
            catch
            {
                tran.Rollback();
                return false;
            }
        }

        public bool GuardarEstadoBorrador(IEnumerable<OperadorCuadreVM> operarios)
        {
            using var db = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
            db.Open();
            using var tran = db.BeginTransaction();

            try
            {
                // 1. Limpiar borradores previos para iniciar carga fresca
                db.Execute("DELETE FROM BorradorOperarios", null, tran);

                foreach (var op in operarios)
                {
                    // 2. Guardar encabezado del borrador (Tienda y Lubricantes)
                    db.Execute(@"INSERT INTO BorradorOperarios 
                (OperarioId, MontoLubricantes, MontoTienda) 
                VALUES (@Id, @Lubs, @Tienda)",
                        new { Id = op.Id, Lubs = op.TotalLubricantes, Tienda = op.VentaTienda }, tran);

                    // 3. Guardar el desglose de pagos temporal (usaremos una tabla auxiliar o campo de texto)
                    // Para simplicidad, puedes reusar una lógica similar a PagosHistorial pero marcada como borrador
                }

                tran.Commit();
                return true;
            }
            catch
            {
                tran.Rollback();
                return false;
            }
        }

        /// <summary>
        /// Recupera todos los registros de la tabla de borradores.
        /// </summary>
        public List<BorradorDTO> ObtenerTodosLosBorradores()
        {
            using var db = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
            // Retornamos una lista de objetos simples (DTO) para mapearlos luego al ViewModel
            return db.Query<BorradorDTO>("SELECT OperarioId, MontoLubricantes, MontoTienda FROM BorradorOperarios").ToList();
        }

        /// <summary>
        /// Limpia la tabla de borradores. Se debe llamar al finalizar exitosamente un turno.
        /// </summary>
        /// <param name="db">Conexión abierta</param>
        /// <param name="tran">Transacción activa</param>
        public void LimpiarBorradores(Microsoft.Data.Sqlite.SqliteConnection db, Microsoft.Data.Sqlite.SqliteTransaction tran)
        {
            db.Execute("DELETE FROM BorradorOperarios", null, tran);
        }

        public List<OperadorCuadreVM> GetOperariosActivos(ObservableCollection<DispensadorGroup> dispensadores)
        {
            // Solo traemos los que no han sido "retirados" (Activo = 1)
            var lista = EjecutarQuery<dynamic>("SELECT Id, Nombre FROM Operarios WHERE Activo = 1");
            var resultado = new List<OperadorCuadreVM>();

            foreach (var item in lista)
            {
                resultado.Add(new OperadorCuadreVM(dispensadores)
                {
                    Id = item.Id, // Necesitas agregar la propiedad 'Id' a tu OperadorCuadreVM
                    Nombre = item.Nombre,
                    Rol = RolOperario.Pista
                });
            }
            return resultado;
        }

        public List<OperadorCuadreVM> GetOperariosCatalogo()
        {
            using var db = new SqliteConnection(ConnectionString);
            // Traemos Id, Nombre y Rol de los empleados que no han sido retirados
            return db.Query<OperadorCuadreVM>(
                "SELECT Id, Nombre, Rol FROM Operarios WHERE Activo = 1"
            ).ToList();
        }

        public void GuardarNuevoOperario(string id, string nombre, string rol)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Execute("INSERT INTO Operarios (Id, Nombre, Rol, Activo) VALUES (@Id, @Nom, @Rol, 1)",
                new { Id = id, Nom = nombre, Rol = rol });
        }

        public void ActualizarOperario(string id, string nombre, string rol)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Execute("UPDATE Operarios SET Nombre = @Nom, Rol = @Rol WHERE Id = @Id",
                new { Id = id, Nom = nombre, Rol = rol });
        }

        public void RetirarOperario(string id)
        {
            using var db = new SqliteConnection(ConnectionString);
            // Cambiamos Activo a 0 para que sea una "eliminación lógica"
            db.Execute("UPDATE Operarios SET Activo = 0 WHERE Id = @Id", new { Id = id });
        }

        // --- LECTURAS ---
        public List<LubricanteVM> GetLubricantes()
        {
            // Traemos los lubricantes y forzamos que la Cantidad inicial sea 0
            return EjecutarQuery<LubricanteVM>(
                "SELECT Nombre, Precio, 0 AS Cantidad FROM Productos WHERE Tipo='Lubricante'"
            );
        }
        public List<PagoItem> GetMediosPago()
        {
            // Traemos los métodos de pago (Efectivo, Tarjetas, etc.) y su categoría
            return EjecutarQuery<PagoItem>(
                "SELECT Nombre, Categoria FROM MediosPago"
            );
        }
        public List<Producto> GetPreciosCombustibles() => EjecutarQuery<Producto>("SELECT Nombre, Precio FROM Productos WHERE Tipo='Combustible'");
        public List<DispensadorGroup> GetPistaConfig()
        {
            var data = EjecutarQuery<dynamic>("SELECT m.DispensadorNo, m.Numero, m.ProductoNombre, m.LecturaAnterior, p.Precio FROM Mangueras m JOIN Productos p ON m.ProductoNombre = p.Nombre ORDER BY m.DispensadorNo, m.Numero");
            return data.GroupBy(x => (int)x.DispensadorNo).Select(g => new DispensadorGroup
            {
                Nombre = $"DISPENSADOR {g.Key:00}",
                Mangueras = new ObservableCollection<MangueraVM>(g.Select(m => new MangueraVM
                {
                    Numero = (int)m.Numero,
                    Producto = m.ProductoNombre,
                    Precio = (double)m.Precio,
                    LecturaInicial = (double)m.LecturaAnterior,
                    LecturaFinal = (double)m.LecturaAnterior
                }))
            }).ToList();
        }
        private List<T> EjecutarQuery<T>(string sql) { using var db = new SqliteConnection(ConnectionString); return db.Query<T>(sql).ToList(); }

        private void InicializarDatosSemilla(SqliteConnection db)
        {
            if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM Productos") == 0)
            {
                db.Execute("INSERT INTO Productos (Nombre, Precio, Tipo) VALUES ('Regular', 272.50, 'Combustible'), ('Premium', 290.10, 'Combustible'), ('Gasoil', 221.60, 'Combustible')");
                var lubs = new List<dynamic>();
                for (int i = 1; i <= 20; i++) lubs.Add(new { Nombre = $"Aceite Tipo {i}", Precio = 100.0 * i + 50 });
                db.Execute("INSERT INTO Productos (Nombre, Precio, Tipo) VALUES (@Nombre, @Precio, 'Lubricante')", lubs);
            }
            if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM Mangueras") == 0)
            {
                var mangueras = new List<dynamic>();
                string[] prods = { "Regular", "Premium", "Gasoil", "Regular", "Premium", "Gasoil" };
                // 16 Dispensadores x 6 Mangueras
                for (int disp = 1; disp <= 16; disp++)
                    for (int mang = 1; mang <= 6; mang++)
                        mangueras.Add(new { D = disp, N = mang, P = prods[mang - 1], L = 1000.0 * disp });
                db.Execute("INSERT INTO Mangueras (DispensadorNo, Numero, ProductoNombre, LecturaAnterior) VALUES (@D, @N, @P, @L)", mangueras);
            }
            if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM MediosPago") == 0)
            {
                db.Execute("INSERT INTO MediosPago (Nombre, Categoria) VALUES ('Efectivo', 'TIPOS'), ('Cheque', 'TIPOS'), ('Visa', 'TARJETAS'), ('Mastercard', 'TARJETAS'), ('Cardnet', 'TARJETAS'), ('Bonos', 'OTROS'), ('Credito', 'OTROS')");
            }
        }

        public List<TurnoHeaderDTO> ObtenerHistorialTurnos()
        {
            using var db = new SqliteConnection(ConnectionString);
            // Traemos los últimos 50 turnos ordenados por fecha descendente
            return db.Query<TurnoHeaderDTO>(@"
                SELECT Id, Fecha, Turno, TotalVentaBombas 
                FROM TurnosGlobales 
                WHERE SucursalId = @SucId 
                ORDER BY Fecha DESC, Turno DESC 
                LIMIT 50",
                new { SucId = ConfigService.SucursalId }).ToList();
        }

        public List<OperarioReporteDTO> ObtenerDetalleTurno(string turnoGlobalId)
        {
            using var db = new SqliteConnection(ConnectionString);

            // 1. Traer Operarios
            var operarios = db.Query<OperarioReporteDTO>(@"
                SELECT Id, NombreOperario, Rol, MontoCombustibleAsignado, MontoLubricantes, MontoTienda, TotalPagos, Diferencia
                FROM CuadresOperarios
                WHERE TurnoGlobalId = @Id",
                new { Id = turnoGlobalId }).ToList();

            // 2. Para cada operario, buscar sus pagos agrupados por categoría
            foreach (var op in operarios)
            {
                var pagos = db.Query<PagoHistorialDTO>(@"
                    SELECT Categoria, SUM(Monto) as Monto 
                    FROM PagosHistorial 
                    WHERE CuadreOperarioId = @OpId 
                    GROUP BY Categoria",
                    new { OpId = op.Id }).ToList();

                op.PagosDetallados = pagos;
            }

            return operarios;
        }
    }
}