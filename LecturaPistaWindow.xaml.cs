using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WPFModuloCuadre
{
    public partial class LecturaPistaWindow : Window
    {
        public LecturaPistaWindow()
        {
            InitializeComponent();

            // Suscribimos el evento Loaded para dar foco al abrir la ventana
            this.Loaded += LecturaPistaWindow_Loaded;
        }

        private void LecturaPistaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Buscamos el primer TextBox generado dinámicamente en la interfaz
            TextBox firstTextBox = FindVisualChild<TextBox>(this);
            if (firstTextBox != null)
            {
                firstTextBox.Focus(); // Le da el foco
            }
        }

        // EVENTO 1: Seleccionar todo el texto al entrar a la casilla
        private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // El Dispatcher asegura que la selección ocurra DESPUÉS de que WPF 
                // procese el clic del mouse, evitando que el cursor quite la selección.
                textBox.Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
            }
        }

        // EVENTO 2: Usar ENTER para saltar a la siguiente casilla
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // Evita el sonido de "ding" de Windows

                // Simula presionar la tecla TAB para mover el foco al siguiente control
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                if (sender is UIElement element)
                {
                    element.MoveFocus(request);
                }
            }
        }

        // EVENTO 3: Botón de Guardado
        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        // --- MÉTODO AUXILIAR ---
        // Escanea el árbol visual de WPF para encontrar controles anidados
        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }
    }
}