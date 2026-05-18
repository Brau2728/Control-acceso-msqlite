using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Win32;

// LIBRERÍAS DE QUESTPDF PARA GENERAR EL PDF OFICIAL
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace prueba1
{
    public partial class ReportesControl : UserControl
    {
        private string _rolActual;
        private string _resumenActual = "";
        
        private string _matriculaJustificar = "";
        private string _nombreJustificar = "";
        private string _registroAuditoriaInalterable = "";

        public ReportesControl(string rolUsuario)
        {
            InitializeComponent();
            _rolActual = rolUsuario;

            dpFechaInicio.SelectedDate = DateTime.Now;
            dpFechaFin.SelectedDate = DateTime.Now;
            dpCorteFecha.SelectedDate = DateTime.Now;
            dpConcentradoMes.SelectedDate = DateTime.Now;

            QuestPDF.Settings.License = LicenseType.Community;

            CargarBitacora();
            CargarArchivoHistorico();
        }

        private void TabBitacora_Click(object sender, RoutedEventArgs e) { TabReportes.SelectedIndex = 0; CargarTarjetasEnVivo(); }
        private void TabCorte_Click(object sender, RoutedEventArgs e) { TabReportes.SelectedIndex = 1; }
        private void TabConcentrado_Click(object sender, RoutedEventArgs e) { TabReportes.SelectedIndex = 2; }
        private void TabArchivo_Click(object sender, RoutedEventArgs e) { TabReportes.SelectedIndex = 3; CargarArchivoHistorico(); }

        // =========================================================
        // --- 1. BITÁCORA Y TARJETAS EN VIVO ---
        // =========================================================
        private void BtnFiltrarBitacora_Click(object sender, RoutedEventArgs e) { CargarBitacora(); }

        private void CargarBitacora()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = @"SELECT r.FechaHora, r.Matricula, IFNULL(p.Nombres || ' ' || p.Apellidos, 'DESCONOCIDO') AS NombreCompleto, r.MensajeAcceso, r.NovedadMomento
                                     FROM Registro_Accesos r LEFT JOIN Personal_Naval p ON r.Matricula = p.Matricula WHERE 1=1 ";

                    if (!string.IsNullOrWhiteSpace(txtFiltroMatricula.Text)) query += $" AND r.Matricula LIKE '%{txtFiltroMatricula.Text}%' ";
                    if (dpFechaInicio.SelectedDate.HasValue) query += $" AND r.FechaHora >= '{dpFechaInicio.SelectedDate.Value.ToString("yyyy-MM-dd")} {txtHoraInicio.Text}:00' ";
                    if (dpFechaFin.SelectedDate.HasValue) query += $" AND r.FechaHora <= '{dpFechaFin.SelectedDate.Value.ToString("yyyy-MM-dd")} {txtHoraFin.Text}:59' ";
                    if (chkOcultarFallos.IsChecked == true) query += " AND r.MensajeAcceso NOT LIKE '%RECHAZADO%' AND r.MensajeAcceso NOT LIKE '%DENEGADO%' ";
                    
                    query += " ORDER BY r.FechaHora DESC";

                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(new SQLiteCommand(query, conexion));
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);
                    dgBitacora.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar bitácora: " + ex.Message); }
            
            CargarTarjetasEnVivo();
        }

        private void CargarTarjetasEnVivo()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string hoy = DateTime.Now.ToString("yyyy-MM-dd");
                    int total = Convert.ToInt32(new SQLiteCommand("SELECT COUNT(*) FROM Personal_Naval WHERE Estatus = 'ACTIVO'", conexion).ExecuteScalar());
                    int presentes = Convert.ToInt32(new SQLiteCommand($"SELECT COUNT(DISTINCT Matricula) FROM Registro_Accesos WHERE FechaHora LIKE '{hoy}%' AND MensajeAcceso LIKE '%ACCESO%'", conexion).ExecuteScalar());
                    int novedades = Convert.ToInt32(new SQLiteCommand("SELECT COUNT(*) FROM Personal_Naval WHERE Estatus = 'ACTIVO' AND Novedad != 'PRESENTE'", conexion).ExecuteScalar());
                    
                    int faltas = total - presentes - novedades;
                    if (faltas < 0) faltas = 0;

                    cardTotal.Text = total.ToString();
                    cardPresentes.Text = presentes.ToString();
                    cardFaltas.Text = faltas.ToString();
                    cardNovedades.Text = novedades.ToString();
                }
            }
            catch { }
        }

        // =========================================================
        // --- 2. CORTE DE TURNO Y JUSTIFICACIÓN ---
        // =========================================================
        private void BtnGenerarVistaPrevia_Click(object sender, RoutedEventArgs e)
        {
            if (!dpCorteFecha.SelectedDate.HasValue) return;

            string fecha = dpCorteFecha.SelectedDate.Value.ToString("yyyy-MM-dd");
            string horaInicio = txtCorteHoraInicio.Text;
            string horaFin = txtCorteHoraFin.Text;
            TimeSpan horaLimite = TimeSpan.Parse(txtCorteHoraLimite.Text); 

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = $@"
                        SELECT p.Matricula, p.Nombres || ' ' || p.Apellidos AS NombreCompleto, p.IdJefatura, p.Novedad,
                               (SELECT MIN(r.FechaHora) FROM Registro_Accesos r WHERE r.Matricula = p.Matricula AND r.FechaHora >= '{fecha} {horaInicio}:00' AND r.FechaHora <= '{fecha} {horaFin}:59') AS HoraEntrada,
                               (SELECT MAX(r.MensajeAcceso) FROM Registro_Accesos r WHERE r.Matricula = p.Matricula AND r.FechaHora LIKE '{fecha}%' AND r.MensajeAcceso LIKE '%JUSTIFICADO%') AS TieneJustificacion
                        FROM Personal_Naval p WHERE p.Estatus = 'ACTIVO' ORDER BY p.IdJefatura";

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dtAsistencia = new DataTable();
                        dtAsistencia.Columns.Add("Matricula");
                        dtAsistencia.Columns.Add("NombreCompleto");
                        dtAsistencia.Columns.Add("Jefatura");
                        dtAsistencia.Columns.Add("HoraEntrada");
                        dtAsistencia.Columns.Add("Situacion");
                        dtAsistencia.Columns.Add("ColorFondo");
                        dtAsistencia.Columns.Add("ColorTexto");

                        int cTotal = 0, cPresentes = 0, cFaltas = 0, cNovedades = 0;

                        while (reader.Read())
                        {
                            cTotal++;
                            DataRow row = dtAsistencia.NewRow();
                            row["Matricula"] = reader["Matricula"].ToString();
                            row["NombreCompleto"] = reader["NombreCompleto"].ToString();
                            row["Jefatura"] = ObtenerNombreJefatura(Convert.ToInt32(reader["IdJefatura"]));
                            
                            string novedadAct = reader["Novedad"].ToString();
                            string horaEntradaCompleta = reader["HoraEntrada"].ToString();
                            string justificacion = reader["TieneJustificacion"].ToString();

                            if (!string.IsNullOrEmpty(justificacion))
                            {
                                row["Situacion"] = "JUSTIFICADO";
                                row["HoraEntrada"] = "-";
                                row["ColorFondo"] = "#DBEAFE"; row["ColorTexto"] = "#1D4ED8"; 
                                cNovedades++;
                            }
                            else if (novedadAct != "PRESENTE")
                            {
                                row["Situacion"] = novedadAct; 
                                row["HoraEntrada"] = "-";
                                row["ColorFondo"] = "#FEF3C7"; row["ColorTexto"] = "#D97706"; 
                                cNovedades++;
                            }
                            else if (!string.IsNullOrEmpty(horaEntradaCompleta))
                            {
                                TimeSpan horaLlegada = Convert.ToDateTime(horaEntradaCompleta).TimeOfDay;
                                row["HoraEntrada"] = horaLlegada.ToString(@"hh\:mm");

                                if (horaLlegada <= horaLimite)
                                {
                                    row["Situacion"] = "PRESENTE";
                                    row["ColorFondo"] = "#D1FAE5"; row["ColorTexto"] = "#065F46"; 
                                    cPresentes++;
                                }
                                else
                                {
                                    row["Situacion"] = "RETARDO";
                                    row["ColorFondo"] = "#FEE2E2"; row["ColorTexto"] = "#991B1B"; 
                                    cFaltas++;
                                }
                            }
                            else
                            {
                                row["Situacion"] = "FALTISTA";
                                row["HoraEntrada"] = "SIN LECTURA";
                                row["ColorFondo"] = "#FEE2E2"; row["ColorTexto"] = "#991B1B"; 
                                cFaltas++;
                            }

                            dtAsistencia.Rows.Add(row);
                        }
                        
                        dgCorteAsistencia.ItemsSource = dtAsistencia.DefaultView;
                        _resumenActual = $"Total: {cTotal} | Presentes: {cPresentes} | Faltas/Retardos: {cFaltas} | Novedades: {cNovedades}";
                        txtResumenCorte.Text = _resumenActual;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error en el cálculo: " + ex.Message); }
        }

        // Justificación Manual
        private void dgCorteAsistencia_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgCorteAsistencia.SelectedItem is DataRowView row)
            {
                _matriculaJustificar = row["Matricula"].ToString();
                _nombreJustificar = row["NombreCompleto"].ToString();
                string situacion = row["Situacion"].ToString();

                if (situacion == "PRESENTE")
                {
                    MessageBox.Show("Este elemento ya cuenta con asistencia verificada.", "No requiere acción", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                txtMatriculaJustificar.Text = $"Elemento: {_nombreJustificar} ({_matriculaJustificar}) | Estado actual: {situacion}";
                txtNotaJustificacion.Text = "";
                PanelJustificacion.Visibility = Visibility.Visible;
            }
        }

        private void BtnAbrirJustificacion_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                _matriculaJustificar = boton.CommandParameter.ToString();
                txtMatriculaJustificar.Text = $"Matrícula a justificar: {_matriculaJustificar}";
                txtNotaJustificacion.Text = "";
                PanelJustificacion.Visibility = Visibility.Visible;
            }
        }

        private void BtnGuardarJustificacion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNotaJustificacion.Text))
            {
                MessageBox.Show("Debe escribir el motivo de la corrección.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    cmd.Parameters.AddWithValue("@motivo", "AUDITORÍA: " + txtNotaJustificacion.Text.Trim());
                    cmd.ExecuteNonQuery();
                }

                _registroAuditoriaInalterable += $"[{DateTime.Now:HH:mm}] El usuario {_rolActual} justificó a {_matriculaJustificar}. Motivo: {txtNotaJustificacion.Text.Trim()}\n";
                txtAuditoriaSistema.Text = _registroAuditoriaInalterable;

                PanelJustificacion.Visibility = Visibility.Collapsed;
                BtnGenerarVistaPrevia_Click(null, null); 
                MessageBox.Show("Asistencia corregida y auditada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al justificar: " + ex.Message); }
        }

        private void BtnCancelarJustificacion_Click(object sender, RoutedEventArgs e) { PanelJustificacion.Visibility = Visibility.Collapsed; }


        // =========================================================
        // --- 3. REPORTE CONCENTRADO ---
        // =========================================================
        private void BtnGenerarConcentrado_Click(object sender, RoutedEventArgs e)
        {
            if (!dpConcentradoMes.SelectedDate.HasValue) return;
            string mes = dpConcentradoMes.SelectedDate.Value.ToString("MM");
            string anio = dpConcentradoMes.SelectedDate.Value.ToString("yyyy");
            string tolerancia = "07:15:00"; 

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = $@"
                        SELECT 
                            p.Matricula, 
                            p.Nombres || ' ' || p.Apellidos AS Nombre, 
                            p.IdJefatura,
                            (SELECT COUNT(DISTINCT date(r1.FechaHora)) FROM Registro_Accesos r1 WHERE r1.Matricula = p.Matricula AND strftime('%m', r1.FechaHora) = '{mes}' AND strftime('%Y', r1.FechaHora) = '{anio}' AND time(r1.FechaHora) <= '{tolerancia}' AND r1.MensajeAcceso NOT LIKE '%JUSTIFICACIÓN%') AS Asistencias,
                            (SELECT COUNT(DISTINCT date(r2.FechaHora)) FROM Registro_Accesos r2 WHERE r2.Matricula = p.Matricula AND strftime('%m', r2.FechaHora) = '{mes}' AND strftime('%Y', r2.FechaHora) = '{anio}' AND time(r2.FechaHora) > '{tolerancia}' AND r2.MensajeAcceso NOT LIKE '%JUSTIFICACIÓN%') AS Retardos,
                            (SELECT COUNT(r3.IdRegistro) FROM Registro_Accesos r3 WHERE r3.Matricula = p.Matricula AND strftime('%m', r3.FechaHora) = '{mes}' AND strftime('%Y', r3.FechaHora) = '{anio}' AND r3.MensajeAcceso LIKE '%JUSTIFICACIÓN%') AS Justificados,
                            p.Novedad
                        FROM Personal_Naval p WHERE p.Estatus = 'ACTIVO'";

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Columns.Add("Matricula");
                        dt.Columns.Add("Nombre");
                        dt.Columns.Add("Area");
                        dt.Columns.Add("TotalAsistencias");
                        dt.Columns.Add("TotalRetardos");
                        dt.Columns.Add("TotalFaltas");
                        dt.Columns.Add("TotalJustificados");

                        int diasEnMes = DateTime.DaysInMonth(Convert.ToInt32(anio), Convert.ToInt32(mes));

                        while (reader.Read())
                        {
                            DataRow row = dt.NewRow();
                            row["Matricula"] = reader["Matricula"].ToString();
                            row["Nombre"] = reader["Nombre"].ToString();
                            row["Area"] = ObtenerNombreJefatura(Convert.ToInt32(reader["IdJefatura"]));
                            
                            int asistencias = Convert.ToInt32(reader["Asistencias"]);
                            int retardos = Convert.ToInt32(reader["Retardos"]);
                            int justificados = Convert.ToInt32(reader["Justificados"]);
                            
                            int faltasAprox = diasEnMes - (asistencias + retardos + justificados);
                            if (faltasAprox < 0) faltasAprox = 0;
                            if (reader["Novedad"].ToString() != "PRESENTE") faltasAprox = 0; 

                            row["TotalAsistencias"] = asistencias.ToString();
                            row["TotalRetardos"] = retardos.ToString();
                            row["TotalJustificados"] = justificados.ToString();
                            row["TotalFaltas"] = faltasAprox.ToString();

                            dt.Rows.Add(row);
                        }
                        dgConcentrado.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error en concentrado: " + ex.Message); }
        }

        // =========================================================
        // --- 4. EXPORTACIÓN BIOMÉTRICA (EXCEL Y PDF OFICIAL) ---
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

            string autorizador = "";
            if (!SolicitarFirmaBiometrica(out autorizador)) return;

            SaveFileDialog dlg = new SaveFileDialog { 
                FileName = "Corte_Oficial_" + DateTime.Now.ToString("yyyyMMdd_HHmm"), 
                Filter = "Archivo Excel (.xls)|*.xls" 
            };

            if (dlg.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<html xmlns:x=\"urn:schemas-microsoft-com:office:excel\">");
                sb.AppendLine("<head><meta charset=\"utf-8\"><style>");
                sb.AppendLine("table { border-collapse: collapse; font-family: Arial; }");
                sb.AppendLine("th { background-color: #0F172A; color: white; padding: 10px; border: 1px solid #CBD5E1; }");
                sb.AppendLine("td { padding: 8px; border: 1px solid #CBD5E1; text-align: center; }");
                sb.AppendLine(".presente { color: #065F46; background-color: #D1FAE5; font-weight: bold; }");
                sb.AppendLine(".falta { color: #991B1B; background-color: #FEE2E2; font-weight: bold; }");
                sb.AppendLine(".novedad { color: #D97706; background-color: #FEF3C7; font-weight: bold; }");
                sb.AppendLine("</style></head><body>");
                
                sb.AppendLine("<h2>SECRETARÍA DE MARINA - ESTADO DE FUERZA</h2>");
                sb.AppendLine($"<p><b>Fecha:</b> {dpCorteFecha.SelectedDate.Value.ToString("dd/MM/yyyy")} | <b>Turno:</b> {txtCorteHoraInicio.Text} a {txtCorteHoraFin.Text}</p>");
                sb.AppendLine($"<p><b>Validado Biomátricamente por:</b> {autorizador.ToUpper()}</p>");
                sb.AppendLine($"<p><b>Resumen:</b> {_resumenActual}</p>");
                
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>MATRÍCULA</th><th>NOMBRE COMPLETO</th><th>ÁREA</th><th>LLEGADA</th><th>SITUACIÓN</th></tr>");

                foreach (DataRowView row in dgCorteAsistencia.ItemsSource) 
                {
                    string sit = row["Situacion"].ToString();
                    string clase = sit == "PRESENTE" ? "presente" : (sit == "FALTISTA" || sit == "RETARDO" ? "falta" : "novedad");
                    sb.AppendLine($"<tr><td>{row["Matricula"]}</td><td style='text-align:left;'>{row["NombreCompleto"]}</td><td>{row["Jefatura"]}</td><td>{row["HoraEntrada"]}</td><td class='{clase}'>{sit}</td></tr>");
                }
                sb.AppendLine("</table></body></html>");
                
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                
                RegistrarEnHistorial(dlg.FileName, autorizador);
                MessageBox.Show($"Corte Excel exportado exitosamente por {autorizador}.", "Autorizado", MessageBoxButton.OK, MessageBoxImage.Information);
                
                txtObservacionesCorte.Text = ""; _registroAuditoriaInalterable = ""; txtAuditoriaSistema.Text = "";
            }
        }

        private void BtnExportarPDF_Click(object sender, RoutedEventArgs e)
        {
            if (dgCorteAsistencia.Items.Count == 0) return;

            string autorizador = "";
            if (!SolicitarFirmaBiometrica(out autorizador)) return;

            SaveFileDialog dlg = new SaveFileDialog { 
                FileName = "Corte_Oficial_" + DateTime.Now.ToString("yyyyMMdd_HHmm"), 
                Filter = "Documento PDF (.pdf)|*.pdf" 
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    DataTable dt = ((DataView)dgCorteAsistencia.ItemsSource).Table;
                    string fechaTexto = dpCorteFecha.SelectedDate.Value.ToString("dd/MM/yyyy");

                    // EL DISEÑO EXACTO ORIGINAL QUE PEDISTE
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(1.5f, Unit.Centimetre);
                            page.PageColor(QuestPDF.Helpers.Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                            page.Header().PaddingBottom(10).BorderBottom(2).BorderColor(QuestPDF.Helpers.Colors.Grey.Darken2).Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("ARMADA DE MÉXICO").FontSize(16).Bold().FontColor(QuestPDF.Helpers.Colors.Black);
                                    col.Item().Text("SISTEMA INTEGRAL DE CONTROL BIOMÉTRICO").FontSize(12).FontColor(QuestPDF.Helpers.Colors.Grey.Darken3);
                                    col.Item().Text("REPORTE OFICIAL DE ESTADO DE FUERZA").FontSize(14).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken4);
                                    col.Item().PaddingTop(5).Text($"FECHA: {fechaTexto}   |   TURNO: {txtCorteHoraInicio.Text} hrs - {txtCorteHoraFin.Text} hrs").FontSize(10);
                                });
                                row.ConstantItem(60).AlignCenter().Text("⚓").FontSize(40);
                            });

                            page.Content().PaddingVertical(15).Column(col =>
                            {
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(60);
                                        columns.RelativeColumn();
                                        columns.ConstantColumn(80);
                                        columns.ConstantColumn(60);
                                        columns.ConstantColumn(80);
                                    });

                                    string[] cabeceras = { "MATRÍCULA", "NOMBRE COMPLETO", "JEFATURA", "LLEGADA", "SITUACIÓN" };
                                    foreach (var c in cabeceras)
                                        table.Cell().Background(QuestPDF.Helpers.Colors.Blue.Darken4).Padding(5).Text(c).FontColor(QuestPDF.Helpers.Colors.White).Bold().FontSize(9);

                                    bool alternate = false;
                                    foreach (DataRow fila in dt.Rows)
                                    {
                                        var bgColor = alternate ? QuestPDF.Helpers.Colors.Grey.Lighten4 : QuestPDF.Helpers.Colors.White;
                                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(4).Text(fila["Matricula"].ToString()).FontSize(8);
                                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(4).Text(fila["NombreCompleto"].ToString()).FontSize(8);
                                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(4).Text(fila["Jefatura"].ToString()).FontSize(8);
                                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(4).Text(fila["HoraEntrada"].ToString()).FontSize(8);
                                        
                                        string sit = fila["Situacion"].ToString();
                                        var sitColor = sit == "PRESENTE" ? QuestPDF.Helpers.Colors.Green.Darken2 : (sit == "FALTISTA" ? QuestPDF.Helpers.Colors.Red.Darken2 : QuestPDF.Helpers.Colors.Orange.Darken3);
                                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(4).Text(sit).FontColor(sitColor).Bold().FontSize(8);
                                        
                                        alternate = !alternate;
                                    }
                                });

                                col.Item().PaddingTop(20).Text("RESUMEN OPERATIVO:").Bold().FontSize(11);
                                col.Item().Text(_resumenActual).FontSize(10);
                            });

                            page.Footer().PaddingTop(20).AlignCenter().Column(col =>
                            {
                                col.Item().AlignCenter().Text("___________________________________________________").Bold();
                                col.Item().AlignCenter().Text($"VALIDADO MEDIANTE FIRMA BIOMÉTRICA POR:").FontSize(8);
                                col.Item().AlignCenter().Text(autorizador.ToUpper()).FontSize(10).Bold();
                                col.Item().AlignCenter().PaddingTop(5).Text(x => {
                                    x.Span("Generado el: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                                });
                            });
                        });
                    }).GeneratePdf(dlg.FileName);

                    RegistrarEnHistorial(dlg.FileName, autorizador);
                    MessageBox.Show($"PDF exportado y sellado exitosamente por {autorizador}.", "Autorizado", MessageBoxButton.OK, MessageBoxImage.Information);

                    txtObservacionesCorte.Text = ""; _registroAuditoriaInalterable = ""; txtAuditoriaSistema.Text = "";
                }
                catch (Exception ex) { MessageBox.Show("Error al estructurar el PDF: " + ex.Message, "Error QuestPDF", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        // =========================================================
        // --- 5. ARCHIVO HISTÓRICO Y AUTO-SANACIÓN DE RUTAS ---
        // =========================================================
        private void RegistrarEnHistorial(string rutaArchivo, string autorizadoPor)
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string turno = $"{dpCorteFecha.SelectedDate.Value.ToString("dd/MM/yyyy")} ({txtCorteHoraInicio.Text} a {txtCorteHoraFin.Text})";
                    string query = "INSERT INTO Historial_Reportes (FechaGeneracion, Turno, GeneradoPor, RutaArchivo) VALUES (@f, @t, @g, @r)";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@f", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                        cmd.Parameters.AddWithValue("@t", turno);
                        cmd.Parameters.AddWithValue("@g", autorizadoPor);
                        cmd.Parameters.AddWithValue("@r", rutaArchivo);
                        cmd.ExecuteNonQuery();
                    }
                }
                CargarArchivoHistorico();
            }
            catch { }
        }

        private void CargarArchivoHistorico()
        {
            if (dgArchivoHistorico == null) return;
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = @"SELECT FechaGeneracion, Turno, GeneradoPor, RutaArchivo FROM Historial_Reportes ORDER BY IdReporte DESC";
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(query, conexion);
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);

                    dt.Columns.Add("TipoArchivo", typeof(string));
                    dt.Columns.Add("ColorFondo", typeof(string));
                    dt.Columns.Add("ColorTexto", typeof(string));

                    foreach (DataRow row in dt.Rows)
                    {
                        string ruta = row["RutaArchivo"].ToString();
                        string extension = Path.GetExtension(ruta).ToLower();

                        if (extension == ".pdf")
                        {
                            row["TipoArchivo"] = "PDF"; row["ColorFondo"] = "#FEE2E2"; row["ColorTexto"] = "#DC2626";
                        }
                        else
                        {
                            row["TipoArchivo"] = "EXCEL"; row["ColorFondo"] = "#DCFCE7"; row["ColorTexto"] = "#16A34A";
                        }
                    }
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
                string rutaOriginal = boton.CommandParameter.ToString();
                
                if (File.Exists(rutaOriginal))
                {
                    Process.Start(new ProcessStartInfo(rutaOriginal) { UseShellExecute = true });
                }
                else
                {
                    string nombreArchivo = Path.GetFileName(rutaOriginal);
                    if (MessageBox.Show($"El archivo '{nombreArchivo}' ya no está en su ubicación original.\n\n¿Deseas buscar la nueva carpeta en tu PC donde lo guardaste para revincularlo?", "Archivo Extraviado", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        OpenFileDialog ofd = new OpenFileDialog { FileName = nombreArchivo, Title = $"Localiza el archivo: {nombreArchivo}" };
                        if (ofd.ShowDialog() == true)
                        {
                            string nuevaRuta = ofd.FileName;
                            using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                            {
                                string query = "UPDATE Historial_Reportes SET RutaArchivo = @nueva WHERE RutaArchivo = @vieja";
                                SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                                cmd.Parameters.AddWithValue("@nueva", nuevaRuta);
                                cmd.Parameters.AddWithValue("@vieja", rutaOriginal);
                                cmd.ExecuteNonQuery();
                            }
                            Process.Start(new ProcessStartInfo(nuevaRuta) { UseShellExecute = true });
                            CargarArchivoHistorico();
                        }
                    }
                }
            }
        }

        private void BtnAbrirCarpeta_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog();
                dialog.Title = "Seleccione la bóveda o carpeta que desea revisar en esta computadora";

                if (dialog.ShowDialog() == true)
                {
                    System.Diagnostics.Process.Start("explorer.exe", dialog.FolderName);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al abrir el explorador de Windows: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private string ObtenerNombreJefatura(int id)
        {
            string[] jefaturas = { "Otro", "Talleres", "Servicios", "Detall", "Comunav" };
            if (id >= 1 && id <= jefaturas.Length) return jefaturas[id - 1];
            return "DESCONOCIDA";
        }
    }
}