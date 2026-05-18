using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace prueba1
{
    public partial class DirectorioControl : UserControl
    {
        private string _rolUsuario;
        private string _matriculaNovedadActual = "";
        private string _matriculaIncidenciaActual = "";

        // Propiedad para el Binding de permisos (Botones Editar y Eliminar)
        public Visibility VisibilidadAdmin { get; set; } = Visibility.Visible;

        public DirectorioControl(string rolUsuario)
        {
            InitializeComponent();
            _rolUsuario = rolUsuario;
            
            // Evaluamos si es GUARDIA para ocultar botones sensibles
            if (_rolUsuario == "GUARDIA")
            {
                VisibilidadAdmin = Visibility.Collapsed;
            }

            // Hacemos que el propio UserControl sea su fuente de datos para poder leer VisibilidadAdmin en el XAML
            this.DataContext = this; 

            CargarDirectorio();
        }

        private void FiltrosDirectorio_Changed(object sender, RoutedEventArgs e)
        {
            if (dgPersonal != null) CargarDirectorio();
        }

        public void CargarDirectorio()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    // Agregamos FotoPerfil a la consulta
                    string query = "SELECT Matricula, IdGrado, Nombres AS Nombre, Apellidos, IdJefatura, Novedad, Estatus, FotoPerfil FROM Personal_Naval WHERE 1=1 ";

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
                        dt.Columns.Add("Foto", typeof(BitmapImage)); // Columna especial para la foto

                        // Imagen por defecto genérica en caso de que no tengan foto
                        BitmapImage fotoPorDefecto = new BitmapImage(new Uri("https://cdn-icons-png.flaticon.com/512/3135/3135715.png"));

                        while (reader.Read())
                        {
                            DataRow row = dt.NewRow();
                            row["Matricula"] = reader["Matricula"].ToString();
                            row["Grado"] = ObtenerNombreGrado(Convert.ToInt32(reader["IdGrado"]));
                            row["Nombre"] = reader["Nombre"].ToString();
                            row["Apellidos"] = reader["Apellidos"].ToString();
                            row["Estatus"] = reader["Estatus"].ToString();
                            row["Novedad"] = reader["Novedad"].ToString();

                            // Procesamiento de la Fotografía
                            if (reader["FotoPerfil"] != DBNull.Value)
                            {
                                byte[] fotoBytes = (byte[])reader["FotoPerfil"];
                                row["Foto"] = ConvertirBytesAImagen(fotoBytes);
                            }
                            else
                            {
                                row["Foto"] = fotoPorDefecto;
                            }

                            dt.Rows.Add(row);
                        }
                        dgPersonal.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar directorio: " + ex.Message); }
        }

        // =========================================================
        // --- EVENTOS DE LOS BOTONES DE LA TABLA (CRUD) ---
        // =========================================================

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

        private void BtnEditarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                // Abre la ventana que ya existe en tu proyecto (RegistroPersonal) en modo edición
                RegistroPersonal ventanaEdicion = new RegistroPersonal(boton.CommandParameter.ToString());
                ventanaEdicion.ShowDialog();
                CargarDirectorio(); // Refresca la tabla al cerrar
            }
        }

        private void BtnEliminarRegistro_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                string mat = boton.CommandParameter.ToString();
                if (MessageBox.Show($"¿Eliminar permanentemente los datos biométricos y el expediente de {mat}?", "Confirmación Crítica", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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

        // =========================================================
        // --- CONTROL DEL MODAL DE NOVEDADES ---
        // =========================================================

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
            catch (Exception ex) { MessageBox.Show("Error al actualizar la novedad: " + ex.Message); }
        }

        private void BtnCancelarNovedad_Click(object sender, RoutedEventArgs e) { PanelNovedades.Visibility = Visibility.Collapsed; }

        // =========================================================
        // --- CONTROL DEL MODAL DE INCIDENCIAS/NOTAS MANUALES ---
        // =========================================================

        private void BtnIncidencia_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                _matriculaIncidenciaActual = boton.CommandParameter.ToString();
                txtElementoIncidencia.Text = $"Matrícula: {_matriculaIncidenciaActual}";
                txtNotaIncidencia.Text = "";
                PanelIncidencia.Visibility = Visibility.Visible;
            }
        }

        private void BtnGuardarIncidencia_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNotaIncidencia.Text))
            {
                MessageBox.Show("Por favor, escriba el motivo de la incidencia.", "Nota vacía", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "INSERT INTO Registro_Accesos (Matricula, FechaHora, MensajeAcceso, NovedadMomento) VALUES (@mat, @fecha, @mensaje, 'INCIDENCIA MANUAL')";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@mat", _matriculaIncidenciaActual);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@mensaje", "NOTA: " + txtNotaIncidencia.Text.Trim());
                    cmd.ExecuteNonQuery();
                }
                MessageBox.Show($"La incidencia fue anexada al expediente de {_matriculaIncidenciaActual} correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                PanelIncidencia.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) { MessageBox.Show("Error al guardar la nota: " + ex.Message); }
        }

        private void BtnCancelarIncidencia_Click(object sender, RoutedEventArgs e) { PanelIncidencia.Visibility = Visibility.Collapsed; }

        // =========================================================
        // --- FUNCIONES AUXILIARES ---
        // =========================================================

        private string ObtenerNombreGrado(int id)
        {
            string[] grados = { "Otro", "Marinero", "Cabo", "Tercer Maestre", "Segundo Maestre", "Primer Maestre", "TTE. Corbeta", "TTE. Fragata", "TTE. Navío", "CAP. Corbeta", "CAP. Fragata", "CAP. Navío", "Contralmirante", "Vicealmirante", "Almirante" };
            if (id >= 1 && id <= grados.Length) return grados[id - 1];
            return "DESCONOCIDO";
        }

        private BitmapImage ConvertirBytesAImagen(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            // Freeze() es CRUCIAL para que WPF pueda usar la imagen en otros hilos de la UI (Como el DataGrid)
            image.Freeze(); 
            return image;
        }
    }
}