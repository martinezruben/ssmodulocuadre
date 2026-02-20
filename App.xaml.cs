using System.Windows;
using WPFModuloCuadre.Services;

namespace WPFModuloCuadre
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Inicializar DB
            var repo = new DataRepository();
            repo.InicializarBaseDeDatos();

            // 2. Abrir ventana principal
            var window = new MainWindow();

            // CORRECCIÓN: El constructor de MainViewModel NO debe pedir parámetros 
            // si se está instanciando de esta forma.
            window.DataContext = new ViewModels.MainViewModel();

            window.Show();
        }
    }
}