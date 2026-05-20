using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Data;

// LIBRERÍAS DE QUESTPDF PARA GENERAR EL PDF OFICIAL
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using ClosedXML.Excel;

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

        // 1. Creamos el comando antes para irle agregando los parámetros
        SQLiteCommand cmd = new SQLiteCommand();
        cmd.Connection = conexion;

        // 2. Filtro de Matrícula Seguro
        if (!string.IsNullOrWhiteSpace(txtFiltroMatricula.Text)) 
        {
            query += " AND r.Matricula LIKE @mat ";
            cmd.Parameters.AddWithValue("@mat", "%" + txtFiltroMatricula.Text.Trim() + "%");
        }

        // 3. Filtro de Fechas Seguro
        if (dpFechaInicio.SelectedDate.HasValue) 
        {
            query += " AND r.FechaHora >= @inicio ";
            cmd.Parameters.AddWithValue("@inicio", $"{dpFechaInicio.SelectedDate.Value:yyyy-MM-dd} {txtHoraInicio.Text}:00");
        }

        if (dpFechaFin.SelectedDate.HasValue) 
        {
            query += " AND r.FechaHora <= @fin ";
            cmd.Parameters.AddWithValue("@fin", $"{dpFechaFin.SelectedDate.Value:yyyy-MM-dd} {txtHoraFin.Text}:59");
        }

        if (chkOcultarFallos.IsChecked == true) 
        {
            query += " AND r.MensajeAcceso NOT LIKE '%RECHAZADO%' AND r.MensajeAcceso NOT LIKE '%DENEGADO%' ";
        }
        
        query += " ORDER BY r.FechaHora DESC";

        cmd.CommandText = query;

        SQLiteDataAdapter adaptador = new SQLiteDataAdapter(cmd);
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
                    SELECT p.Matricula, p.Nombres || ' ' || p.Apellidos AS NombreCompleto, p.IdJefatura, p.Novedad, j.NombreJefatura,
                        (SELECT MIN(r.FechaHora) FROM Registro_Accesos r WHERE r.Matricula = p.Matricula AND r.FechaHora >= '{fecha} {horaInicio}:00' AND r.FechaHora <= '{fecha} {horaFin}:59') AS HoraEntrada,
                        (SELECT MAX(r.MensajeAcceso) FROM Registro_Accesos r WHERE r.Matricula = p.Matricula AND r.FechaHora LIKE '{fecha}%' AND r.MensajeAcceso LIKE '%JUSTIFICADO%') AS TieneJustificacion
                    FROM Personal_Naval p 
                    LEFT JOIN Cat_Jefaturas j ON p.IdJefatura = j.IdJefatura
                    WHERE p.Estatus = 'ACTIVO' ORDER BY p.IdJefatura";
                    
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
                            row["Jefatura"] = reader["NombreJefatura"] != DBNull.Value ? reader["NombreJefatura"].ToString() : "DESCONOCIDA";

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
        private async void BtnGenerarConcentrado_Click(object sender, RoutedEventArgs e)
{
    if (!dpConcentradoMes.SelectedDate.HasValue) return;

    string mes = dpConcentradoMes.SelectedDate.Value.ToString("MM");
    string anio = dpConcentradoMes.SelectedDate.Value.ToString("yyyy");
    TimeSpan tolerancia = new TimeSpan(7, 15, 0); 
    int diasEnMes = DateTime.DaysInMonth(Convert.ToInt32(anio), Convert.ToInt32(mes));

    Button btnOrigen = sender as Button;
    if (btnOrigen != null) btnOrigen.IsEnabled = false;

    try
    {
        DataTable dtConcentrado = null;

        await Task.Run(() =>
        {
            using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
            {
                // 1. Extraer accesos
                string queryAccesos = $@"
                    SELECT Matricula, date(FechaHora) AS Fecha, time(FechaHora) AS Hora, MensajeAcceso 
                    FROM Registro_Accesos 
                    WHERE strftime('%m', FechaHora) = '{mes}' AND strftime('%Y', FechaHora) = '{anio}'";
                
                var accesosDict = new Dictionary<string, List<AccesoInfo>>();
                using (SQLiteCommand cmdAcc = new SQLiteCommand(queryAccesos, conexion))
                using (SQLiteDataReader readerAcc = cmdAcc.ExecuteReader())
                {
                    while (readerAcc.Read())
                    {
                        string mat = readerAcc["Matricula"].ToString();
                        if (!accesosDict.ContainsKey(mat)) accesosDict[mat] = new List<AccesoInfo>();
                        
                        string horaStr = readerAcc["Hora"]?.ToString();
                        if (!string.IsNullOrEmpty(horaStr))
                        {
                            accesosDict[mat].Add(new AccesoInfo { 
                                Fecha = readerAcc["Fecha"].ToString(), 
                                Hora = TimeSpan.Parse(horaStr),
                                Mensaje = readerAcc["MensajeAcceso"].ToString()
                            });
                        }
                    }
                }

                // 2. Traer personal
                string queryPersonal = @"
                    SELECT p.Matricula, p.Nombres || ' ' || p.Apellidos AS Nombre, 
                           IFNULL(j.NombreJefatura, 'DESCONOCIDA') AS Area,
                           IFNULL(p.Novedad, 'PRESENTE') AS Novedad, p.FechaInicioNovedad, p.FechaFinNovedad
                    FROM Personal_Naval p
                    LEFT JOIN Cat_Jefaturas j ON p.IdJefatura = j.IdJefatura
                    WHERE p.Estatus = 'ACTIVO' ORDER BY p.IdJefatura";

                DataTable dt = new DataTable();
                dt.Columns.Add("Matricula");
                dt.Columns.Add("Nombre");
                dt.Columns.Add("Area");
                
                // === SOLUCIÓN: COLUMNAS CON PREFIJO PARA EVITAR CRASH === (Ej: Dia01, Dia02)
                for (int i = 1; i <= diasEnMes; i++) dt.Columns.Add("Dia" + i.ToString("D2"));

                dt.Columns.Add("Asist", typeof(int));
                dt.Columns.Add("Retard", typeof(int));
                dt.Columns.Add("Faltas", typeof(int));
                dt.Columns.Add("Noved", typeof(int));

                using (SQLiteCommand cmdPers = new SQLiteCommand(queryPersonal, conexion))
                using (SQLiteDataReader readerPers = cmdPers.ExecuteReader())
                {
                    while (readerPers.Read())
                    {
                        DataRow row = dt.NewRow();
                        string matricula = readerPers["Matricula"].ToString();
                        row["Matricula"] = matricula;
                        row["Nombre"] = readerPers["Nombre"].ToString();
                        row["Area"] = readerPers["Area"].ToString();

                        int cAsistencias = 0, cRetardos = 0, cFaltas = 0, cNovedades = 0;
                        string novedadActual = readerPers["Novedad"].ToString().Trim();
                        if (string.IsNullOrEmpty(novedadActual)) novedadActual = "PRESENTE";

                        DateTime? fIni = readerPers["FechaInicioNovedad"] != DBNull.Value ? Convert.ToDateTime(readerPers["FechaInicioNovedad"]) : (DateTime?)null;
                        DateTime? fFin = readerPers["FechaFinNovedad"] != DBNull.Value ? Convert.ToDateTime(readerPers["FechaFinNovedad"]) : (DateTime?)null;

                        // 3. Evaluar día por día
                        for (int dia = 1; dia <= diasEnMes; dia++)
                        {
                            DateTime fechaActual = new DateTime(Convert.ToInt32(anio), Convert.ToInt32(mes), dia);
                            string fechaStr = fechaActual.ToString("yyyy-MM-dd");
                            string celdaValor = "";
                            bool tieneAcceso = false;

                            if (accesosDict.ContainsKey(matricula))
                            {
                                var accesosDia = accesosDict[matricula].Where(a => a.Fecha == fechaStr).ToList();
                                if (accesosDia.Any())
                                {
                                    tieneAcceso = true;
                                    if (accesosDia.Any(a => a.Mensaje.Contains("JUSTIFICA")))
                                    {
                                        celdaValor = "P"; cAsistencias++;
                                    }
                                    else
                                    {
                                        var primeraLectura = accesosDia.OrderBy(a => a.Hora).First();
                                        if (primeraLectura.Hora <= tolerancia) { celdaValor = "P"; cAsistencias++; }
                                        else { celdaValor = "R"; cRetardos++; }
                                    }
                                }
                            }

                            if (!tieneAcceso)
                            {
                                if (fechaActual > DateTime.Now.Date)
                                {
                                    celdaValor = ""; 
                                }
                                else if (novedadActual != "PRESENTE" && (!fIni.HasValue || fechaActual >= fIni.Value) && (!fFin.HasValue || fechaActual <= fFin.Value))
                                {
                                    switch(novedadActual.ToUpper()) {
                                        case "VACACIONES": celdaValor = "V"; break;
                                        case "COMISION": 
                                        case "COMISIÓN": celdaValor = "C"; break; // Acepta ambas
                                        case "HOSPITALIZADO": 
                                        case "REBAJADO": celdaValor = "N"; break;
                                        default: celdaValor = novedadActual.Length > 0 ? novedadActual.Substring(0,1) : "N"; break;
                                    }
                                    cNovedades++;
                                }
                                else
                                {
                                    celdaValor = "F"; cFaltas++;
                                }
                            }

                            // === ASIGNAMOS USANDO EL PREFIJO ===
                            row["Dia" + dia.ToString("D2")] = celdaValor;
                        }

                        row["Asist"] = cAsistencias; row["Retard"] = cRetardos; 
                        row["Faltas"] = cFaltas; row["Noved"] = cNovedades;
                        dt.Rows.Add(row);
                    }
                }
                dtConcentrado = dt;
            }
        });

        if (dtConcentrado != null)
        {
            // === 1. DESTRUIMOS EL DISEÑO VIEJO DEL XAML ===
            dgConcentrado.Columns.Clear();
            
            // === 2. FORZAMOS A QUE DIBUJE LAS 31 COLUMNAS NUEVAS ===
            dgConcentrado.AutoGenerateColumns = true; 
            
            dgConcentrado.AutoGeneratingColumn -= DgConcentrado_AutoGeneratingColumn;
            dgConcentrado.AutoGeneratingColumn += DgConcentrado_AutoGeneratingColumn;
            dgConcentrado.ItemsSource = dtConcentrado.DefaultView;
        }
    }
    catch (Exception ex) 
    { 
        MessageBox.Show("Error al construir reporte matricial: " + ex.Message, "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error); 
    }
    finally { if (btnOrigen != null) btnOrigen.IsEnabled = true; }
}
// === CLASE AUXILIAR COLOCADA AQUÍ PARA EVITAR ERRORES DE LECTURA ===
private class AccesoInfo 
{
    public string Fecha { get; set; }
    public TimeSpan Hora { get; set; }
    public string Mensaje { get; set; }
}

      private void DgConcentrado_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
{
    if (e.Column is DataGridTextColumn col)
    {
        if (e.PropertyName.StartsWith("Dia") && int.TryParse(e.PropertyName.Substring(3), out int dia))
        {
            col.Binding = new Binding(e.PropertyName);
            col.Width = 48; 
            
            string tituloColumna = dia.ToString("D2");
            if (dpConcentradoMes.SelectedDate.HasValue)
            {
                try
                {
                    DateTime fechaColumna = new DateTime(dpConcentradoMes.SelectedDate.Value.Year, dpConcentradoMes.SelectedDate.Value.Month, dia);
                    string nombreDia = fechaColumna.ToString("ddd", new System.Globalization.CultureInfo("es-MX"));
                    string diaLetras = char.ToUpper(nombreDia[0]) + nombreDia.Substring(1, 1);
                    tituloColumna = $"{diaLetras}\n{dia:D2}";
                }
                catch { } 
            }

            col.Header = tituloColumna;
            
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            string path = e.PropertyName;

            // Formato 'P' -> Verde
            DataTrigger trgP = new DataTrigger { Binding = new Binding(path), Value = "P" };
            trgP.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#065F46"))));
            trgP.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#D1FAE5"))));
            style.Triggers.Add(trgP);

            // Formato 'R' -> Naranja
            DataTrigger trgR = new DataTrigger { Binding = new Binding(path), Value = "R" };
            trgR.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#B45309"))));
            trgR.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FEF3C7"))));
            style.Triggers.Add(trgR);

            // Formato 'F' -> Rojo
            DataTrigger trgF = new DataTrigger { Binding = new Binding(path), Value = "F" };
            trgF.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#991B1B"))));
            trgF.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FEE2E2"))));
            style.Triggers.Add(trgF);

            // Formato 'V' -> Azul (Vacaciones)
            DataTrigger trgV = new DataTrigger { Binding = new Binding(path), Value = "V" };
            trgV.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#1E40AF")))); 
            trgV.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#DBEAFE"))));
            style.Triggers.Add(trgV);

            // === AHORA SÍ: Formato 'C' -> Azul (Comisión) ===
            DataTrigger trgC = new DataTrigger { Binding = new Binding(path), Value = "C" };
            trgC.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#1E40AF")))); 
            trgC.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#DBEAFE"))));
            style.Triggers.Add(trgC);

            // === AHORA SÍ: Formato 'N' -> Azul (Médico / Hospitalizado / Rebajado) ===
            DataTrigger trgN = new DataTrigger { Binding = new Binding(path), Value = "N" };
            trgN.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#1E40AF")))); 
            trgN.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#DBEAFE"))));
            style.Triggers.Add(trgN);
            
            col.ElementStyle = style;
        }
        else if (e.PropertyName == "Matricula" || e.PropertyName == "Nombre" || e.PropertyName == "Area")
        {
            if (e.PropertyName == "Matricula") col.Header = "MATRÍCULA";
            if (e.PropertyName == "Nombre") col.Header = "NOMBRE COMPLETO";
            if (e.PropertyName == "Area") col.Header = "JEFATURA";
            col.Width = DataGridLength.Auto;
        }
        else 
        {
            if (e.PropertyName == "Asist") col.Header = "ASIST";
            if (e.PropertyName == "Retard") col.Header = "RETARD";
            if (e.PropertyName == "Faltas") col.Header = "FALTAS";
            if (e.PropertyName == "Noved") col.Header = "NOVED";

            col.Width = 65;
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#334155"))));
            col.ElementStyle = style;
        }
    }
}

        private void BtnExportarConcentradoExcel_Click(object sender, RoutedEventArgs e)
{
    // Verificamos que la tabla tenga datos en pantalla antes de exportar
    if (dgConcentrado.ItemsSource == null || dgConcentrado.Items.Count == 0)
    {
        MessageBox.Show("Primero debes generar el desglose analítico en pantalla antes de exportar.", "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    SaveFileDialog dlg = new SaveFileDialog
    {
        FileName = "Estado_Fuerza_Mensual_" + dpConcentradoMes.SelectedDate.Value.ToString("yyyy_MM"),
        // ¡OJO! Ahora usamos .xlsx nativo moderno, adiós a los errores de compatibilidad
        Filter = "Libro de Excel (*.xlsx)|*.xlsx" 
    };

    if (dlg.ShowDialog() == true)
    {
        try
        {
            DataView vista = (DataView)dgConcentrado.ItemsSource;
            DataTable dt = vista.Table;

            // Iniciamos la creación del archivo Excel real
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Estado de Fuerza");

                // ==============================================================
                // 1. GENERAR ENCABEZADOS Y ESTILO MILITAR
                // ==============================================================
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    string tituloCabecera = dt.Columns[i].ColumnName;
                    
                    // Renombramos para la presentación
                    if (tituloCabecera.StartsWith("Dia")) tituloCabecera = tituloCabecera.Substring(3);
                    else if (tituloCabecera == "Matricula") tituloCabecera = "MATRÍCULA";
                    else if (tituloCabecera == "Nombre") tituloCabecera = "NOMBRE COMPLETO";
                    else if (tituloCabecera == "Area") tituloCabecera = "JEFATURA";
                    else if (tituloCabecera == "Asist") tituloCabecera = "TOTAL ASIST";
                    else if (tituloCabecera == "Retard") tituloCabecera = "TOTAL RETARD";
                    else if (tituloCabecera == "Faltas") tituloCabecera = "TOTAL FALTAS";
                    else if (tituloCabecera == "Noved") tituloCabecera = "TOTAL NOVED";

                    var celdaHeader = worksheet.Cell(1, i + 1);
                    celdaHeader.Value = tituloCabecera;
                    celdaHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF"); // Azul Marino Institucional
                    celdaHeader.Style.Font.FontColor = XLColor.White;
                    celdaHeader.Style.Font.Bold = true;
                    celdaHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // ==============================================================
                // 2. INYECTAR DATOS Y COLORES EN TIEMPO REAL
                // ==============================================================
                for (int r = 0; r < dt.Rows.Count; r++)
                {
                    for (int c = 0; c < dt.Columns.Count; c++)
                    {
                        var celda = worksheet.Cell(r + 2, c + 1); // R+2 porque la fila 1 son los encabezados
                        string valor = dt.Rows[r][c].ToString();
                        celda.Value = valor;

                        // Alineación por defecto: Centrado para las letras del mes y totales
                        celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        celda.Style.Font.Bold = true;

                        string colName = dt.Columns[c].ColumnName;
                        if (colName == "Nombre" || colName == "Area")
                        {
                            celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                            celda.Style.Font.Bold = false; // Nombres en letra normal
                        }

                        // === MAPEO DE COLORES DE LA LEYENDA ===
                        if (valor == "P") { 
                            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#D1FAE5"); 
                            celda.Style.Font.FontColor = XLColor.FromHtml("#065F46"); 
                        }
                        else if (valor == "R") { 
                            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7"); 
                            celda.Style.Font.FontColor = XLColor.FromHtml("#B45309"); 
                        }
                        else if (valor == "F") { 
                            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2"); 
                            celda.Style.Font.FontColor = XLColor.FromHtml("#991B1B"); 
                        }
                        else if (valor == "V" || valor == "C" || valor == "N") { 
                            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE"); 
                            celda.Style.Font.FontColor = XLColor.FromHtml("#1E40AF"); 
                        }
                        
                        // Le ponemos un borde ligerito a cada celda para que parezca reporte formal
                        celda.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        celda.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
                    }
                }

                // ==============================================================
                // 3. MAGIA: AUTO-AJUSTAR COLUMNAS PARA QUE NADA SE VEA AMONTONADO
                // ==============================================================
                worksheet.Columns().AdjustToContents();

                // Congelamos la primera fila y las columnas de datos personales para que 
                // al navegar por los 31 días hacia la derecha, los nombres no se pierdan de vista
                worksheet.SheetView.FreezeRows(1);
                worksheet.SheetView.FreezeColumns(3);

                // Guardamos el archivo físico
                workbook.SaveAs(dlg.FileName);
            }

            MessageBox.Show("¡Evidencia documental exportada exitosamente en formato Excel Nativo!\n\nEl archivo está listo para auditoría sin mensajes de error.", "Exportación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error crítico al generar archivo de Excel: " + ex.Message, "Error de Guardado", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
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

        private async void BtnExportarPDF_Click(object sender, RoutedEventArgs e)
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
            // ====================================================================
            // FASE 1: LECTURA EN EL HILO DE LA INTERFAZ (UI)
            // Extraemos todos los datos de los controles gráficos antes de ir al fondo.
            // Usamos .Copy() en la tabla para evitar problemas de hilos cruzados.
            // ====================================================================
            DataTable dt = ((DataView)dgCorteAsistencia.ItemsSource).Table.Copy();
            string fechaTexto = dpCorteFecha.SelectedDate.Value.ToString("dd/MM/yyyy");
            string horaInicioTexto = txtCorteHoraInicio.Text;
            string horaFinTexto = txtCorteHoraFin.Text;
            string resumenTexto = _resumenActual;
            string rutaArchivo = dlg.FileName;
            string fechaImpresion = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            // ====================================================================
            // FASE 2: TRABAJO PESADO EN SEGUNDO PLANO (NO CONGELA LA PANTALLA)
            // ====================================================================
            await Task.Run(() =>
            {
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
                                // AQUI USAMOS LAS VARIABLES LOCALES EXTRAÍDAS
                                col.Item().PaddingTop(5).Text($"FECHA: {fechaTexto}   |   TURNO: {horaInicioTexto} hrs - {horaFinTexto} hrs").FontSize(10);
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
                            // USAMOS LA VARIABLE DE RESUMEN
                            col.Item().Text(resumenTexto).FontSize(10); 
                        });

                        page.Footer().PaddingTop(20).AlignCenter().Column(col =>
                        {
                            col.Item().AlignCenter().Text("___________________________________________________").Bold();
                            col.Item().AlignCenter().Text($"VALIDADO MEDIANTE FIRMA BIOMÉTRICA POR:").FontSize(8);
                            col.Item().AlignCenter().Text(autorizador.ToUpper()).FontSize(10).Bold();
                            col.Item().AlignCenter().PaddingTop(5).Text(x => {
                                // USAMOS LA VARIABLE DE FECHA DE IMPRESIÓN
                                x.Span("Generado el: " + fechaImpresion).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                            });
                        });
                    });
                }).GeneratePdf(rutaArchivo);
            });

            // ====================================================================
            // FASE 3: RETORNO AL HILO DE LA INTERFAZ
            // Terminó el PDF, actualizamos la base de datos y limpiamos la pantalla.
            // ====================================================================
            RegistrarEnHistorial(rutaArchivo, autorizador);
            MessageBox.Show($"PDF exportado y sellado exitosamente por {autorizador}.", "Autorizado", MessageBoxButton.OK, MessageBoxImage.Information);

            txtObservacionesCorte.Text = ""; 
            _registroAuditoriaInalterable = ""; 
            txtAuditoriaSistema.Text = "";
        }
        catch (Exception ex) 
        { 
            MessageBox.Show("Error al estructurar el PDF: " + ex.Message, "Error QuestPDF", MessageBoxButton.OK, MessageBoxImage.Error); 
        }
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

       
    }
}