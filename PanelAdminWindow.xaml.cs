using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using prueva1;
using prueba1;

namespace prueba1
{
    public partial class PanelAdminWindow : Window
    {
        private string _matriculaNovedadActual = "";
        private string _resumenActual = "";

        public PanelAdminWindow()
        {
            InitializeComponent();
        }

        // =========================================================
        // --- FUNCIONES TRADUCTORAS (ID a TEXTO) ---
        // =========================================================
        private string ObtenerNombreGrado(int id)
        {
            string[] grados = { "Otro", "Marinero", "Cabo", "Tercer Maestre", "Segundo Maestre", "Primer Maestre", "TTE. Corbeta", "TTE. Fragata", "TTE. Navío", "CAP. Corbeta", "CAP. Fragata", "CAP. Navío", "Contralmirante", "Vicealmirante", "Almirante" };
            if (id >= 1 && id <= grados.Length) return grados[id - 1];
            return "DESCONOCIDO";
        }

        private string ObtenerNombreJefatura(int id)
        {
            string[] jefaturas = { "Otro", "Talleres", "Servicios", "Detall", "Comunav" };
            if (id >= 1 && id <= jefaturas.Length) return jefaturas[id - 1];
            return "DESCONOCIDA";
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
            OcultarPaneles();
            PanelDirectorio.Visibility = Visibility.Visible;
            CargarDirectorio();
        }

        private void BtnReportes_Click(object sender, RoutedEventArgs e)
        {
            OcultarPaneles();
            PanelReportes.Visibility = Visibility.Visible;

            dpFechaInicio.SelectedDate = DateTime.Now;
            dpFechaFin.SelectedDate = DateTime.Now;
            dpCorteFecha.SelectedDate = DateTime.Now;

            CargarBitacora();
            CargarArchivoHistorico();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sesión cerrada correctamente.", "Cerrar Sesión", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void OcultarPaneles()
        {
            PanelBienvenida.Visibility = Visibility.Collapsed;
            PanelDirectorio.Visibility = Visibility.Collapsed;
            PanelReportes.Visibility = Visibility.Collapsed;
        }

        // --- FUNCIONES DEL DIRECTORIO (Editar, Eliminar, Novedades) ---
        private void BtnEditarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                RegistroPersonal ventanaEdicion = new RegistroPersonal(boton.CommandParameter.ToString());
                ventanaEdicion.ShowDialog();
                CargarDirectorio();
            }
        }

        private void BtnEliminarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string mat = boton.CommandParameter.ToString();
                if (MessageBox.Show($"¿Eliminar permanentemente a {mat}?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                        {
                            new SQLiteCommand($"DELETE FROM Personal_Naval WHERE Matricula = '{mat}'", conexion).ExecuteNonQuery();
                            CargarDirectorio();
                        }
                    }
                    catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
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
                        SQLiteCommand cmd = new SQLiteCommand("SELECT Estatus, Novedad FROM Personal_Naval WHERE Matricula = @mat", conexion);
                        cmd.Parameters.AddWithValue("@mat", _matriculaNovedadActual);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                foreach (ComboBoxItem item in cmbEstatus.Items) if (item.Tag.ToString() == reader["Estatus"].ToString()) { cmbEstatus.SelectedItem = item; break; }
                                foreach (ComboBoxItem item in cmbNovedad.Items) if (item.Tag.ToString() == reader["Novedad"].ToString()) { cmbNovedad.SelectedItem = item; break; }
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
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    SQLiteCommand cmd = new SQLiteCommand("UPDATE Personal_Naval SET Estatus = @est, Novedad = @nov WHERE Matricula = @mat", conexion);
                    cmd.Parameters.AddWithValue("@est", ((ComboBoxItem)cmbEstatus.SelectedItem).Tag.ToString());
                    cmd.Parameters.AddWithValue("@nov", ((ComboBoxItem)cmbNovedad.SelectedItem).Tag.ToString());
                    cmd.Parameters.AddWithValue("@mat", _matriculaNovedadActual);
                    cmd.ExecuteNonQuery();
                }
                PanelNovedades.Visibility = Visibility.Collapsed;
                CargarDirectorio();
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void BtnCancelarNovedad_Click(object sender, RoutedEventArgs e) { PanelNovedades.Visibility = Visibility.Collapsed; }

        private void CargarDirectorio()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT Matricula, IdGrado, Nombres AS Nombre, Apellidos, IdJefatura, Novedad, Estatus FROM Personal_Naval";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Columns.Add("Matricula");
                        dt.Columns.Add("Grado"); // Aquí guardaremos el texto traducido
                        dt.Columns.Add("Nombre");
                        dt.Columns.Add("Apellidos");
                        dt.Columns.Add("Estatus");
                        dt.Columns.Add("Novedad");

                        while (reader.Read())
                        {
                            DataRow row = dt.NewRow();
                            row["Matricula"] = reader["Matricula"].ToString();
                            // Traducimos el Grado al instante
                            row["Grado"] = ObtenerNombreGrado(Convert.ToInt32(reader["IdGrado"]));
                            row["Nombre"] = reader["Nombre"].ToString();
                            row["Apellidos"] = reader["Apellidos"].ToString();
                            row["Estatus"] = reader["Estatus"].ToString();
                            row["Novedad"] = reader["Novedad"].ToString();

                            dt.Rows.Add(row);
                        }
                        dgPersonal.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar personal: " + ex.Message); }
        }

        // =========================================================
        // --- PESTAÑA 1: BITÁCORA EN VIVO ---
        // =========================================================

        private void BtnFiltrarBitacora_Click(object sender, RoutedEventArgs e) { CargarBitacora(); }

        private void CargarBitacora()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = @"
                        SELECT r.FechaHora, r.Matricula, IFNULL(p.Nombres || ' ' || p.Apellidos, 'DESCONOCIDO') AS NombreCompleto, r.MensajeAcceso
                        FROM Registro_Accesos r LEFT JOIN Personal_Naval p ON r.Matricula = p.Matricula WHERE 1=1 ";

                    if (!string.IsNullOrWhiteSpace(txtFiltroMatricula.Text)) query += $" AND r.Matricula LIKE '%{txtFiltroMatricula.Text}%' ";

                    if (dpFechaInicio.SelectedDate.HasValue)
                        query += $" AND r.FechaHora >= '{dpFechaInicio.SelectedDate.Value.ToString("yyyy-MM-dd")} {txtHoraInicio.Text}:00' ";

                    if (dpFechaFin.SelectedDate.HasValue)
                        query += $" AND r.FechaHora <= '{dpFechaFin.SelectedDate.Value.ToString("yyyy-MM-dd")} {txtHoraFin.Text}:59' ";

                    if (chkOcultarFallos.IsChecked == true)
                        query += " AND r.MensajeAcceso NOT LIKE '%RECHAZADO%' AND r.MensajeAcceso NOT LIKE '%DENEGADO%' ";

                    query += " ORDER BY r.FechaHora DESC";

                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(new SQLiteCommand(query, conexion));
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);
                    dgBitacora.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar bitácora: " + ex.Message); }
        }

        // =========================================================
        // --- PESTAÑA 2: CORTE DE ASISTENCIA (EL MOTOR) ---
        // =========================================================

        private void BtnGenerarVistaPrevia_Click(object sender, RoutedEventArgs e)
        {
            if (!dpCorteFecha.SelectedDate.HasValue) return;

            string fecha = dpCorteFecha.SelectedDate.Value.ToString("yyyy-MM-dd");
            string horaInicio = txtCorteHoraInicio.Text;
            string horaFin = txtCorteHoraFin.Text;

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = $@"
                        SELECT 
                            p.Matricula, 
                            p.IdGrado, 
                            p.Nombres || ' ' || p.Apellidos AS NombreCompleto, 
                            p.IdJefatura, 
                            p.Novedad,
                            (SELECT COUNT(*) FROM Registro_Accesos r WHERE r.Matricula = p.Matricula AND r.FechaHora >= '{fecha} {horaInicio}:00' AND r.FechaHora <= '{fecha} {horaFin}:59') AS AccesosTurno
                        FROM Personal_Naval p
                        WHERE p.Estatus = 'ACTIVO'
                        ORDER BY p.IdJefatura, p.IdGrado";

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    SQLiteDataReader reader = cmd.ExecuteReader();

                    DataTable dtAsistencia = new DataTable();
                    dtAsistencia.Columns.Add("Matricula");
                    dtAsistencia.Columns.Add("Grado");
                    dtAsistencia.Columns.Add("NombreCompleto");
                    dtAsistencia.Columns.Add("Jefatura");
                    dtAsistencia.Columns.Add("Situacion");

                    int total = 0, presentes = 0, faltistas = 0, justificados = 0;

                    while (reader.Read())
                    {
                        total++;
                        DataRow row = dtAsistencia.NewRow();
                        row["Matricula"] = reader["Matricula"].ToString();

                        // --- ¡AQUÍ TRADUCIMOS LOS IDs A TEXTO! ---
                        row["Grado"] = ObtenerNombreGrado(Convert.ToInt32(reader["IdGrado"]));
                        row["Jefatura"] = ObtenerNombreJefatura(Convert.ToInt32(reader["IdJefatura"]));
                        // -----------------------------------------

                        row["NombreCompleto"] = reader["NombreCompleto"].ToString();

                        string novedadAct = reader["Novedad"].ToString();
                        int accesos = Convert.ToInt32(reader["AccesosTurno"]);

                        if (novedadAct != "PRESENTE")
                        {
                            row["Situacion"] = novedadAct;
                            justificados++;
                        }
                        else if (accesos > 0)
                        {
                            row["Situacion"] = "PRESENTE";
                            presentes++;
                        }
                        else
                        {
                            row["Situacion"] = "FALTISTA";
                            faltistas++;
                        }

                        dtAsistencia.Rows.Add(row);
                    }

                    dgCorteAsistencia.ItemsSource = dtAsistencia.DefaultView;
                    _resumenActual = $"Total: {total} | Presentes: {presentes} | Faltistas: {faltistas} | Justificados/Novedad: {justificados}";
                    txtResumenCorte.Text = _resumenActual;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al generar corte: " + ex.Message); }
        }

        private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            if (dgCorteAsistencia.Items.Count == 0) return;

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "Corte_Asistencia_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
            dlg.Filter = "Archivo Excel CSV (.csv)|*.csv";

            if (dlg.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("SECRETARIA DE MARINA - CORTE DE ASISTENCIA");
                sb.AppendLine($"Fecha: {dpCorteFecha.SelectedDate.Value.ToString("dd/MM/yyyy")} | Turno: {txtCorteHoraInicio.Text} a {txtCorteHoraFin.Text}");
                sb.AppendLine(_resumenActual);
                sb.AppendLine();
                sb.AppendLine("Matricula,Grado,Nombre Completo,Jefatura,Situacion Final");

                foreach (DataRowView row in dgCorteAsistencia.ItemsSource)
                {
                    sb.AppendLine($"{row["Matricula"]},{row["Grado"]},{row["NombreCompleto"]},{row["Jefatura"]},{row["Situacion"]}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                RegistrarEnHistorial(dlg.FileName);

                MessageBox.Show("Corte exportado a Excel exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportarPDF_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("¡Excelente progreso! Para generar el PDF oficial necesitamos instalar la librería iText7. Por ahora, usa la exportación a Excel.", "Módulo PDF en Construcción", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========================================================
        // --- PESTAÑA 3: ARCHIVO HISTÓRICO ---
        // =========================================================

        private void RegistrarEnHistorial(string rutaArchivo)
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string turno = $"{dpCorteFecha.SelectedDate.Value.ToString("dd/MM/yyyy")} ({txtCorteHoraInicio.Text} a {txtCorteHoraFin.Text})";
                    SQLiteCommand cmd = new SQLiteCommand("INSERT INTO Historial_Reportes (FechaGeneracion, Turno, GeneradoPor, RutaArchivo) VALUES (@f, @t, @g, @r)", conexion);
                    cmd.Parameters.AddWithValue("@f", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    cmd.Parameters.AddWithValue("@t", turno);
                    cmd.Parameters.AddWithValue("@g", "Admin");
                    cmd.Parameters.AddWithValue("@r", rutaArchivo);
                    cmd.ExecuteNonQuery();
                }
                CargarArchivoHistorico();
            }
            catch { }
        }

        private void CargarArchivoHistorico()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter("SELECT FechaGeneracion, Turno, GeneradoPor, RutaArchivo FROM Historial_Reportes ORDER BY IdReporte DESC", conexion);
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);
                    dgArchivoHistorico.ItemsSource = dt.DefaultView;
                }
            }
            catch { }
        }

        private void BtnAbrirArchivo_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string ruta = boton.CommandParameter.ToString();
                if (File.Exists(ruta))
                {
                    Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("El archivo físico ya no se encuentra en la ruta original.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}