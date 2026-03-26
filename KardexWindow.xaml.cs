using System;
using System.Data;
using System.Data.SQLite;
using System.Windows;

namespace prueba1
{
    public partial class KardexWindow : Window
    {
        private string _matricula = "";

        public KardexWindow(string matricula)
        {
            InitializeComponent();
            _matricula = matricula;
            dpDesde.SelectedDate = DateTime.Now.AddDays(-30); // Ver último mes por defecto
            dpHasta.SelectedDate = DateTime.Now;
            
            CargarDatosPersonales();
            CargarHistorial();
        }

        private void CargarDatosPersonales()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    SQLiteCommand cmd = new SQLiteCommand("SELECT Nombres, Apellidos FROM Personal_Naval WHERE Matricula=@mat", conexion);
                    cmd.Parameters.AddWithValue("@mat", _matricula);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            txtNombreMarino.Text = $"{reader["Nombres"]} {reader["Apellidos"]} ({_matricula})";
                    }
                }
            }
            catch { }
        }

        private void BtnConsultar_Click(object sender, RoutedEventArgs e) { CargarHistorial(); }

        private void CargarHistorial()
        {
            if (!dpDesde.SelectedDate.HasValue || !dpHasta.SelectedDate.HasValue) return;

            string fechaDesde = dpDesde.SelectedDate.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string fechaHasta = dpHasta.SelectedDate.Value.ToString("yyyy-MM-dd") + " 23:59:59";

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT FechaHora, MensajeAcceso, NovedadMomento FROM Registro_Accesos WHERE Matricula=@mat AND FechaHora >= @desde AND FechaHora <= @hasta ORDER BY FechaHora DESC";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@mat", _matricula);
                    cmd.Parameters.AddWithValue("@desde", fechaDesde);
                    cmd.Parameters.AddWithValue("@hasta", fechaHasta);

                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);
                    dgHistorial.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }
    }
}