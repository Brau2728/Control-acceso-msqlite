using System;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DPFP; // Lector
using DPFP.Verification; // Verificador

namespace prueba1 // Asegúrate de que diga prueba1 o prueva1 según tu proyecto
{
    public partial class MainWindow : Window, DPFP.Capture.EventHandler
    {
        private DPFP.Capture.Capture Capturer;
        private Verification Verificator;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Este botón actualmente está oculto (Visibility="Collapsed")
            // Aquí puedes poner código para simular una huella en el futuro si tu lector falla en las pruebas.
        }

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
        }

        #region Control del Lector
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitHuella();
            IniciarCaptura();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            DetenerCaptura();
        }

        private void InitHuella()
        {
            try
            {
                Capturer = new DPFP.Capture.Capture();
                Verificator = new Verification();

                if (Capturer != null)
                    Capturer.EventHandler = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al inicializar lector: " + ex.Message);
            }
        }

        private void IniciarCaptura() { if (Capturer != null) { try { Capturer.StartCapture(); } catch { } } }
        private void DetenerCaptura() { if (Capturer != null) { try { Capturer.StopCapture(); } catch { } } }
        #endregion

        #region Eventos del Lector
        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            ProcesarHuella(Sample);
        }

        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) { }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) { }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) { }
        #endregion

        #region Lógica de Verificación e Historial
        private void ProcesarHuella(Sample sample)
        {
            DPFP.Processing.FeatureExtraction extractor = new DPFP.Processing.FeatureExtraction();
            DPFP.Capture.CaptureFeedback feedback = DPFP.Capture.CaptureFeedback.None;
            DPFP.FeatureSet features = new DPFP.FeatureSet();

            extractor.CreateFeatureSet(sample, DPFP.Processing.DataPurpose.Verification, ref feedback, ref features);

            if (feedback == DPFP.Capture.CaptureFeedback.Good)
            {
                VerificarEnBaseDeDatos(features);
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (this.DataContext is MainViewModel vm) vm.MalaCaptura();
                });
            }
        }

        private void VerificarEnBaseDeDatos(DPFP.FeatureSet featuresCapturadas)
        {
            bool accesoConcedido = false;
            Marino marinoEncontrado = null;

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT Matricula, Nombres, Apellidos, IdGrado, IdJefatura, FotoPerfil, Huella, Estatus, Novedad FROM Personal_Naval";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["Huella"] != DBNull.Value)
                            {
                                byte[] huellaBD = (byte[])reader["Huella"];

                                DPFP.Template templateGuardado = new DPFP.Template();
                                using (MemoryStream stream = new MemoryStream(huellaBD))
                                {
                                    templateGuardado = new DPFP.Template(stream);
                                }

                                Verification.Result result = new Verification.Result();
                                Verificator.Verify(featuresCapturadas, templateGuardado, ref result);

                                if (result.Verified)
                                {
                                    accesoConcedido = true;
                                    marinoEncontrado = new Marino
                                    {
                                        Matricula = reader["Matricula"].ToString(),
                                        Nombre = reader["Nombres"].ToString(),
                                        Apellidos = reader["Apellidos"].ToString(),
                                        Grado = ObtenerNombreGrado(Convert.ToInt32(reader["IdGrado"])),
                                        Jefatura = ObtenerNombreJefatura(Convert.ToInt32(reader["IdJefatura"])),
                                        Estatus = reader["Estatus"].ToString(),
                                        Novedad = reader["Novedad"].ToString()
                                    };

                                    if (reader["FotoPerfil"] != DBNull.Value)
                                    {
                                        byte[] fotoBytes = (byte[])reader["FotoPerfil"];
                                        marinoEncontrado.FotoImagen = ConvertirBytesAImagen(fotoBytes);
                                    }
                                    else
                                    {
                                        marinoEncontrado.FotoImagen = new BitmapImage(new Uri("https://cdn-icons-png.flaticon.com/512/3135/3135715.png"));
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }

                // --- LÓGICA DE DECISIÓN DE COLORES Y REGISTRO DE HISTORIAL ---
                this.Dispatcher.Invoke(() =>
                {
                    if (this.DataContext is MainViewModel vm)
                    {
                        if (accesoConcedido && marinoEncontrado != null)
                        {
                            if (marinoEncontrado.Estatus == "BAJA")
                            {
                                vm.AccesoDenegadoBaja(marinoEncontrado);
                                GuardarHistorialAcceso(marinoEncontrado.Matricula, "DENEGADO (BAJA)", marinoEncontrado.Novedad);
                            }
                            else if (marinoEncontrado.Novedad != "PRESENTE")
                            {
                                vm.AccesoConNovedad(marinoEncontrado);
                                GuardarHistorialAcceso(marinoEncontrado.Matricula, "ACCESO CON NOVEDAD", marinoEncontrado.Novedad);
                            }
                            else
                            {
                                vm.AccesoAutorizado(marinoEncontrado);
                                GuardarHistorialAcceso(marinoEncontrado.Matricula, "ACCESO NORMAL", "PRESENTE");
                            }
                        }
                        else
                        {
                            vm.AccesoDenegado();
                            // Registramos intentos de huellas no reconocidas
                            GuardarHistorialAcceso("DESCONOCIDA", "INTENTO RECHAZADO", "-");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(() => MessageBox.Show("Error al verificar: " + ex.Message));
            }
        }

        // --- NUEVA FUNCIÓN PARA GUARDAR EL HISTORIAL ---
        private void GuardarHistorialAcceso(string matricula, string mensajeAcceso, string novedad)
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "INSERT INTO Registro_Accesos (Matricula, FechaHora, MensajeAcceso, NovedadMomento) VALUES (@mat, @fecha, @mensaje, @novedad)";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@mat", matricula);
                        cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@mensaje", mensajeAcceso);
                        cmd.Parameters.AddWithValue("@novedad", novedad);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al guardar historial: " + ex.Message);
            }
        }
        #endregion

        #region Funciones Auxiliares e Interfaz
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
            image.Freeze();
            return image;
        }

       

            private void OpenLogin_Click(object sender, RoutedEventArgs e)
        {
            // Pausamos el sensor para prestárselo a otras ventanas
            DetenerCaptura();

            LoginWindow login = new LoginWindow();
            if (login.ShowDialog() == true)
            {
                // En lugar de un MessageBox, abrimos el panel para AMBOS roles, 
                // pero le pasamos el rol (ADMIN o GUARDIA) como parámetro
                PanelAdminWindow panelAdmin = new PanelAdminWindow(login.RolUsuario);
                panelAdmin.ShowDialog();
            }

            // Al cerrar las ventanas, reactivamos el sensor
            IniciarCaptura();
        }
        

        private void Test_Click(object sender, RoutedEventArgs e) { }
        private void CloseWindow_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
        #endregion
    }
}