using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WPFModuloCuadre.Models;

namespace WPFModuloCuadre.Services
{
    public static class DataSimulationService
    {
        public static void CargarEntornoCompleto(
            ObservableCollection<DispensadorGroup> dispensadores,
            ObservableCollection<OperadorCuadreVM> operadores)
        {
            // Instanciamos el repositorio para conectar con SQLite
            var dbRepo = new DataRepository();

            // 1. CARGAR DISPENSADORES Y MANGUERAS DESDE LA BASE DE DATOS
            var pistaConfig = dbRepo.GetPistaConfig();
            dispensadores.Clear();
            foreach (var disp in pistaConfig)
            {
                dispensadores.Add(disp);
            }

            // 2. OBTENER CATÁLOGOS MAESTROS DESDE LA BASE DE DATOS
            var catalogoLubricantes = dbRepo.GetLubricantes();
            var catalogoPagos = dbRepo.GetMediosPago();

            // 3. CREAR OPERARIOS (Mantenemos nombres de prueba, pero con productos reales)
            var nombresOperarios = new[] { "Juan Pérez", "María Rodríguez", "Pedro Martínez", "Ana García" };
            var random = new Random();

            foreach (var nombre in nombresOperarios)
            {
                var op = new OperadorCuadreVM(dispensadores)
                {
                    Nombre = nombre,
                    Rol = RolOperario.Pista
                };

                // Clonar lubricantes de la DB para este operario
                foreach (var lub in catalogoLubricantes)
                {
                    op.Lubricantes.Add(new LubricanteVM
                    {
                        Nombre = lub.Nombre,
                        Precio = lub.Precio,
                        Cantidad = 0
                    });
                }

                // Clonar métodos de pago de la DB para este operario
                foreach (var pago in catalogoPagos)
                {
                    op.Pagos.Add(new PagoItem
                    {
                        Categoria = pago.Categoria,
                        Nombre = pago.Nombre, // Asegúrate de que el Query en DataRepository trae el 'Nombre' y no '_nombre'
                        Monto = 0
                    });
                }

                operadores.Add(op);
            }

            // 4. ASIGNAR OPERARIOS ALEATORIAMENTE A LAS MANGUERAS
            // (Para que los totales calculen algo al iniciar, en la vida real el usuario los asigna)
            foreach (var disp in dispensadores)
            {
                foreach (var manguera in disp.Mangueras)
                {
                    manguera.Operador = operadores[random.Next(operadores.Count)];
                }
            }
        }
    }
}