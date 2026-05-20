using System;
using System.Data.SQLite;
using System.Windows;

namespace prueba1
{
    public partial class PanelAdminWindow : Window
    {
        private string _rolActual = "";

        public Visibility VisibilidadAdmin { get; set; } = Visibility.Visible;

        public PanelAdminWindow(string rolUsuario)
        {
            InitializeComponent();
            _rolActual = rolUsuario;
            
            AplicarPermisos();
            this.DataContext = this; 

            // Tareas de mantenimiento al iniciar la aplicación principal
            ActualizarEsquemaHistorial();
            LimpiarNovedadesExpiradas();
        }

        private void ActualizarEsquemaHistorial()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    new SQLiteCommand("ALTER TABLE Historial_Reportes ADD COLUMN TipoArchivo TEXT DEFAULT 'EXCEL'", conexion).ExecuteNonQuery();
                }
            }
            catch { /* Falla silenciosamente si la columna ya existe */ }
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    new SQLiteCommand("ALTER TABLE Historial_Reportes ADD COLUMN TipoReporte TEXT DEFAULT 'DIARIO'", conexion).ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void AplicarPermisos()
        {
            if (_rolActual == "GUARDIA")
            {
                BtnNuevoRegistroBtn.Visibility = Visibility.Collapsed; 
                BtnUsuarios.Visibility = Visibility.Collapsed; 
                VisibilidadAdmin = Visibility.Collapsed; 
            }
        }

        // MOTOR AUTOMATIZADO DE FECHAS (Se mantiene aquí porque aplica a todo el sistema al arrancar)
        private void LimpiarNovedadesExpiradas()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string hoy = DateTime.Now.ToString("yyyy-MM-dd");
                    string query = $@"UPDATE Personal_Naval 
                                      SET Novedad = 'PRESENTE', FechaInicioNovedad = NULL, FechaFinNovedad = NULL 
                                      WHERE FechaFinNovedad IS NOT NULL AND FechaFinNovedad < '{hoy}' AND Novedad != 'PRESENTE'";
                    new SQLiteCommand(query, conexion).ExecuteNonQuery();
                }
            }
            catch { /* Silencioso */ }
        }

        // =========================================================
        // --- NAVEGACIÓN Y ENRUTAMIENTO (ARQUITECTURA LIMPIA) ---
        // =========================================================

        private void BtnNuevoRegistro_Click(object sender, RoutedEventArgs e)
        {
            RegistroPersonal ventana = new RegistroPersonal();
            ventana.ShowDialog();
            
            // Si el directorio estaba abierto al momento de agregar, lo recargamos
            if (ContenedorPrincipal.Content is DirectorioControl directorioActual)
            {
                directorioActual.CargarDirectorio();
            }
        }
        private void BtnGrados_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new GestionGradosControl();
        }

        private void BtnDirectorio_Click(object sender, RoutedEventArgs e)
        {
            // Inyecta el módulo de Directorio
            ContenedorPrincipal.Content = new DirectorioControl(_rolActual);
        }

        private void BtnNovedadesMasivas_Click(object sender, RoutedEventArgs e)
        {
            // Inyecta el módulo de Novedades Masivas (Asegúrate de crear NovedadesMasivasControl de manera similar a los demás)
            ContenedorPrincipal.Content = new NovedadesMasivasControl();
        }

        private void BtnReportes_Click(object sender, RoutedEventArgs e)
        {
            // Inyecta el módulo de Reportes, pasándole el rol actual
            ContenedorPrincipal.Content = new ReportesControl(_rolActual);
        }

        private void BtnUsuarios_Click(object sender, RoutedEventArgs e)
        {
            // Inyecta el módulo de Usuarios
            ContenedorPrincipal.Content = new UsuariosControl();

        }

        private void BtnOrganigrama_Click(object sender, RoutedEventArgs e)
        {
            // Inyecta el módulo del organigrama dinámico
            ContenedorPrincipal.Content = new GestionJefaturasControl();
        }

        private void BtnSistema_Click(object sender, RoutedEventArgs e)
        {
            // Inyecta el módulo de Sistema y Respaldo
            ContenedorPrincipal.Content = new SistemaControl();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sesión cerrada correctamente.", "Cerrar Sesión", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}