using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using prueva1;
using prueba1;

namespace prueba1
{
    public partial class PanelAdminWindow : Window
    {
        private string _matriculaNovedadActual = "";

        public PanelAdminWindow()
        {
            InitializeComponent();
        }

        // --- NAVEGACIÓN DEL MENÚ LATERAL ---
        private void BtnNuevoRegistro_Click(object sender, RoutedEventArgs e)
        {
            RegistroPersonal ventana = new RegistroPersonal();
            ventana.ShowDialog();
            if (PanelDirectorio.Visibility == Visibility.Visible) CargarDirectorio();
        }

        private void BtnDirectorio_Click(object sender, RoutedEventArgs e)
        {
            PanelBienvenida.Visibility = Visibility.Collapsed;
            PanelReportes.Visibility = Visibility.Collapsed;
            PanelDirectorio.Visibility = Visibility.Visible;
            CargarDirectorio();
        }

        private void BtnReportes_Click(object sender, RoutedEventArgs e)
        {
            PanelBienvenida.Visibility = Visibility.Collapsed;
            PanelDirectorio.Visibility = Visibility.Collapsed;
            PanelReportes.Visibility = Visibility.Visible;
            CargarReportes(); // Carga la tabla de historial al abrir la pestaña
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sesión cerrada correctamente.", "Cerrar Sesión", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        // --- FUNCIONES DEL DIRECTORIO (Editar, Eliminar, Novedades) ---
        private void BtnEditarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string matriculaSeleccionada = boton.CommandParameter.ToString();
                RegistroPersonal ventanaEdicion = new RegistroPersonal(matriculaSeleccionada);
                ventanaEdicion.ShowDialog();
                CargarDirectorio();
            }
        }

        private void BtnEliminarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string matriculaSeleccionada = boton.CommandParameter.ToString();
                MessageBoxResult respuesta = MessageBox.Show(
                    $"¿Estás seguro de que deseas eliminar permanentemente a {matriculaSeleccionada}?",
                    "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (respuesta == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                        {
                            string query = "DELETE FROM Personal_Naval WHERE Matricula = @mat";
                            SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                            cmd.Parameters.AddWithValue("@mat", matriculaSeleccionada);
                            cmd.ExecuteNonQuery();
                            MessageBox.Show("Registro eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                            CargarDirectorio();
                        }
                    }
                    catch (Exception ex) { MessageBox.Show("Error al eliminar: " + ex.Message); }
                }
            }
        }

        private void BtnNovedad_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                _matriculaNovedadActual = boton.CommandParameter.ToString();
                txtMatriculaNovedad.Text = _matriculaNovedadActual;

                try
                {
                    using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                    {
                        string query = "SELECT Estatus, Novedad FROM Personal_Naval WHERE Matricula = @mat";
                        SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                        cmd.Parameters.AddWithValue("@mat", _matriculaNovedadActual);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string estatusBD = reader["Estatus"].ToString();
                                string novedadBD = reader["Novedad"].ToString();

                                foreach (ComboBoxItem item in cmbEstatus.Items)
                                    if (item.Tag.ToString() == estatusBD) { cmbEstatus.SelectedItem = item; break; }

                                foreach (ComboBoxItem item in cmbNovedad.Items)
                                    if (item.Tag.ToString() == novedadBD) { cmbNovedad.SelectedItem = item; break; }
                            }
                        }
                    }
                }
                catch { }

                PanelNovedades.Visibility = Visibility.Visible;
            }
        }

        private void BtnGuardarNovedad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string nuevoEstatus = ((ComboBoxItem)cmbEstatus.SelectedItem).Tag.ToString();
                string nuevaNovedad = ((ComboBoxItem)cmbNovedad.SelectedItem).Tag.ToString();

                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "UPDATE Personal_Naval SET Estatus = @estatus, Novedad = @novedad WHERE Matricula = @mat";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@estatus", nuevoEstatus);
                    cmd.Parameters.AddWithValue("@novedad", nuevaNovedad);
                    cmd.Parameters.AddWithValue("@mat", _matriculaNovedadActual);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Novedad actualizada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                PanelNovedades.Visibility = Visibility.Collapsed;
                CargarDirectorio();
            }
            catch (Exception ex) { MessageBox.Show("Error al guardar novedad: " + ex.Message); }
        }

        private void BtnCancelarNovedad_Click(object sender, RoutedEventArgs e) { PanelNovedades.Visibility = Visibility.Collapsed; }

        private void CargarDirectorio()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT Matricula, IdGrado AS Grado, Nombres AS Nombre, Apellidos, IdJefatura AS Jefatura, Novedad, Estatus FROM Personal_Naval";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(cmd);
                    System.Data.DataTable dt = new System.Data.DataTable();
                    adaptador.Fill(dt);
                    dgPersonal.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar personal: " + ex.Message); }
        }

        // =========================================================
        // --- NUEVA SECCIÓN: MÓDULO DE REPORTES Y EXPORTACIÓN ---
        // =========================================================

        private void BtnFiltrarReporte_Click(object sender, RoutedEventArgs e)
        {
            CargarReportes(); // Vuelve a cargar la tabla aplicando lo escrito en los filtros
        }

        private void CargarReportes()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    // Unimos la tabla de accesos con la del personal para obtener el Nombre Completo
                    string query = @"
                        SELECT 
                            r.FechaHora, 
                            r.Matricula, 
                            IFNULL(p.Nombres || ' ' || p.Apellidos, 'DESCONOCIDO (SIN REGISTRO)') AS NombreCompleto, 
                            r.MensajeAcceso, 
                            r.NovedadMomento
                        FROM Registro_Accesos r
                        LEFT JOIN Personal_Naval p ON r.Matricula = p.Matricula
                        WHERE 1=1 ";

                    // Aplicamos el filtro de matrícula si se escribió algo
                    if (!string.IsNullOrWhiteSpace(txtFiltroMatricula.Text))
                    {
                        query += $" AND r.Matricula LIKE '%{txtFiltroMatricula.Text}%' ";
                    }

                    // Aplicamos el filtro de fecha de inicio
                    if (dpFechaInicio.SelectedDate.HasValue)
                    {
                        query += $" AND r.FechaHora >= '{dpFechaInicio.SelectedDate.Value.ToString("yyyy-MM-dd 00:00:00")}' ";
                    }

                    // Aplicamos el filtro de fecha fin
                    if (dpFechaFin.SelectedDate.HasValue)
                    {
                        query += $" AND r.FechaHora <= '{dpFechaFin.SelectedDate.Value.ToString("yyyy-MM-dd 23:59:59")}' ";
                    }

                    query += " ORDER BY r.FechaHora DESC"; // Los más recientes primero

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(cmd);
                    System.Data.DataTable dt = new System.Data.DataTable();
                    adaptador.Fill(dt);

                    dgReportes.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar reportes: " + ex.Message);
            }
        }

        private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            if (dgReportes.Items.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = "Reporte_Accesos_" + DateTime.Now.ToString("yyyyMMdd");
                dlg.DefaultExt = ".csv";
                dlg.Filter = "Archivo Excel CSV (.csv)|*.csv";

                if (dlg.ShowDialog() == true)
                {
                    StringBuilder sb = new StringBuilder();

                    // Cabeceras (Títulos de Excel)
                    sb.AppendLine("Fecha y Hora,Matricula,Nombre Completo,Mensaje Acceso,Novedad al Momento");

                    // Extraemos los datos de la tabla que está viendo el usuario
                    foreach (DataRowView row in dgReportes.ItemsSource)
                    {
                        string fecha = row["FechaHora"].ToString();
                        string matricula = row["Matricula"].ToString();
                        string nombre = row["NombreCompleto"].ToString();
                        string mensaje = row["MensajeAcceso"].ToString();
                        string novedad = row["NovedadMomento"].ToString();

                        sb.AppendLine($"{fecha},{matricula},{nombre},{mensaje},{novedad}");
                    }

                    // Guardamos el archivo, usando UTF8 para que reconozca los acentos y las ñ
                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Reporte exportado exitosamente a Excel.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al exportar a Excel: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}