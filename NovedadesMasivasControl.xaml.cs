using System;
using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace prueba1
{
    public partial class NovedadesMasivasControl : UserControl
    {
        private DataTable dtPersonalMasivo;

        public NovedadesMasivasControl()
        {
            InitializeComponent();
            AsegurarColumnaDetalle();

            dpMasivoInicio.SelectedDate = DateTime.Now;
            dpMasivoFin.SelectedDate = DateTime.Now.AddDays(1);
            
            CargarFiltrosArea();
            CargarPersonalMasivo();
        }

        private void AsegurarColumnaDetalle()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN DetalleNovedad TEXT", conexion).ExecuteNonQuery();
                }
            }
            catch { /* Silencioso */ }
        }

        private void CargarPersonalMasivo()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    // 💡 MAGIA: ORDER BY Estatus ASC asegura que los 'ACTIVO' van primero, y 'BAJA' hasta abajo.
                    string query = @"
                    SELECT p.Matricula, p.Nombres || ' ' || p.Apellidos AS Nombre, 
                        p.Novedad AS NovedadTipo, p.Estatus AS EstatusActual,
                        p.FechaInicioNovedad, p.FechaFinNovedad, p.DetalleNovedad,
                        IFNULL(j.NombreJefatura, 'DESCONOCIDA') AS Area
                    FROM Personal_Naval p
                    LEFT JOIN Cat_Jefaturas j ON p.IdJefatura = j.IdJefatura
                    ORDER BY p.Estatus ASC, p.IdJefatura, p.IdGrado";  
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(new SQLiteCommand(query, conexion));
                    dtPersonalMasivo = new DataTable();
                    adaptador.Fill(dtPersonalMasivo);

                    dtPersonalMasivo.Columns.Add("Seleccionado", typeof(bool));
                    dtPersonalMasivo.Columns.Add("NovedadDisplay", typeof(string));
                    dtPersonalMasivo.Columns.Add("TiempoRestante", typeof(string));
                    dtPersonalMasivo.Columns.Add("NovedadTooltip", typeof(string));
                    dtPersonalMasivo.Columns.Add("ColorFondo", typeof(string));
                    dtPersonalMasivo.Columns.Add("ColorTexto", typeof(string));
                    dtPersonalMasivo.Columns.Add("EstatusColorFondo", typeof(string));
                    dtPersonalMasivo.Columns.Add("EstatusColorTexto", typeof(string));

                    foreach (DataRow row in dtPersonalMasivo.Rows) 
                    {
                        row["Seleccionado"] = false;
                        CalcularDatosInteligentes(row);
                    }

                    AplicarFiltrosLocales();
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar personal masivo: " + ex.Message); }
        }

        private void CargarFiltrosArea()
        {
            // Quitamos temporalmente el evento para que no marque errores al cargar
            cmbFiltroArea.SelectionChanged -= CmbFiltroArea_SelectionChanged;

            cmbFiltroArea.Items.Clear();
            cmbFiltroArea.Items.Add(new ComboBoxItem { Content = "TODAS LAS ÁREAS" });

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT NombreJefatura FROM Cat_Jefaturas ORDER BY NombreJefatura ASC";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbFiltroArea.Items.Add(new ComboBoxItem { Content = reader["NombreJefatura"].ToString() });
                        }
                    }
                }
            }
            catch { }

            cmbFiltroArea.SelectedIndex = 0;
            
            // Regresamos el evento a la normalidad
            cmbFiltroArea.SelectionChanged += CmbFiltroArea_SelectionChanged;
        }

        private void CalcularDatosInteligentes(DataRow row)
        {
            string novedadTipo = row["NovedadTipo"].ToString();
            string estatus = row["EstatusActual"].ToString();
            string detalle = row["DetalleNovedad"].ToString();
            DateTime? fIni = row["FechaInicioNovedad"] != DBNull.Value ? Convert.ToDateTime(row["FechaInicioNovedad"]) : (DateTime?)null;
            DateTime? fFin = row["FechaFinNovedad"] != DBNull.Value ? Convert.ToDateTime(row["FechaFinNovedad"]) : (DateTime?)null;

            // 1. Colores de la Etiqueta de Estatus (Alta/Baja)
            if (estatus == "BAJA")
            {
                row["EstatusColorFondo"] = "#FEE2E2"; // Rojo claro
                row["EstatusColorTexto"] = "#991B1B"; // Rojo oscuro
            }
            else
            {
                row["EstatusColorFondo"] = "#DCFCE7"; // Verde claro
                row["EstatusColorTexto"] = "#16A34A"; // Verde oscuro
            }

            // 2. Colores de la Novedad
            row["NovedadDisplay"] = string.IsNullOrEmpty(detalle) ? novedadTipo : $"{novedadTipo} - {detalle}";
            switch (novedadTipo)
            {
                case "PRESENTE": row["ColorFondo"] = "#D1FAE5"; row["ColorTexto"] = "#065F46"; break; 
                case "VACACIONES": row["ColorFondo"] = "#FEF08A"; row["ColorTexto"] = "#854D0E"; break; 
                case "COMISION": row["ColorFondo"] = "#E0E7FF"; row["ColorTexto"] = "#3730A3"; break; 
                case "PERMISO": row["ColorFondo"] = "#FCE7F3"; row["ColorTexto"] = "#9D174D"; break; 
                case "HOSPITALIZADO":
                case "REBAJADO": row["ColorFondo"] = "#FFEDD5"; row["ColorTexto"] = "#9A3412"; break; 
                case "FALTISTA":
                case "ARRESTO": row["ColorFondo"] = "#FEE2E2"; row["ColorTexto"] = "#991B1B"; break; 
                default: row["ColorFondo"] = "#F1F5F9"; row["ColorTexto"] = "#475569"; break; 
            }

            // 3. Tooltip e Inteligencia de tiempos
            string tooltip = $"📍 Situación: {novedadTipo}";
            if (!string.IsNullOrEmpty(detalle)) tooltip += $"\n📋 Detalle / Lugar: {detalle}";

            if (novedadTipo != "PRESENTE" && fIni.HasValue)
            {
                tooltip += $"\n📅 Fecha de Inicio: {fIni.Value.ToString("dd/MMM/yyyy")}";
                
                if (fFin.HasValue)
                {
                    tooltip += $"\n🏁 Fecha de Término: {fFin.Value.ToString("dd/MMM/yyyy")}";
                    int diasTotales = (fFin.Value - fIni.Value).Days + 1; 
                    int diasRestantes = (fFin.Value - DateTime.Now.Date).Days;
                    if (diasRestantes < 0) diasRestantes = 0;

                    tooltip += $"\n⏱️ Duración Total: {diasTotales} días";
                    tooltip += $"\n⏳ Días Restantes: {diasRestantes} días";
                    row["TiempoRestante"] = $"{diasRestantes} Días";
                }
                else
                {
                    tooltip += "\n🏁 Fecha de Término: INDEFINIDO";
                    row["TiempoRestante"] = "Indefinido";
                }
            }
            else
            {
                row["TiempoRestante"] = "-";
            }

            row["NovedadTooltip"] = tooltip;
        }

        private void TxtBuscarMasivo_TextChanged(object sender, TextChangedEventArgs e) { AplicarFiltrosLocales(); }
        private void CmbFiltroArea_SelectionChanged(object sender, SelectionChangedEventArgs e) { AplicarFiltrosLocales(); }

        private void AplicarFiltrosLocales()
        {
            if (dtPersonalMasivo == null || dgMasivo == null) return;
            string busqueda = txtBuscarMasivo.Text.Trim().ToUpper();
            string area = cmbFiltroArea.SelectedItem is ComboBoxItem item ? item.Content.ToString() : "TODAS LAS ÁREAS";
            string filtroQuery = "1 = 1";

            if (!string.IsNullOrEmpty(busqueda)) filtroQuery += $" AND (Nombre LIKE '%{busqueda}%' OR Matricula LIKE '%{busqueda}%')";
            if (area != "TODAS LAS ÁREAS") filtroQuery += $" AND Area = '{area}'";

            dtPersonalMasivo.DefaultView.RowFilter = filtroQuery;
            dgMasivo.ItemsSource = dtPersonalMasivo.DefaultView;
        }

        private void BtnSeleccionarTodos_Click(object sender, RoutedEventArgs e) { CambiarSeleccionVisible(true); }
        private void BtnDeseleccionarTodos_Click(object sender, RoutedEventArgs e) { CambiarSeleccionVisible(false); }

        private void CambiarSeleccionVisible(bool estado)
        {
            if (dgMasivo.ItemsSource is DataView vista)
                foreach (DataRowView fila in vista) fila["Seleccionado"] = estado;
        }

        private void CmbTipoAccion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelNovedad == null || panelEstatus == null) return;
            string tipoAccion = ((ComboBoxItem)cmbTipoAccion.SelectedItem).Tag.ToString();
            panelNovedad.Visibility = (tipoAccion == "NOVEDAD") ? Visibility.Visible : Visibility.Collapsed;
            panelEstatus.Visibility = (tipoAccion == "ESTATUS") ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmbMasivoNovedad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelFechas == null || panelDetalles == null || cmbMasivoNovedad.SelectedItem == null) return;
            string novedadSeleccionada = ((ComboBoxItem)cmbMasivoNovedad.SelectedItem).Tag.ToString();

            if (novedadSeleccionada == "PRESENTE")
            {
                panelFechas.Visibility = Visibility.Collapsed;
                panelDetalles.Visibility = Visibility.Collapsed;
                txtMasivoDetalle.Text = "";
            }
            else
            {
                panelFechas.Visibility = Visibility.Visible;
                panelDetalles.Visibility = Visibility.Visible;
            }
        }

        private void ChkMasivoIndefinido_Checked(object sender, RoutedEventArgs e) { if (panelFechaFin != null) panelFechaFin.Visibility = Visibility.Collapsed; }
        private void ChkMasivoIndefinido_Unchecked(object sender, RoutedEventArgs e) { if (panelFechaFin != null) panelFechaFin.Visibility = Visibility.Visible; }

       private void BtnAplicarMasivo_Click(object sender, RoutedEventArgs e)
        {
            DataView vista = (DataView)dgMasivo.ItemsSource;
            int actualizados = 0;
            string tipoAccion = ((ComboBoxItem)cmbTipoAccion.SelectedItem).Tag.ToString();

            bool haySeleccionados = false;
            foreach (DataRowView fila in vista)
                if (Convert.ToBoolean(fila["Seleccionado"]) == true) { haySeleccionados = true; break; }

            if (!haySeleccionados)
            {
                MessageBox.Show("Por favor, marca la casilla de al menos un elemento.", "Sin Selección", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"¿Aplicar este cambio a todos los elementos seleccionados?", "Confirmación Masiva", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    // 🚀 MEJORA DE RENDIMIENTO: Iniciamos una transacción maestra
                    using (SQLiteTransaction transaccion = conexion.BeginTransaction())
                    {
                        foreach (DataRowView fila in vista)
                        {
                            if (Convert.ToBoolean(fila["Seleccionado"]) == true)
                            {
                                string matricula = fila["Matricula"].ToString();

                                // 🛡️ MEJORA DE SEGURIDAD: Uso estricto de parámetros
                                using (SQLiteCommand cmd = new SQLiteCommand(conexion))
                                {
                                    if (tipoAccion == "ESTATUS")
                                    {
                                        string nuevoEstatus = ((ComboBoxItem)cmbMasivoEstatus.SelectedItem).Tag.ToString();
                                        cmd.CommandText = "UPDATE Personal_Naval SET Estatus = @estatus WHERE Matricula = @mat";
                                        cmd.Parameters.AddWithValue("@estatus", nuevoEstatus);
                                        cmd.Parameters.AddWithValue("@mat", matricula);
                                    }
                                    else if (tipoAccion == "NOVEDAD")
                                    {
                                        string nuevaNovedad = ((ComboBoxItem)cmbMasivoNovedad.SelectedItem).Tag.ToString();
                                        string detalle = txtMasivoDetalle.Text.Trim();
                                        
                                        if (nuevaNovedad != "PRESENTE")
                                        {
                                            cmd.CommandText = "UPDATE Personal_Naval SET Novedad = @novedad, FechaInicioNovedad = @fIni, FechaFinNovedad = @fFin, DetalleNovedad = @detalle WHERE Matricula = @mat";
                                            
                                            // Manejo inteligente de Fechas Nulas
                                            cmd.Parameters.AddWithValue("@fIni", dpMasivoInicio.SelectedDate.HasValue ? dpMasivoInicio.SelectedDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                                            
                                            // Si es indefinido o no hay fecha fin, mandamos NULL
                                            cmd.Parameters.AddWithValue("@fFin", (chkMasivoIndefinido.IsChecked == false && dpMasivoFin.SelectedDate.HasValue) ? dpMasivoFin.SelectedDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                                            
                                            cmd.Parameters.AddWithValue("@detalle", string.IsNullOrEmpty(detalle) ? DBNull.Value : detalle);
                                        }
                                        else
                                        {
                                            // Si regresa a presente, limpiamos el historial de esa novedad
                                            cmd.CommandText = "UPDATE Personal_Naval SET Novedad = @novedad, FechaInicioNovedad = NULL, FechaFinNovedad = NULL, DetalleNovedad = NULL WHERE Matricula = @mat";
                                        }

                                        cmd.Parameters.AddWithValue("@novedad", nuevaNovedad);
                                        cmd.Parameters.AddWithValue("@mat", matricula);
                                    }
                                    
                                    cmd.ExecuteNonQuery();
                                    actualizados++;
                                }
                            }
                        }

                        // 🚀 AQUÍ OCURRE LA MAGIA: Guardamos todos los cambios en el disco de un solo golpe
                        transaccion.Commit();
                    }
                }

                MessageBox.Show($"¡Operación exitosa!\n\nSe actualizó la situación de {actualizados} elementos en tiempo récord.", "Novedades Masivas", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarPersonalMasivo(); 
            }
            catch (Exception ex) 
            { 
                MessageBox.Show("Error SQL en actualización: " + ex.Message, "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }
    }
}