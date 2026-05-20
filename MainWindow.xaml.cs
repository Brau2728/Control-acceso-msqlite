using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DPFP; 
using DPFP.Verification; 

namespace prueba1 
{
    public partial class MainWindow : Window, DPFP.Capture.EventHandler
    {
        private DPFP.Capture.Capture Capturer;
        private Verification Verificator;

        // ==============================================================
        // 1. VARIABLES PARA EL CACHÉ EN RAM
        // ==============================================================
        private List<RegistroCache> _cacheBiometrico = new List<RegistroCache>();
        private bool _cacheListo = false;

        private class RegistroCache
        {
            public Marino DatosMarino { get; set; }
            public DPFP.Template Plantilla1 { get; set; }
            public DPFP.Template Plantilla2 { get; set; }
            public DPFP.Template Plantilla3 { get; set; }
        }

        private void Button_Click(object sender, RoutedEventArgs e) { }

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
        }

        #region Control del Lector
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitHuella();
            IniciarCaptura();
            
            // Disparamos la carga a la memoria RAM sin congelar la ventana al abrir
            await CargarCacheBiometricoAsync();
        }

        private void MainWindow_Closed(object sender, EventArgs e) { DetenerCaptura(); }

        private void InitHuella()
        {
            try
            {
                Capturer = new DPFP.Capture.Capture();
                Verificator = new Verification();
                if (Capturer != null) Capturer.EventHandler = this;
            }
            catch (Exception ex) { MessageBox.Show("Error al inicializar lector: " + ex.Message); }
        }

        private void IniciarCaptura() { if (Capturer != null) { try { Capturer.StartCapture(); } catch { } } }
        private void DetenerCaptura() { if (Capturer != null) { try { Capturer.StopCapture(); } catch { } } }
        #endregion

        #region Lógica de Verificación (Caché en RAM)
        // ==============================================================
        // CARGA PESADA A LA RAM (Se ejecuta solo 1 vez al abrir)
        // ==============================================================
        private async System.Threading.Tasks.Task CargarCacheBiometricoAsync()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                var cacheTemporal = new List<RegistroCache>();
                try
                {
                    using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                    {
                        string query = @"
                        SELECT p.Matricula, p.Nombres, p.Apellidos, p.IdGrado, p.IdJefatura, p.FotoPerfil, p.Huella, p.Huella2, p.Huella3, p.Estatus, p.Novedad, 
                            j.NombreJefatura, g.NombreGrado 
                        FROM Personal_Naval p
                        LEFT JOIN Cat_Jefaturas j ON p.IdJefatura = j.IdJefatura
                        LEFT JOIN Cat_Grados g ON p.IdGrado = g.IdGrado";

                        using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var itemCache = new RegistroCache();
                                
                                // 1. Guardamos los datos del Marino en RAM
                                itemCache.DatosMarino = new Marino
                                {
                                    Matricula = reader["Matricula"].ToString(),
                                    Nombre = reader["Nombres"].ToString(),
                                    Apellidos = reader["Apellidos"].ToString(),
                                    Grado = reader["NombreGrado"] != DBNull.Value ? reader["NombreGrado"].ToString() : "DESCONOCIDO",
                                    Jefatura = reader["NombreJefatura"] != DBNull.Value ? reader["NombreJefatura"].ToString() : "DESCONOCIDA",
                                    Estatus = reader["Estatus"].ToString(),
                                    Novedad = reader["Novedad"].ToString()
                                };

                                if (reader["FotoPerfil"] != DBNull.Value)
                                {
                                    byte[] fotoBytes = (byte[])reader["FotoPerfil"];
                                    itemCache.DatosMarino.FotoImagen = ConvertirBytesAImagenFueraDeUI(fotoBytes); 
                                }

                                // 2. Convertimos los Bytes a Plantillas Biométricas UNA SOLA VEZ
                                if (reader["Huella"] != DBNull.Value)
                                    using (MemoryStream ms = new MemoryStream((byte[])reader["Huella"])) { itemCache.Plantilla1 = new DPFP.Template(ms); }
                                
                                if (reader["Huella2"] != DBNull.Value)
                                    using (MemoryStream ms = new MemoryStream((byte[])reader["Huella2"])) { itemCache.Plantilla2 = new DPFP.Template(ms); }
                                
                                if (reader["Huella3"] != DBNull.Value)
                                    using (MemoryStream ms = new MemoryStream((byte[])reader["Huella3"])) { itemCache.Plantilla3 = new DPFP.Template(ms); }

                                cacheTemporal.Add(itemCache);
                            }
                        }
                    }
                    _cacheBiometrico = cacheTemporal;
                    _cacheListo = true; // Liberamos el candado para que ya pueda leer huellas
                }
                catch (Exception ex) {
                    // Si algo falla, avisamos a la pantalla principal en lugar de fallar en silencio
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        MessageBox.Show("Error crítico al cargar las huellas a la memoria RAM: " + ex.Message, "Fallo de Caché", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
              });
        }

        // Eventos nativos del lector de huellas DigitalPersona
        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample) { ProcesarHuella(Sample); }
        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) { }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) { }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) { }

        private void ProcesarHuella(Sample sample)
        {
            if (!_cacheListo) 
            {
                this.Dispatcher.Invoke(() => MessageBox.Show("Iniciando motor biométrico en RAM, por favor espere un segundo...", "Cargando", MessageBoxButton.OK, MessageBoxImage.Information));
                return;
            }

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
                this.Dispatcher.Invoke(() => { if (this.DataContext is MainViewModel vm) vm.MalaCaptura(); });
            }
        }

        // ==============================================================
        // COMPARACIÓN EXTREMA: Ya no convierte bytes, compara directo en RAM
        // ==============================================================
        private bool CompararHuellasRapido(DPFP.FeatureSet features, DPFP.Template templateGuardado)
        {
            if (templateGuardado == null) return false;
            try 
            {
                Verification.Result result = new Verification.Result();
                Verificator.Verify(features, templateGuardado, ref result);
                return result.Verified;
            }
            catch { return false; }
        }

        private void VerificarEnBaseDeDatos(DPFP.FeatureSet featuresCapturadas)
        {
            bool accesoConcedido = false;
            Marino marinoEncontrado = null;

            try
            {
                // BARRIDO DE MILISEGUNDOS EN RAM
                foreach (var item in _cacheBiometrico)
                {
                    bool match1 = CompararHuellasRapido(featuresCapturadas, item.Plantilla1);
                    bool match2 = CompararHuellasRapido(featuresCapturadas, item.Plantilla2);
                    bool match3 = CompararHuellasRapido(featuresCapturadas, item.Plantilla3);

                    if (match1 || match2 || match3)
                    {
                        accesoConcedido = true;
                        marinoEncontrado = item.DatosMarino;
                        break;
                    }
                }

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
            catch (Exception ex) { Console.WriteLine("Error al guardar historial: " + ex.Message); }
        }
        #endregion

        #region Funciones Auxiliares e Interfaz
       private BitmapImage ConvertirBytesAImagenFueraDeUI(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            
            BitmapImage image = null;
            
            // Forzamos TODO (creación, carga y congelamiento) al hilo principal de la UI
            Application.Current.Dispatcher.Invoke(() => 
            {
                image = new BitmapImage();
                using (var mem = new MemoryStream(imageData))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = mem;
                    image.EndInit();
                }
                image.Freeze(); // La congelamos aquí mismo
            });
            
            return image;
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
            DetenerCaptura();
            LoginWindow login = new LoginWindow();
            if (login.ShowDialog() == true)
            {
                PanelAdminWindow panelAdmin = new PanelAdminWindow(login.RolUsuario);
                panelAdmin.ShowDialog();
            }
            IniciarCaptura();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
        #endregion
    }
}