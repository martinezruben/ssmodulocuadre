using System.Windows;

namespace WPFModuloCuadre
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // NO asignes el DataContext aquí. Ya se asigna en App.xaml.cs.
            // Si lo haces aquí, creas un segundo ViewModel vacío.
        }
    }
}