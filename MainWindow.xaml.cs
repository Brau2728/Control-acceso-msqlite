using System;
using System.Windows;
using prueva1;
using prueba1;

namespace prueba1
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Simula el escaneo de huella llamando al ViewModel
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                // Se actualiza al método ProcesarLectura según tu lógica actual
                vm.ProcesarLectura();
            }
        }

        // Manejo de inicio de sesión con validación de roles
        private void OpenLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow login = new LoginWindow();
            
            // Si el diálogo se cierra con éxito (autenticado)
            if (login.ShowDialog() == true)
            {
                if (login.RolUsuario == "Administrador")
                {
                    // Solo el administrador accede al CRUD completo
                    PanelAdminWindow panelAdmin = new PanelAdminWindow();
                    panelAdmin.ShowDialog(); 
                }
                else
                {
                    // El guardia recibe confirmación y el sistema queda listo para novedades
                    MessageBox.Show("Sesión iniciada como Guardia. Sistema listo para registro de novedades.", "Acceso Autorizado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // Abre la ventana de registro de personal para pruebas
        private void Test_Click(object sender, RoutedEventArgs e)
        {
            RegistroPersonal ventana = new RegistroPersonal();
            ventana.ShowDialog();
        }

        // Cierra la aplicación de forma segura (necesario para el botón '✕' del diseño)
        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}