using System;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents; 
using System.Windows.Media;     
using ExcelDataReader;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace prueba1
{
    public partial class PanelAdminWindow : Window
    {
        private string _matriculaNovedadActual = "";
        private string _registroAuditoriaInalterable = "";
        
        private string _matriculaJustificar = "";
        private string _nombreJustificar = "";

        private string _resumenActual = "";
        private string _rolActual = "";
        private int _idUsuarioSeleccionado = 0;
        
        private string rutaBD = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SemaforoMarina.db");
        
        public Visibility VisibilidadAdmin { get; set; } = Visibility.Visible;

        public PanelAdminWindow(string rolUsuario)
        {
            InitializeComponent();
            _rolActual = rolUsuario;

            AplicarPermisos();
            this.DataContext = this;

            CargarMetricasDashboard();
            ucDashboard.TarjetaClickeada += UcDashboard_TarjetaClickeada; 

            CargarHistorialReportes();
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
            
            dpConcentradoInicio.SelectedDate = DateTime.Now.AddDays(-15);
            dpConcentradoFin.SelectedDate = DateTime.Now;

            CargarBitacora();
            CargarHistorialReportes();
        }

        private void BtnUsuarios_Click(object sender, RoutedEventArgs e)
        {
            OcultarPaneles();
            PanelUsuarios.Visibility = Visibility.Visible;
            BtnLimpiarUsuario_Click(null, null);
            CargarUsuarios();
        }
        
        private void BtnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            OcultarPaneles();
            PanelConfiguracion.Visibility = Visibility.Visible;
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OcultarPaneles()
        {
            PanelBienvenida.Visibility = Visibility.Collapsed;
            PanelDirectorio.Visibility = Visibility.Collapsed;
            PanelReportes.Visibility = Visibility.Collapsed;
            PanelUsuarios.Visibility = Visibility.Collapsed;
            PanelConfiguracion.Visibility = Visibility.Collapsed; 
        }

        // =========================================================
        // --- MÓDULO DE RESPALDOS Y EXCEL---
        // =========================================================

        private void BtnExportarRespaldo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "Base de Datos SQLite (*.db)|*.db";
            saveFileDialog.FileName = $"Respaldo_SICB_{DateTime.Now:yyyyMMdd_HHmm}.db";
            saveFileDialog.Title = "Guardar Respaldo de Base de Datos";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    System.IO.File.Copy(rutaBD, saveFileDialog.FileName, true);
                    MessageBox.Show("Respaldo exportado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al respaldar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnImportarRespaldo_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("⚠️ Esta acción reemplazará los datos actuales. ¿Deseas continuar?", "Restaurar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.Filter = "Base de Datos SQLite (*.db)|*.db";
                openFileDialog.Title = "Seleccionar Archivo de Respaldo";

                if (openFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        System.IO.File.Copy(openFileDialog.FileName, rutaBD, true);
                        MessageBox.Show("Base de datos restaurada. El sistema se cerrará.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error al restaurar. " + ex.Message, "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnImportarNovedadesExcel_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Archivos de Excel (*.xlsx;*.xls)|*.xlsx;*.xls";
            openFileDialog.Title = "Seleccionar Plantilla de Novedades";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    using (var stream = System.IO.File.Open(openFileDialog.FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        using (var reader = ExcelReaderFactory.CreateReader(stream))
                        {
                            var result = reader.AsDataSet();
                            DataTable tabla = result.Tables[0]; 

                            int actualizados = 0;

                            using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                            {
                                for (int i = 1; i < tabla.Rows.Count; i++)
                                {
                                    string matricula = tabla.Rows[i][0]?.ToString();
                                    string nuevaNovedad = tabla.Rows[i][2]?.ToString()?.ToUpper(); 

                                    if (!string.IsNullOrWhiteSpace(matricula) && !string.IsNullOrWhiteSpace(nuevaNovedad))
                                    {
                                        string query = "UPDATE Personal_Naval SET Novedad = @nov WHERE Matricula = @mat";
                                        SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                                        cmd.Parameters.AddWithValue("@nov", nuevaNovedad.Trim());
                                        cmd.Parameters.AddWithValue("@mat", matricula.Trim());
                                        
                                        actualizados += cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            
                            MessageBox.Show($"¡Proceso completado! Se actualizaron {actualizados} elementos.", "Sincronización", MessageBoxButton.OK, MessageBoxImage.Information);
                            CargarDirectorio();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al leer el archivo Excel. " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // =========================================================
        // --- MÓDULO: REPORTE CONCENTRADO MATRIZ (CORREGIDO) ---
        // =========================================================

        private void BtnGenerarConcentrado_Click(object sender, RoutedEventArgs e)
        {
            if (!dpConcentradoInicio.SelectedDate.HasValue || !dpConcentradoFin.SelectedDate.HasValue) return;

            DateTime fechaInicio = dpConcentradoInicio.SelectedDate.Value.Date;
            DateTime fechaFin = dpConcentradoFin.SelectedDate.Value.Date;

            if ((fechaFin - fechaInicio).TotalDays > 31)
            {
                MessageBox.Show("Por rendimiento, el rango máximo para la matriz es de 31 días.", "Rango muy amplio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (fechaInicio > fechaFin)
            {
                MessageBox.Show("La fecha de inicio no puede ser mayor a la final.", "Error de Fechas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    DataTable dtMatriz = new DataTable();
                    dtMatriz.Columns.Add("Matrícula");
                    dtMatriz.Columns.Add("Grado");
                    dtMatriz.Columns.Add("Nombre Elemento");

                    for (DateTime d = fechaInicio; d <= fechaFin; d = d.AddDays(1))
                    {
                        // CORRECCIÓN: Quitamos la diagonal para que WPF dibuje la columna correctamente
                        dtMatriz.Columns.Add(d.ToString("dd MMM").ToUpper()); 
                    }

                    string queryAccesos = "SELECT Matricula, date(FechaHora) as FechaStr, MIN(FechaHora) as HoraEntrada FROM Registro_Accesos WHERE FechaHora >= @ini AND FechaHora <= @fin GROUP BY Matricula, date(FechaHora)";
                    SQLiteCommand cmdAcc = new SQLiteCommand(queryAccesos, conexion);
                    cmdAcc.Parameters.AddWithValue("@ini", fechaInicio.ToString("yyyy-MM-dd 00:00:00"));
                    cmdAcc.Parameters.AddWithValue("@fin", fechaFin.ToString("yyyy-MM-dd 23:59:59"));
                    
                    var dicAccesos = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, TimeSpan>>();
                    
                    using (SQLiteDataReader rAcc = cmdAcc.ExecuteReader())
                    {
                        while(rAcc.Read())
                        {
                            string mat = rAcc["Matricula"].ToString();
                            string fechaD = rAcc["FechaStr"].ToString(); 
                            
                            if (!dicAccesos.ContainsKey(mat)) dicAccesos[mat] = new System.Collections.Generic.Dictionary<string, TimeSpan>();
                            
                            if(DateTime.TryParse(rAcc["HoraEntrada"].ToString(), out DateTime dtAcc))
                            {
                                dicAccesos[mat][fechaD] = dtAcc.TimeOfDay;
                            }
                        }
                    }

                    TimeSpan horaLimite = TimeSpan.Parse(txtConcentradoTolerancia.Text);

                    string queryPersonal = "SELECT Matricula, IdGrado, Nombres || ' ' || Apellidos AS NombreCompleto FROM Personal_Naval WHERE Estatus = 'ACTIVO' ORDER BY IdJefatura, IdGrado";
                    using (SQLiteCommand cmdPers = new SQLiteCommand(queryPersonal, conexion))
                    using (SQLiteDataReader rPers = cmdPers.ExecuteReader())
                    {
                        while(rPers.Read())
                        {
                            DataRow row = dtMatriz.NewRow();
                            string mat = rPers["Matricula"].ToString();
                            row["Matrícula"] = mat;
                            row["Grado"] = ObtenerNombreGrado(Convert.ToInt32(rPers["IdGrado"]));
                            row["Nombre Elemento"] = rPers["NombreCompleto"].ToString();

                            for (DateTime d = fechaInicio; d <= fechaFin; d = d.AddDays(1))
                            {
                                string fechaKey = d.ToString("yyyy-MM-dd");
                                string colName = d.ToString("dd MMM").ToUpper(); 

                                if (dicAccesos.ContainsKey(mat) && dicAccesos[mat].ContainsKey(fechaKey))
                                {
                                    TimeSpan llegada = dicAccesos[mat][fechaKey];
                                    if (llegada <= horaLimite) row[colName] = "[ P ]"; 
                                    else row[colName] = "[ R ]"; 
                                }
                                else
                                {
                                    row[colName] = "[ F ]"; 
                                }
                            }
                            dtMatriz.Rows.Add(row);
                        }
                    }

                    dgConcentrado.ItemsSource = null; 
                    dgConcentrado.ItemsSource = dtMatriz.DefaultView;
                    txtResumenConcentrado.Text = $"Simbología: [ P ] Presente | [ R ] Retardo | [ F ] Falta  ---  Total evaluados: {dtMatriz.Rows.Count}";
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al generar matriz concentrada: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarConcentradoExcel_Click(object sender, RoutedEventArgs e)
        {
            if (dgConcentrado.ItemsSource == null) return;

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "Matriz_Concentrada_" + DateTime.Now.ToString("yyyyMMdd");
            dlg.Filter = "Archivo Excel CSV (.csv)|*.csv";
            dlg.Title = "Guardar Reporte Concentrado Como...";

            if (dlg.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("SECRETARIA DE MARINA - REPORTE CONCENTRADO DE ASISTENCIA");
                sb.AppendLine(txtResumenConcentrado.Text);
                sb.AppendLine();

                DataTable dt = ((DataView)dgConcentrado.ItemsSource).Table;
                string[] columnNames = new string[dt.Columns.Count];
                for (int i = 0; i < dt.Columns.Count; i++) columnNames[i] = dt.Columns[i].ColumnName;
                sb.AppendLine(string.Join(",", columnNames));

                foreach (DataRow row in dt.Rows)
                {
                    string[] fields = new string[dt.Columns.Count];
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        string val = row[i].ToString();
                        val = val.Replace("[ P ]", "P").Replace("[ R ]", "R").Replace("[ F ]", "F");
                        fields[i] = val.Replace(",", " "); 
                    }
                    sb.AppendLine(string.Join(",", fields));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Matriz Concentrada guardada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // =========================================================
        // --- FUNCIONES DEL DIRECTORIO Y KARDEX ---
        // =========================================================

        private void FiltrosDirectorio_Changed(object sender, RoutedEventArgs e)
        {
            if (dgPersonal != null) CargarDirectorio();
        }

        private void CargarDirectorio()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT Matricula, IdGrado, Nombres AS Nombre, Apellidos, IdJefatura, Novedad, Estatus FROM Personal_Naval WHERE 1=1 ";

                    if (txtBuscarPersonal != null && !string.IsNullOrWhiteSpace(txtBuscarPersonal.Text))
                        query += " AND (Matricula LIKE @busqueda OR Nombres LIKE @busqueda OR Apellidos LIKE @busqueda) ";

                    if (cmbFiltroNovedad != null && cmbFiltroNovedad.SelectedItem is ComboBoxItem item && item.Content.ToString() != "TODOS")
                        query += " AND Novedad = @novedad ";

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);

                    if (txtBuscarPersonal != null && !string.IsNullOrWhiteSpace(txtBuscarPersonal.Text))
                        cmd.Parameters.AddWithValue("@busqueda", "%" + txtBuscarPersonal.Text.Trim() + "%");

                    if (cmbFiltroNovedad != null && cmbFiltroNovedad.SelectedItem is ComboBoxItem itemCombo && itemCombo.Content.ToString() != "TODOS")
                        cmd.Parameters.AddWithValue("@novedad", itemCombo.Content.ToString());

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Columns.Add("Matricula");
                        dt.Columns.Add("Grado");
                        dt.Columns.Add("Nombre");
                        dt.Columns.Add("Apellidos");
                        dt.Columns.Add("Estatus");
                        dt.Columns.Add("Novedad");

                        while (reader.Read())
                        {
                            DataRow row = dt.NewRow();
                            row["Matricula"] = reader["Matricula"].ToString();
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

        private void BtnVerPerfil_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string matricula = boton.CommandParameter.ToString();
                KardexWindow perfil = new KardexWindow(matricula);
                perfil.ShowDialog();
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

        // ===================================================================
        // ---  MÓDULO: CORRECCIÓN EN DOBLE CLIC 
        // ===================================================================
        private void dgCorteAsistencia_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgCorteAsistencia.SelectedItem is DataRowView row)
            {
                _matriculaJustificar = row["Matricula"].ToString();
                _nombreJustificar = row["NombreCompleto"].ToString();
                string situacion = row["Situacion"].ToString();

                if (situacion == "PRESENTE")
                {
                    MessageBox.Show("Este elemento ya cuenta con asistencia verificada y validada.", "No requiere acción", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                txtElementoJustificar.Text = $"Elemento: {_nombreJustificar} ({_matriculaJustificar}) | Estado actual: {situacion}";
                txtNotaIncidencia.Text = "";
                PanelIncidencia.Visibility = Visibility.Visible;
            }
        }

      private void BtnGuardarJustificacion_Click(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrWhiteSpace(txtNotaIncidencia.Text))
    {
        MessageBox.Show("Para propósitos de auditoría, debe escribir el motivo de la corrección.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    try
    {
        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
        {
            string fechaCorte = dpCorteFecha.SelectedDate.Value.ToString("yyyy-MM-dd");
            string horaPerfecta = txtCorteHoraInicio.Text + ":00";
            string fechaHoraInyeccion = $"{fechaCorte} {horaPerfecta}";

            string query = "INSERT INTO Registro_Accesos (Matricula, FechaHora, MensajeAcceso, NovedadMomento) VALUES (@mat, @fecha, 'ACCESO JUSTIFICADO MANUALMENTE', @motivo)";
            SQLiteCommand cmd = new SQLiteCommand(query, conexion);
            cmd.Parameters.AddWithValue("@mat", _matriculaJustificar);
            cmd.Parameters.AddWithValue("@fecha", fechaHoraInyeccion);
            cmd.Parameters.AddWithValue("@motivo", "AUDITORÍA: " + txtNotaIncidencia.Text.Trim());
            cmd.ExecuteNonQuery();
        }

        // LÓGICA BLINDADA: Lo guardamos en RAM, el usuario no puede borrarlo
        string lineaAuditoria = $"[{DateTime.Now:HH:mm}] El usuario {_rolActual} justificó a {_matriculaJustificar}. Motivo: {txtNotaIncidencia.Text.Trim()}";
        _registroAuditoriaInalterable += lineaAuditoria + "\n";
        
        // Lo mostramos en el cuadro bloqueado (IsReadOnly="True")
        txtAuditoriaSistema.Text = _registroAuditoriaInalterable;

        PanelIncidencia.Visibility = Visibility.Collapsed;
        BtnGenerarVistaPrevia_Click(null, null); // Recarga la tabla
        MessageBox.Show("Asistencia corregida y auditada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex) { MessageBox.Show("Error al justificar: " + ex.Message); }
}

        private void BtnCancelarIncidencia_Click(object sender, RoutedEventArgs e) { PanelIncidencia.Visibility = Visibility.Collapsed; }

        // =========================================================
        // --- PESTAÑA: BITÁCORA Y REPORTES ---
        // =========================================================








        private void BtnFiltrarBitacora_Click(object sender, RoutedEventArgs e) { CargarBitacora(); CargarMetricasDashboard();}

        

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
                            (SELECT MIN(r.FechaHora) FROM Registro_Accesos r WHERE r.Matricula = p.Matricula AND r.FechaHora >= '{fecha} {horaInicio}:00' AND r.FechaHora <= '{fecha} {horaFin}:59') AS HoraPrimerAcceso
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

                    int total = 0, presentes = 0, faltistas = 0, retardos = 0, justificados = 0;

                    while (reader.Read())
                    {
                        total++;
                        DataRow row = dtAsistencia.NewRow();
                        row["Matricula"] = reader["Matricula"].ToString();
                        row["Grado"] = ObtenerNombreGrado(Convert.ToInt32(reader["IdGrado"]));
                        row["Jefatura"] = ObtenerNombreJefatura(Convert.ToInt32(reader["IdJefatura"]));
                        row["NombreCompleto"] = reader["NombreCompleto"].ToString();

                        string novedadAct = reader["Novedad"].ToString();
                        string horaPrimeraEntrada = reader["HoraPrimerAcceso"].ToString();

                        if (novedadAct != "PRESENTE")
                        {
                            row["Situacion"] = novedadAct; 
                            justificados++;
                        }
                        else if (!string.IsNullOrEmpty(horaPrimeraEntrada))
                        {
                            TimeSpan horaLlegada = Convert.ToDateTime(horaPrimeraEntrada).TimeOfDay;
                            TimeSpan horaLimite = TimeSpan.Parse(txtCorteHoraLimite.Text);

                            if (horaLlegada <= horaLimite)
                            {
                                row["Situacion"] = "PRESENTE";
                                presentes++;
                            }
                            else
                            {
                                row["Situacion"] = "RETARDO";
                                retardos++;
                            }
                        }
                        else
                        {
                            row["Situacion"] = "FALTISTA";
                            faltistas++;
                        }

                        dtAsistencia.Rows.Add(row);
                    }

                    dgCorteAsistencia.ItemsSource = dtAsistencia.DefaultView;
                    _resumenActual = $"Total de Elementos Evaluados: {total}  |  Presentes: {presentes}  |  Retardos: {retardos}  |  Faltistas: {faltistas}  |  Justificados: {justificados}";
                    txtResumenCorte.Text = _resumenActual;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al generar corte: " + ex.Message); }
        }

        // =========================================================
        // --- 🔒 CANDADO BIOMÉTRICO (SIMULACIÓN PARA EXPORTAR) ---
        // =========================================================
       private bool SolicitarFirmaBiometrica(out string nombreFirmante)
       {
        FirmaBiometricaWindow ventanaFirma = new FirmaBiometricaWindow();
        bool? resultado = ventanaFirma.ShowDialog();
        nombreFirmante = ventanaFirma.NombreFirmante;
        return ventanaFirma.FirmaExitosa;
        }

       private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            if (dgCorteAsistencia.Items.Count == 0) return;

            // 1. Verificamos identidad con la NUEVA ventana biométrica
            string autorizador = "";
            if (!SolicitarFirmaBiometrica(out autorizador)) return;

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "Corte_Asistencia_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
            dlg.Filter = "Archivo Excel CSV (.csv)|*.csv";

            if (dlg.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("SECRETARIA DE MARINA - CORTE DE ASISTENCIA OFICIAL");
                sb.AppendLine($"Fecha del Corte: {dpCorteFecha.SelectedDate.Value.ToString("dd/MM/yyyy")} | Horario Supervisado: {txtCorteHoraInicio.Text} a {txtCorteHoraFin.Text}");
                
                // ESTAMPA BIOMÉTRICA CON EL NOMBRE REAL DEL LECTOR
                sb.AppendLine($"[ DOCUMENTO VALIDADO Y FIRMADO BIOMÉTRICAMENTE POR: {autorizador.ToUpper()} ]");
                sb.AppendLine(_resumenActual);
                
                // Agregamos las Notas Manuales
                sb.AppendLine();
                sb.AppendLine("--- NOTAS MANUALES DEL TURNO ---");
                sb.AppendLine(string.IsNullOrWhiteSpace(txtObservacionesCorte.Text) ? "NINGUNA" : txtObservacionesCorte.Text.Replace("\n", " - ").Replace("\r", "")); 
                
                // Agregamos la Auditoría Inalterable
                sb.AppendLine();
                sb.AppendLine("--- REGISTRO DE AUDITORÍA DEL SISTEMA (AUTOMÁTICO) ---");
                sb.AppendLine(string.IsNullOrWhiteSpace(_registroAuditoriaInalterable) ? "SIN CAMBIOS MANUALES" : _registroAuditoriaInalterable.Replace("\n", " - ").Replace("\r", ""));

                sb.AppendLine();
                sb.AppendLine("Matricula,Grado,Nombre Completo,Jefatura,Situacion Final");

                foreach (DataRowView row in dgCorteAsistencia.ItemsSource)
                {
                    sb.AppendLine($"{row["Matricula"]},{row["Grado"]},{row["NombreCompleto"]},{row["Jefatura"]},{row["Situacion"]}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                RegistrarEnHistorial(dlg.FileName);

                MessageBox.Show("Corte firmado y exportado a Excel exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Limpiamos los cuadros de texto después de exportar exitosamente
                txtObservacionesCorte.Text = ""; 
                _registroAuditoriaInalterable = "";
                txtAuditoriaSistema.Text = "";
            }
        }

        private void BtnExportarPDF_Click(object sender, RoutedEventArgs e)
        {
            if (dgCorteAsistencia.Items.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar. Genere el corte primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Verificamos identidad biométrica
            string autorizador = "";
            if (!SolicitarFirmaBiometrica(out autorizador)) return;

            try
            {
                // Configuración de licencia gratuita comunitaria obligatoria de QuestPDF
                QuestPDF.Settings.License = LicenseType.Community;

                // 2. Preparamos rutas y nombres
                string nombreAutomatico = $"Corte_Asistencia_{DateTime.Now.ToString("yyyyMMdd_HHmm")}.pdf";
                string carpetaBoveda = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reportes_Oficiales");
                if (!System.IO.Directory.Exists(carpetaBoveda)) System.IO.Directory.CreateDirectory(carpetaBoveda);
                string rutaFinal = System.IO.Path.Combine(carpetaBoveda, nombreAutomatico);

                // Variables para el texto
                string fechaTexto = dpCorteFecha.SelectedDate.HasValue
                    ? dpCorteFecha.SelectedDate.Value.ToString("dddd, dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-MX")).ToUpper()
                    : DateTime.Now.ToString("dd/MM/yyyy");
                string horario = $"{txtCorteHoraInicio.Text} A {txtCorteHoraFin.Text} HRS";

                System.Data.DataTable dt = ((System.Data.DataView)dgCorteAsistencia.ItemsSource).Table;

                // 3. CREACIÓN Y GUARDADO DEL PDF (A LA VELOCIDAD DE LA LUZ)
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);

                        // 🔥 AQUÍ ESPECIFICAMOS QUE ES EL COLOR DE QUESTPDF
                        page.PageColor(QuestPDF.Helpers.Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        // --- ENCABEZADO ---
                        page.Header().AlignCenter().Column(col =>
                        {
                            col.Item().Text("ARMADA DE MÉXICO").FontSize(18).Bold();
                            col.Item().Text("DÉCIMA SEXTA ZONA NAVAL").FontSize(14).SemiBold();
                            col.Item().PaddingTop(10).Text("REPORTE OFICIAL DE ASISTENCIA Y ESTADO DE FUERZA").FontSize(16).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                            col.Item().Text($"CORRESPONDIENTE AL DÍA: {fechaTexto}").FontSize(12).SemiBold();
                            col.Item().Text($"HORARIO SUPERVISADO: {horario}").FontSize(10);
                        });

                        // --- TABLA DE DATOS ---
                        page.Content().PaddingVertical(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);  // Matrícula
                                columns.ConstantColumn(70);  // Grado
                                columns.RelativeColumn();    // Nombre
                                columns.ConstantColumn(90);  // Jefatura
                                columns.ConstantColumn(90);  // Situación
                            });

                            string[] cabeceras = { "MATRÍCULA", "GRADO", "NOMBRE COMPLETO", "ÁREA", "SITUACIÓN" };
                            foreach (var cabecera in cabeceras)
                            {
                                table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black).Background(QuestPDF.Helpers.Colors.Grey.Lighten3)
                                     .Padding(5).AlignCenter().Text(cabecera).Bold().FontSize(9);
                            }

                            foreach (System.Data.DataRow fila in dt.Rows)
                            {
                                for (int i = 0; i < dt.Columns.Count; i++)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                                         .Padding(5).Text(fila[i].ToString()).FontSize(9);
                                }
                            }
                        });

                        // --- PIE DE PÁGINA Y FIRMAS ---
                        page.Footer().Column(col =>
                        {
                            col.Item().Text("RESUMEN DEL TURNO:").Bold();
                            col.Item().Text(txtResumenCorte.Text);
                            col.Item().PaddingBottom(10);

                            if (!string.IsNullOrWhiteSpace(txtObservacionesCorte.Text))
                            {
                                col.Item().Text("NOTAS MANUALES DEL GUARDIA:").Bold();
                                col.Item().Text(txtObservacionesCorte.Text);
                                col.Item().PaddingBottom(10);
                            }

                            if (!string.IsNullOrWhiteSpace(_registroAuditoriaInalterable))
                            {
                                col.Item().Text("AUDITORÍA DE SISTEMA:").Bold().FontColor(QuestPDF.Helpers.Colors.Red.Darken2);
                                col.Item().Text(_registroAuditoriaInalterable).FontColor(QuestPDF.Helpers.Colors.Red.Darken2);
                                col.Item().PaddingBottom(10);
                            }

                            col.Item().AlignCenter().PaddingTop(20).Text("VALIDADO Y FIRMADO MEDIANTE AUTENTICACIÓN BIOMÉTRICA").Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                            col.Item().AlignCenter().Text("_____________________________________________").Bold();
                            col.Item().AlignCenter().Text($"AUTORIZÓ: {autorizador.ToUpper()}").FontSize(9);
                        });
                    });
                })
                .GeneratePdf(rutaFinal);

                // 4. Guardamos en Base de Datos y Limpiamos
                RegistrarEnHistorial(rutaFinal);

                MessageBox.Show("Documento Oficial archivado automáticamente en la bóveda.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                txtObservacionesCorte.Text = "";
                _registroAuditoriaInalterable = "";
                txtAuditoriaSistema.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar el PDF con la librería: " + ex.Message, "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                    cmd.Parameters.AddWithValue("@g", _rolActual);
                    cmd.Parameters.AddWithValue("@r", rutaArchivo);
                    cmd.ExecuteNonQuery();
                }
                CargarHistorialReportes();
            }
            catch { }
        }

       

    

        // =========================================================
        // --- PESTAÑA: GESTIÓN DE ACCESOS Y USUARIOS ---
        // =========================================================

        private void CargarUsuarios()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT Id, Username, Rol FROM Usuarios_Sistema"; 
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(new SQLiteCommand(query, conexion));
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);
                    dgUsuarios.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar usuarios: " + ex.Message); }
        }

        private void BtnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUserName.Text) || cmbUserRol.SelectedItem == null)
            {
                MessageBox.Show("El nombre de usuario y el rol son obligatorios.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    SQLiteCommand cmd = new SQLiteCommand();
                    cmd.Connection = conexion;

                    if (_idUsuarioSeleccionado == 0)
                    {
                        if (string.IsNullOrWhiteSpace(txtUserPass.Password))
                        {
                            MessageBox.Show("Debe asignar una contraseña al nuevo usuario.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        cmd.CommandText = "INSERT INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES (@user, @pass, @rol)";
                        cmd.Parameters.AddWithValue("@user", txtUserName.Text.Trim());
                        cmd.Parameters.AddWithValue("@pass", txtUserPass.Password); 
                        cmd.Parameters.AddWithValue("@rol", ((ComboBoxItem)cmbUserRol.SelectedItem).Content.ToString());
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(txtUserPass.Password))
                        {
                            cmd.CommandText = "UPDATE Usuarios_Sistema SET Username=@user, Rol=@rol WHERE Id=@id";
                        }
                        else
                        {
                            cmd.CommandText = "UPDATE Usuarios_Sistema SET Username=@user, PasswordHash=@pass, Rol=@rol WHERE Id=@id";
                            cmd.Parameters.AddWithValue("@pass", txtUserPass.Password);
                        }
                        cmd.Parameters.AddWithValue("@user", txtUserName.Text.Trim());
                        cmd.Parameters.AddWithValue("@rol", ((ComboBoxItem)cmbUserRol.SelectedItem).Content.ToString());
                        cmd.Parameters.AddWithValue("@id", _idUsuarioSeleccionado);
                    }

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Usuario guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnLimpiarUsuario_Click(null, null);
                    CargarUsuarios();
                }
            }
            catch (SQLiteException ex)
            {
                if (ex.ResultCode == SQLiteErrorCode.Constraint)
                    MessageBox.Show("Ese nombre de usuario ya existe. Elija otro.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void BtnEditarUsuario_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                _idUsuarioSeleccionado = Convert.ToInt32(boton.CommandParameter);

                try
                {
                    using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                    {
                        SQLiteCommand cmd = new SQLiteCommand("SELECT Username, Rol FROM Usuarios_Sistema WHERE Id=@id", conexion);
                        cmd.Parameters.AddWithValue("@id", _idUsuarioSeleccionado);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtUserName.Text = reader["Username"].ToString();
                                
                                foreach (ComboBoxItem item in cmbUserRol.Items)
                                {
                                    if (item.Content.ToString() == reader["Rol"].ToString())
                                    {
                                        cmbUserRol.SelectedItem = item;
                                        break;
                                    }
                                }
                                txtUserPass.Password = ""; 
                            }
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error al cargar usuario: " + ex.Message); }
            }
        }

        private void BtnEliminarUsuario_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                int idEliminar = Convert.ToInt32(boton.CommandParameter);

                if (MessageBox.Show("¿Está seguro de eliminar este acceso permanentemente?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                        {
                            SQLiteCommand cmd = new SQLiteCommand("DELETE FROM Usuarios_Sistema WHERE Id=@id", conexion);
                            cmd.Parameters.AddWithValue("@id", idEliminar);
                            cmd.ExecuteNonQuery();
                        }
                        CargarUsuarios();
                    }
                    catch (Exception ex) { MessageBox.Show("Error al eliminar: " + ex.Message); }
                }
            }
        }

        private void BtnLimpiarUsuario_Click(object sender, RoutedEventArgs e)
        {
            _idUsuarioSeleccionado = 0;
            txtUserName.Text = "";
            txtUserPass.Password = "";
            cmbUserRol.SelectedIndex = -1;
        }

        private void CargarMetricasDashboard()
{
    try
    {
        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
        {
            // 1. Contar Total de Personal Activo
            string qTotal = "SELECT COUNT(*) FROM Personal_Naval WHERE Estatus = 'ACTIVO'";
            SQLiteCommand cmd1 = new SQLiteCommand(qTotal, conexion);
            int total = Convert.ToInt32(cmd1.ExecuteScalar());

            // 2. Contar Presentes Hoy (Matrículas únicas en Registro_Accesos con fecha de hoy)
            string hoy = DateTime.Now.ToString("yyyy-MM-dd");
            string qPresentes = $"SELECT COUNT(DISTINCT Matricula) FROM Registro_Accesos WHERE FechaHora LIKE '{hoy}%' AND MensajeAcceso LIKE '%ACCESO%'";
            SQLiteCommand cmd2 = new SQLiteCommand(qPresentes, conexion);
            int presentes = Convert.ToInt32(cmd2.ExecuteScalar());

            // 3. Contar personal con Novedades (Vacaciones, Comisión, etc.)
            string qNovedades = "SELECT COUNT(*) FROM Personal_Naval WHERE Estatus = 'ACTIVO' AND Novedad != 'PRESENTE'";
            SQLiteCommand cmd3 = new SQLiteCommand(qNovedades, conexion);
            int novedades = Convert.ToInt32(cmd3.ExecuteScalar());

            // 4. Calcular Faltas (Total - Presentes - Novedades)
            int faltas = total - presentes - novedades;
            if (faltas < 0) faltas = 0;

            // 🔥 ENVIAR LOS DATOS AL DASHBOARD
            ucDashboard.ActualizarDatos(total, presentes, faltas, novedades);
        }
    }
    catch (Exception ex)
    {
        // Si hay error, al menos dejamos los ceros
        Console.WriteLine("Error en Dashboard: " + ex.Message);
    }
}
        


private void UcDashboard_TarjetaClickeada(object sender, string tipoFiltro)
{
    try
    {
        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
        {
            string hoy = DateTime.Now.ToString("yyyy-MM-dd");
            string query = "";

            // 💡 Adaptamos la consulta SQL según la tarjeta que presionaste
            switch (tipoFiltro)
            {
                case "TOTAL":
                    // Muestra a todos los activos
                    query = @"SELECT '-' as FechaHora, Matricula, (Nombres || ' ' || Apellidos) as NombreCompleto, 
                              ('ESTATUS: ' || Estatus || ' | NOVEDAD: ' || Novedad) as MensajeAcceso 
                              FROM Personal_Naval WHERE Estatus = 'ACTIVO'";
                    break;

                case "PRESENTES":
                    query = $@"SELECT MIN(FechaHora) as FechaHora, Matricula, 
                               (SELECT Nombres || ' ' || Apellidos FROM Personal_Naval WHERE Matricula = Registro_Accesos.Matricula) as NombreCompleto, 
                               'ACCESO CONFIRMADO' as MensajeAcceso 
                               FROM Registro_Accesos 
                               WHERE FechaHora LIKE '{hoy}%' AND MensajeAcceso LIKE '%ACCESO%'
                               GROUP BY Matricula";
                    break;

                case "FALTAS":
                    // Muestra a los que NO están en la tabla de accesos de hoy, pero deberían estar
                    query = $@"SELECT '-' as FechaHora, Matricula, (Nombres || ' ' || Apellidos) as NombreCompleto, 
                               '⚠️ FALTA NO JUSTIFICADA' as MensajeAcceso 
                               FROM Personal_Naval 
                               WHERE Estatus = 'ACTIVO' AND Novedad = 'PRESENTE' 
                               AND Matricula NOT IN (SELECT Matricula FROM Registro_Accesos WHERE FechaHora LIKE '{hoy}%')";
                    break;

                case "NOVEDADES":
                    // Muestra a los de vacaciones, comisión, etc.
                    query = @"SELECT '-' as FechaHora, Matricula, (Nombres || ' ' || Apellidos) as NombreCompleto, 
                              ('🛑 JUSTIFICADO: ' || Novedad) as MensajeAcceso 
                              FROM Personal_Naval WHERE Estatus = 'ACTIVO' AND Novedad != 'PRESENTE'";
                    break;
            }

            // Ejecutamos la consulta y rellenamos la tabla directamente
            SQLiteCommand cmd = new SQLiteCommand(query, conexion);
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                System.Data.DataTable dt = new System.Data.DataTable();
                da.Fill(dt);
                dgBitacora.ItemsSource = dt.DefaultView;
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error al cargar detalle: " + ex.Message);
    }
}





        #region ARCHIVO HISTÓRICO Y DOCUMENTOS
        // --- SEPARADOR DE DOCUMENTOS EN LAS TABLAS ---
       private void CargarHistorialReportes()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    // 1. Llenamos la tabla de PDFs
                    string queryPDF = "SELECT IdReporte, FechaGeneracion, Turno, GeneradoPor, RutaArchivo FROM Historial_Reportes WHERE RutaArchivo LIKE '%.pdf' ORDER BY IdReporte DESC";
                    using (SQLiteCommand cmdPDF = new SQLiteCommand(queryPDF, conexion))
                    using (SQLiteDataAdapter daPDF = new SQLiteDataAdapter(cmdPDF))
                    {
                        System.Data.DataTable dtPDF = new System.Data.DataTable();
                        daPDF.Fill(dtPDF);
                        dgReportesPDF.ItemsSource = dtPDF.DefaultView;
                    }

                    // 2. Llenamos la tabla de Excels
                    string queryExcel = "SELECT IdReporte, FechaGeneracion, Turno, GeneradoPor, RutaArchivo FROM Historial_Reportes WHERE RutaArchivo LIKE '%.csv' OR RutaArchivo LIKE '%.xlsx' OR RutaArchivo LIKE '%.xml' ORDER BY IdReporte DESC";
                    using (SQLiteCommand cmdExcel = new SQLiteCommand(queryExcel, conexion))
                    using (SQLiteDataAdapter daExcel = new SQLiteDataAdapter(cmdExcel))
                    {
                        System.Data.DataTable dtExcel = new System.Data.DataTable();
                        daExcel.Fill(dtExcel);
                        dgReportesExcel.ItemsSource = dtExcel.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar el historial clasificado: " + ex.Message);
            }
        }

        // --- MOTOR INTELIGENTE ÚNICO PARA ABRIR CUALQUIER ARCHIVO ---
        private void BtnAbrirDocumento_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                System.Data.DataRowView row = btn.DataContext as System.Data.DataRowView;

                if (row != null)
                {
                    // Tomamos la ruta EXACTA que se guardó en la base de datos
                    string rutaExacta = row["RutaArchivo"].ToString();

                    if (System.IO.File.Exists(rutaExacta))
                    {
                        // Dejamos que Windows decida con qué programa abrirlo (Acrobat o Excel)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                        {
                            FileName = rutaExacta,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show($"No se encontró el archivo físicamente en la computadora.\n\nRuta buscada: {rutaExacta}\n\nEs posible que alguien lo haya movido o borrado de la carpeta.", "Archivo Extraviado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de Windows al intentar abrir el archivo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
      

        // 2. Botón de Actualizar Lista
        private void BtnActualizarHistorial_Click(object sender, RoutedEventArgs e)
        {
            CargarHistorialReportes();
        }

      

      
        // 4. Botón de la sub-pestaña para abrir la carpeta principal de la app
       private void BtnAbrirCarpeta_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Instanciamos el selector de carpetas moderno de .NET 8
                Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog();
                dialog.Title = "Seleccione la bóveda de reportes que desea revisar";

                // Si el usuario elige una carpeta y le da clic a "Seleccionar Carpeta"
                if (dialog.ShowDialog() == true)
                {
                    string rutaElegida = dialog.FolderName;

                    // Le pedimos a Windows que abra exactamente esa ruta
                    System.Diagnostics.Process.Start("explorer.exe", rutaElegida);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el explorador de Windows: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

    }
}