using Microsoft.Win32;
using System;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Drawing; // Necesario para procesar la imagen de la huella
using prueva1;
using DPFP; // Librería base del lector
using DPFP.Capture; // Para la captura
using DPFP.Processing; // Para extraer características y crear el template

namespace prueba1 
{
    public partial class RegistroPersonal : Window, DPFP.Capture.EventHandler
    {
        private DPFP.Template templateHuella = null; 

        // Variables nativas para el lector
        private DPFP.Capture.Capture Capturer;
        private DPFP.Processing.Enrollment Enroller;

        // Variables para el MODO EDICIÓN
        private bool _esEdicion = false;
        private string _matriculaOriginal = "";
        
        // EL DETECTOR DE CAMBIOS DE FOTO (La solución al problema)
        private bool _fotoModificada = false; 

        // 1. CONSTRUCTOR NORMAL (Nuevo Registro)
        public RegistroPersonal()
        {
            InitializeComponent();
            this.Loaded += RegistroPersonal_Loaded;
            this.Closed += RegistroPersonal_Closed;
        }

        // 2. CONSTRUCTOR SOBRECARGADO (Modo Edición)
        public RegistroPersonal(string matriculaEditar) : this() // Llama al constructor normal primero
        {
            _esEdicion = true;
            _matriculaOriginal = matriculaEditar;
            
            this.Title = "Editar Personal Naval - " + matriculaEditar;
            txtMatricula.IsEnabled = false; // Bloqueamos la matrícula para evitar errores de BD
            
            CargarDatosEdicion(matriculaEditar);
        }

        #region Carga de Datos (Solo Edición)
        private void CargarDatosEdicion(string matricula)
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT * FROM Personal_Naval WHERE Matricula = @mat";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@mat", matricula);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtMatricula.Text = reader["Matricula"].ToString();
                            txtNombres.Text = reader["Nombres"].ToString();
                            txtApellidos.Text = reader["Apellidos"].ToString();

                            int idGrado = Convert.ToInt32(reader["IdGrado"]);
                            int idJefa = Convert.ToInt32(reader["IdJefatura"]);

                            cmbGrado.SelectedIndex = idGrado - 1;
                            cmbJefatura.SelectedIndex = idJefa - 1;

                            if (reader["FotoPerfil"] != DBNull.Value)
                            {
                                byte[] fotoBytes = (byte[])reader["FotoPerfil"];
                                BitmapImage bi = new BitmapImage();
                                using (MemoryStream ms = new MemoryStream(fotoBytes))
                                {
                                    ms.Position = 0;
                                    bi.BeginInit();
                                    bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                    bi.CacheOption = BitmapCacheOption.OnLoad;
                                    bi.UriSource = null;
                                    bi.StreamSource = ms;
                                    bi.EndInit();
                                }
                                bi.Freeze();
                                imgFoto.Source = bi;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar los datos: " + ex.Message);
            }
        }
        #endregion

        #region Ciclo de vida del Lector de Huella
        private void RegistroPersonal_Loaded(object sender, RoutedEventArgs e)
        {
            InitHuella();
            IniciarCaptura();
            
            if (_esEdicion)
            {
                ActualizarEstadoUI("Modo Edición: Escanee 4 veces solo si desea CAMBIAR la huella.", System.Windows.Media.Brushes.Purple);
            }
        }

        private void RegistroPersonal_Closed(object sender, EventArgs e) { DetenerCaptura(); }

        private void InitHuella()
        {
            try
            {
                Capturer = new DPFP.Capture.Capture();       
                Enroller = new DPFP.Processing.Enrollment(); 
                if (Capturer != null) Capturer.EventHandler = this; 
            }
            catch (Exception ex) { }
        }

        private void IniciarCaptura() { if (Capturer != null) { try { Capturer.StartCapture(); } catch { } } }
        private void DetenerCaptura() { if (Capturer != null) { try { Capturer.StopCapture(); } catch { } } }
        #endregion

        #region Eventos del Lector DPFP 
        public void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample) { ProcesarHuella(Sample); }
        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) 
        {
            if (!_esEdicion) ActualizarEstadoUI("Lector conectado. Coloque su dedo.", System.Windows.Media.Brushes.Blue);
        }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) 
        {
            ActualizarEstadoUI("Lector desconectado.", System.Windows.Media.Brushes.Red);
        }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) { }
        #endregion

        #region Procesamiento y Visualización de la Huella
        private void ProcesarHuella(DPFP.Sample Sample)
        {
            MostrarImagenHuella(Sample);
            DPFP.FeatureSet features = ExtraerCaracteristicas(Sample, DPFP.Processing.DataPurpose.Enrollment);

            if (features != null)
            {
                try { Enroller.AddFeatures(features); }
                finally
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        switch (Enroller.TemplateStatus)
                        {
                            case DPFP.Processing.Enrollment.Status.Ready:
                                templateHuella = Enroller.Template;
                                ActualizarEstadoUI("Nueva huella registrada exitosamente ✔️", System.Windows.Media.Brushes.Green);
                                DetenerCaptura(); 
                                break;
                            case DPFP.Processing.Enrollment.Status.Failed:
                                Enroller.Clear();
                                DetenerCaptura();
                                ActualizarEstadoUI("Fallo al registrar huella. Intente de nuevo ❌", System.Windows.Media.Brushes.Red);
                                IniciarCaptura();
                                break;
                            case DPFP.Processing.Enrollment.Status.Insufficient:
                                ActualizarEstadoUI($"Faltan {Enroller.FeaturesNeeded} lecturas. Vuelva a colocar el dedo.", System.Windows.Media.Brushes.DarkOrange);
                                break;
                        }
                    });
                }
            }
        }

        private void MostrarImagenHuella(DPFP.Sample sample)
        {
            try
            {
                DPFP.Capture.SampleConversion convertor = new DPFP.Capture.SampleConversion();
                Bitmap bitmap = null;
                convertor.ConvertToPicture(sample, ref bitmap);

                if (bitmap != null)
                {
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                        ms.Position = 0;
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze(); 

                        this.Dispatcher.Invoke(() =>
                        {
                            if (this.FindName("imgHuella") is System.Windows.Controls.Image cuadroHuella)
                            {
                                cuadroHuella.Source = bi;
                            }
                        });
                    }
                }
            }
            catch { }
        }

        private DPFP.FeatureSet ExtraerCaracteristicas(DPFP.Sample Sample, DPFP.Processing.DataPurpose Purpose)
        {
            DPFP.Processing.FeatureExtraction Extractor = new DPFP.Processing.FeatureExtraction();
            DPFP.Capture.CaptureFeedback feedback = DPFP.Capture.CaptureFeedback.None;
            DPFP.FeatureSet features = new DPFP.FeatureSet();
            Extractor.CreateFeatureSet(Sample, Purpose, ref feedback, ref features);
            if (feedback == DPFP.Capture.CaptureFeedback.Good) return features;
            else return null;
        }

        private void ActualizarEstadoUI(string mensaje, System.Windows.Media.Brush color)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (this.FindName("txtEstadoHuella") is TextBlock txt)
                {
                    txt.Text = mensaje;
                    txt.Foreground = color;
                }
            });
        }
        #endregion

        #region Lógica de Registro / Actualización (Botones y Foto)
        
        // SI EL USUARIO TOCA CUALQUIERA DE ESTOS CONTROLES, ACTIVAMOS LA BANDERA DE _fotoModificada
        private void BtnFoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Seleccionar imagen";
            op.Filter = "Archivos de imagen|*.jpg;*.jpeg;*.png";
            if (op.ShowDialog() == true) 
            {
                imgFoto.Source = new BitmapImage(new Uri(op.FileName));
                _fotoModificada = true; // <--- SE ACTIVÓ EL DETECTOR
            }
        }
        
        private void BtnRotarIzq_Click(object sender, RoutedEventArgs e) { rtRotacion.Angle -= 90; _fotoModificada = true; }
        private void BtnRotarDer_Click(object sender, RoutedEventArgs e) { rtRotacion.Angle += 90; _fotoModificada = true; }
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e) { stEscala.ScaleX += 0.1; stEscala.ScaleY += 0.1; _fotoModificada = true; }
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) { if (stEscala.ScaleX > 0.2) { stEscala.ScaleX -= 0.1; stEscala.ScaleY -= 0.1; _fotoModificada = true; } }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMatricula.Text) || string.IsNullOrWhiteSpace(txtNombres.Text) ||
                cmbGrado.SelectedItem == null || cmbJefatura.SelectedItem == null) 
            {
                MessageBox.Show("Faltan datos obligatorios.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_esEdicion && templateHuella == null)
            {
                MessageBox.Show("No se ha terminado de registrar la huella (se requieren 4 lecturas).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "";
                    
                    if (_esEdicion)
                    {
                        // MODO EDICIÓN: Solo actualiza la foto SI EL DETECTOR SE ACTIVÓ
                        query = @"UPDATE Personal_Naval 
                                  SET Nombres = @nom, Apellidos = @ape, IdGrado = @idGrado, IdJefatura = @idJefa";
                                  
                        if (_fotoModificada) query += ", FotoPerfil = @foto";
                        if (templateHuella != null) query += ", Huella = @huella";
                        
                        query += " WHERE Matricula = @matOriginal";
                    }
                    else
                    {
                        query = @"INSERT INTO Personal_Naval 
                                 (Matricula, Nombres, Apellidos, IdGrado, IdJefatura, FotoPerfil, Huella) 
                                 VALUES (@mat, @nom, @ape, @idGrado, @idJefa, @foto, @huella)";
                    }

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    
                    cmd.Parameters.AddWithValue("@mat", txtMatricula.Text);
                    cmd.Parameters.AddWithValue("@nom", txtNombres.Text);
                    cmd.Parameters.AddWithValue("@ape", txtApellidos.Text);
                    if (_esEdicion) cmd.Parameters.AddWithValue("@matOriginal", _matriculaOriginal);
                    
                    int idGrado = int.Parse(((ComboBoxItem)cmbGrado.SelectedItem).Tag.ToString());
                    int idJefa = int.Parse(((ComboBoxItem)cmbJefatura.SelectedItem).Tag.ToString());
                    cmd.Parameters.AddWithValue("@idGrado", idGrado);
                    cmd.Parameters.AddWithValue("@idJefa", idJefa);

                    // SOLO TOMAMOS LA CAPTURA SI ES UN REGISTRO NUEVO O SI MODIFICARON LA FOTO EN EDICIÓN
                    // SOLO TOMAMOS LA CAPTURA SI ES UN REGISTRO NUEVO O SI MODIFICARON LA FOTO EN EDICIÓN
                    if (!_esEdicion || _fotoModificada)
                    {
                        if (imgFoto.Source != null && this.FindName("bdrFoto") is Border bordeFoto)
                        {
                            // MULTIPLICADOR DE CALIDAD: x4 (Convertimos 160x190 a 640x760 HD)
                            int escala = 4; 
                            int w = (int)bordeFoto.ActualWidth * escala;
                            int h = (int)bordeFoto.ActualHeight * escala;

                            System.Windows.Media.VisualBrush pincelVisual = new System.Windows.Media.VisualBrush(bordeFoto);
                            System.Windows.Media.DrawingVisual dibujoVisual = new System.Windows.Media.DrawingVisual();
                            
                            using (System.Windows.Media.DrawingContext contexto = dibujoVisual.RenderOpen())
                            {
                                // Le decimos a WPF que agrande el pincel antes de pintar para no perder calidad
                                contexto.PushTransform(new System.Windows.Media.ScaleTransform(escala, escala));
                                contexto.DrawRectangle(pincelVisual, null, new Rect(0, 0, bordeFoto.ActualWidth, bordeFoto.ActualHeight));
                            }

                            RenderTargetBitmap renderTarget = new RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            renderTarget.Render(dibujoVisual);

                            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                            // Usamos una calidad alta para el JPEG (95%)
                            encoder.QualityLevel = 95; 
                            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                            
                            using (MemoryStream ms = new MemoryStream())
                            {
                                encoder.Save(ms);
                                cmd.Parameters.AddWithValue("@foto", ms.ToArray()); 
                            }
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@foto", DBNull.Value);
                        }
                    }

                    if (templateHuella != null) cmd.Parameters.AddWithValue("@huella", templateHuella.Bytes);

                    int filas = cmd.ExecuteNonQuery();

                    if (filas > 0)
                    {
                        string msgExito = _esEdicion ? "Datos actualizados correctamente." : "Personal registrado correctamente.";
                        MessageBox.Show(msgExito, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close(); 
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar en BD: " + ex.Message);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) { this.Close(); }
        #endregion
    }
}