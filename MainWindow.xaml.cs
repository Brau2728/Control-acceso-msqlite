using System;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using prueva1;
using DPFP; // Lector
using DPFP.Verification; // Verificador

namespace prueba1
{
    public partial class MainWindow : Window, DPFP.Capture.EventHandler
    {
        private DPFP.Capture.Capture Capturer;
        private Verification Verificator;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
        }

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

        // --- EVENTO CUANDO ALGUIEN PONE EL DEDO ---
        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            ProcesarHuella(Sample);
        }

        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) { }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) { }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) { }

        // --- PROCESAR Y VERIFICAR ---
       private void ProcesarHuella(Sample sample)
        {
            // Extraer características de la huella capturada para VERIFICACIÓN
            DPFP.Processing.FeatureExtraction extractor = new DPFP.Processing.FeatureExtraction();
            DPFP.Capture.CaptureFeedback feedback = DPFP.Capture.CaptureFeedback.None;
            DPFP.FeatureSet features = new DPFP.FeatureSet();
            
            // Usamos DataPurpose.Verification para comparar
            extractor.CreateFeatureSet(sample, DPFP.Processing.DataPurpose.Verification, ref feedback, ref features);

            if (feedback == DPFP.Capture.CaptureFeedback.Good)
            {
                // Si la lectura física es buena, busca en SQLite
                VerificarEnBaseDeDatos(features);
            }
            else
            {
                // Si la lectura física falló (movió el dedo, está sucio, etc.)
                this.Dispatcher.Invoke(() =>
                {
                    if (this.DataContext is MainViewModel vm)
                    {
                        vm.MalaCaptura();
                    }
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
                    // Traemos todos los registros para compararlos uno por uno
                    string query = "SELECT Matricula, Nombres, Apellidos, IdGrado, IdJefatura, FotoPerfil, Huella FROM Personal_Naval";
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

                                // REALIZAR LA COMPARACIÓN
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
                                        Jefatura = ObtenerNombreJefatura(Convert.ToInt32(reader["IdJefatura"]))
                                    };

                                    if (reader["FotoPerfil"] != DBNull.Value)
                                    {
                                        byte[] fotoBytes = (byte[])reader["FotoPerfil"];
                                        marinoEncontrado.FotoImagen = ConvertirBytesAImagen(fotoBytes);
                                    }

                                    break; // Huella encontrada, salir del ciclo
                                }
                            }
                        }
                    }
                }

                // Mandar el resultado a la pantalla
                this.Dispatcher.Invoke(() =>
                {
                    if (this.DataContext is MainViewModel vm)
                    {
                        if (accesoConcedido && marinoEncontrado != null)
                            vm.AccesoAutorizado(marinoEncontrado);
                        else
                            vm.AccesoDenegado();
                    }
                });
            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(() => MessageBox.Show("Error al verificar: " + ex.Message));
            }
        }

        // --- FUNCIONES DE AYUDA ---
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

        private void Button_Click(object sender, RoutedEventArgs e) { } // Botón manual vacío

    private void OpenLogin_Click(object sender, RoutedEventArgs e)
        {
            // 1. PAUSAMOS EL LECTOR PARA "PRESTARLO" A OTRAS VENTANAS
            DetenerCaptura();

            LoginWindow login = new LoginWindow();
            if (login.ShowDialog() == true) // ShowDialog detiene el código aquí hasta que se cierre la ventana
            {
                if (login.RolUsuario == "ADMIN")
                {
                    PanelAdminWindow panelAdmin = new PanelAdminWindow();
                    panelAdmin.ShowDialog(); // Lo mismo, espera a que el admin cierre su panel
                }
                else
                {
                    MessageBox.Show("Sesión iniciada como Guardia.", "Acceso Autorizado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            // 2. CUANDO EL ADMIN O GUARDIA CIERRAN SUS PANELES Y REGRESAN AQUÍ, REANUDAMOS EL LECTOR
            IniciarCaptura();
        }

        private void Test_Click(object sender, RoutedEventArgs e) { }
        private void CloseWindow_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
    }
}