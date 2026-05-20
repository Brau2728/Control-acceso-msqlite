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

      private async void BtnAplicarMasivo_Click(object sender, RoutedEventArgs e)
        {
            // ====================================================================
            // FASE 1: LECTURA EN EL HILO DE LA INTERFAZ (UI)
            // Extraemos todo lo visual a variables nativas de C# antes de ir al fondo.
            // ====================================================================
            DataView vista = (DataView)dgMasivo.ItemsSource;
            List<string> matriculasSeleccionadas = new List<string>();

            // 1.1 Obtener quiénes están palomeados
            foreach (DataRowView fila in vista)
            {
                if (Convert.ToBoolean(fila["Seleccionado"]) == true)
                {
                    matriculasSeleccionadas.Add(fila["Matricula"].ToString());
                }
            }

            if (matriculasSeleccionadas.Count == 0)
            {
                MessageBox.Show("Por favor, marca la casilla de al menos un elemento.", "Sin Selección", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"¿Aplicar este cambio a {matriculasSeleccionadas.Count} elementos seleccionados?", "Confirmación Masiva", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            // 1.2 Extraer las configuraciones de los ComboBox, TextBoxes y DatePickers
            string tipoAccion = ((ComboBoxItem)cmbTipoAccion.SelectedItem).Tag.ToString();
            
            // Variables para almacenar lo que el usuario eligió
            string nuevoEstatus = null;
            string nuevaNovedad = null;
            string detalleNovedad = null;
            string fIni = null;
            string fFin = null;

            if (tipoAccion == "ESTATUS")
            {
                nuevoEstatus = ((ComboBoxItem)cmbMasivoEstatus.SelectedItem).Tag.ToString();
            }
            else if (tipoAccion == "NOVEDAD")
            {
                nuevaNovedad = ((ComboBoxItem)cmbMasivoNovedad.SelectedItem).Tag.ToString();
                detalleNovedad = txtMasivoDetalle.Text.Trim();
                
                fIni = dpMasivoInicio.SelectedDate.HasValue ? dpMasivoInicio.SelectedDate.Value.ToString("yyyy-MM-dd") : null;
                
                bool esIndefinido = chkMasivoIndefinido.IsChecked == true;
                fFin = (!esIndefinido && dpMasivoFin.SelectedDate.HasValue) ? dpMasivoFin.SelectedDate.Value.ToString("yyyy-MM-dd") : null;
            }

            // ====================================================================
            // FASE 2: TRABAJO PESADO EN SEGUNDO PLANO
            // ====================================================================
            int actualizados = 0; // Llevamos la cuenta aquí

            // Deshabilitamos el botón temporalmente para evitar doble clic accidental
            Button btnOrigen = sender as Button;
            if (btnOrigen != null) btnOrigen.IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                    {
                        // Seguimos usando la Transacción Maestra, es excelente para la velocidad
                        using (SQLiteTransaction transaccion = conexion.BeginTransaction())
                        {
                            // Recorremos nuestra lista segura de Strings, NO el DataGrid
                            foreach (string matricula in matriculasSeleccionadas)
                            {
                                using (SQLiteCommand cmd = new SQLiteCommand(conexion))
                                {
                                    if (tipoAccion == "ESTATUS")
                                    {
                                        cmd.CommandText = "UPDATE Personal_Naval SET Estatus = @estatus WHERE Matricula = @mat";
                                        cmd.Parameters.AddWithValue("@estatus", nuevoEstatus);
                                        cmd.Parameters.AddWithValue("@mat", matricula);
                                    }
                                    else if (tipoAccion == "NOVEDAD")
                                    {
                                        if (nuevaNovedad != "PRESENTE")
                                        {
                                            cmd.CommandText = "UPDATE Personal_Naval SET Novedad = @novedad, FechaInicioNovedad = @fIni, FechaFinNovedad = @fFin, DetalleNovedad = @detalle WHERE Matricula = @mat";
                                            
                                            // Manejo de nulos en SQLite parametrizado
                                            cmd.Parameters.AddWithValue("@fIni", fIni != null ? (object)fIni : DBNull.Value);
                                            cmd.Parameters.AddWithValue("@fFin", fFin != null ? (object)fFin : DBNull.Value);
                                            cmd.Parameters.AddWithValue("@detalle", string.IsNullOrEmpty(detalleNovedad) ? DBNull.Value : detalleNovedad);
                                        }
                                        else
                                        {
                                            cmd.CommandText = "UPDATE Personal_Naval SET Novedad = @novedad, FechaInicioNovedad = NULL, FechaFinNovedad = NULL, DetalleNovedad = NULL WHERE Matricula = @mat";
                                        }

                                        cmd.Parameters.AddWithValue("@novedad", nuevaNovedad);
                                        cmd.Parameters.AddWithValue("@mat", matricula);
                                    }
                                    
                                    cmd.ExecuteNonQuery();
                                    actualizados++;
                                }
                            }

                            // Guardamos todo de golpe
                            transaccion.Commit();
                        }
                    }
                });

                // ====================================================================
                // FASE 3: RETORNO AL HILO DE LA INTERFAZ
                // ====================================================================
                MessageBox.Show($"¡Operación exitosa!\n\nSe actualizó la situación de {actualizados} elementos en tiempo récord y sin congelar el sistema.", "Novedades Masivas", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarPersonalMasivo(); 
            }
            catch (Exception ex) 
            { 
                MessageBox.Show("Error SQL en actualización: " + ex.Message, "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
            finally
            {
                // Siempre reactivamos el botón pase lo que pase
                if (btnOrigen != null) btnOrigen.IsEnabled = true;
            }
        }
      
    }
}