using System;
using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using prueva1;
using prueba1;

namespace prueba1
{
    public partial class PanelAdminWindow : Window
    {
        public PanelAdminWindow()
        {
            InitializeComponent();
        }

        private void BtnNuevoRegistro_Click(object sender, RoutedEventArgs e)
        {
            RegistroPersonal ventana = new RegistroPersonal();
            ventana.ShowDialog();
            
            if (PanelDirectorio.Visibility == Visibility.Visible)
            {
                CargarDirectorio();
            }
        }

        private void BtnDirectorio_Click(object sender, RoutedEventArgs e)
        {
            PanelBienvenida.Visibility = Visibility.Collapsed;
            PanelDirectorio.Visibility = Visibility.Visible;
            CargarDirectorio();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sesión cerrada correctamente.", "Cerrar Sesión", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        // --- MÉTODO DE EDITAR (El que se había borrado) ---
        private void BtnEditarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string matriculaSeleccionada = boton.CommandParameter.ToString();
                
                // Abrimos la ventana de registro enviándole la matrícula (Modo Edición)
                RegistroPersonal ventanaEdicion = new RegistroPersonal(matriculaSeleccionada);
                ventanaEdicion.ShowDialog();
                
                // Recargamos la tabla al terminar de editar
                CargarDirectorio();
            }
        }

        // --- MÉTODO DE ELIMINAR (El nuevo que agregamos) ---
        private void BtnEliminarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string matriculaSeleccionada = boton.CommandParameter.ToString();

                // 1. Preguntar si está seguro
                MessageBoxResult respuesta = MessageBox.Show(
                    $"¿Estás seguro de que deseas eliminar permanentemente el registro con matrícula {matriculaSeleccionada}?\n\nEsta acción también borrará su huella y foto.", 
                    "Confirmar Eliminación", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);

                // 2. Si dice que SÍ, borramos de SQLite
                if (respuesta == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                        {
                            string query = "DELETE FROM Personal_Naval WHERE Matricula = @mat";
                            SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                            cmd.Parameters.AddWithValue("@mat", matriculaSeleccionada);

                            int filasAfectadas = cmd.ExecuteNonQuery();

                            if (filasAfectadas > 0)
                            {
                                MessageBox.Show("Registro y datos biométricos eliminados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                                CargarDirectorio(); // Actualiza la tabla visualmente
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error al eliminar el registro: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // --- MÉTODO PARA CARGAR LOS DATOS EN LA TABLA ---
        private void CargarDirectorio()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = @"
                        SELECT 
                            Matricula, 
                            IdGrado AS Grado, 
                            Nombres AS Nombre, 
                            Apellidos, 
                            IdJefatura AS Jefatura,
                            'SIN REGISTRO' AS Novedad
                        FROM Personal_Naval"; 

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(cmd);
                    System.Data.DataTable dt = new System.Data.DataTable();
                    adaptador.Fill(dt);

                    dgPersonal.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar personal: " + ex.Message);
            }
        }
    }
}