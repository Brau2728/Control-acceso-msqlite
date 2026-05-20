using Microsoft.Win32;
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Drawing; 
using DPFP; 
using DPFP.Capture; 
using DPFP.Processing; 

namespace prueba1 
{
    public partial class RegistroPersonal : Window, DPFP.Capture.EventHandler
    {
        // 💡 SOLUCIÓN: Guardamos los Bytes en crudo inmediatamente para evitar que el lector los sobreescriba
        private byte[] _huella1Bytes = null; 
        private byte[] _huella2Bytes = null; 
        private byte[] _huella3Bytes = null; 
        
        private int _dedoActual = 1; 
        private bool _huellasModificadas = false;

        private DPFP.Capture.Capture Capturer;
        private DPFP.Processing.Enrollment Enroller;

        private bool _esEdicion = false;
        private string _matriculaOriginal = "";
        private bool _fotoModificada = false; 

        public RegistroPersonal()
        {
            InitializeComponent();
            this.Loaded += RegistroPersonal_Loaded;
            this.Closed += RegistroPersonal_Closed;
        }

        public RegistroPersonal(string matriculaEditar) : this() 
        {
            _esEdicion = true;
            _matriculaOriginal = matriculaEditar;
            
            this.Title = "Editar Personal Naval - " + matriculaEditar;
            txtMatricula.IsEnabled = false; 
            
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

                           cmbGrado.SelectedValue = Convert.ToInt32(reader["IdGrado"]);
                           cmbJefatura.SelectedValue = Convert.ToInt32(reader["IdJefatura"]);

                            btnGuardarRegistro.IsEnabled = true;

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
            catch (Exception ex) { MessageBox.Show("Error al cargar los datos: " + ex.Message); }
        }
        #endregion

        #region Ciclo de vida del Lector de Huella
        private void RegistroPersonal_Loaded(object sender, RoutedEventArgs e)
        {
            CargarGradosCombo();
            CargarJefaturasCombo();
            InitHuella();
            IniciarCaptura();
            
            if (_esEdicion)
            {
                ActualizarEstadoUI("Modo Edición: Coloque un dedo SOLAMENTE si desea REEMPLAZAR las 3 huellas actuales.", System.Windows.Media.Brushes.Purple);
            }
            else
            {
                ActualizarEstadoUI("PASO 1/3: Coloque su ÍNDICE DERECHO en el lector 4 veces.", System.Windows.Media.Brushes.Blue);
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
            catch { }
        }

        private void IniciarCaptura() { if (Capturer != null) { try { Capturer.StartCapture(); } catch { } } }
        private void DetenerCaptura() { if (Capturer != null) { try { Capturer.StopCapture(); } catch { } } }
        #endregion

        #region Eventos del Lector DPFP 
        public void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample) { ProcesarHuella(Sample); }
        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) { }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) 
        {
            ActualizarEstadoUI("⚠️ Lector desconectado. Revise el cable.", System.Windows.Media.Brushes.Red);
        }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) { }
        #endregion

        #region Procesamiento MULTI-HUELLA (El Algoritmo Principal)
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
                                if (_dedoActual == 1)
                                {
                                    // 💡 EXTRAEMOS LOS BYTES AL INSTANTE Y CREAMOS UN ENROLLER NUEVO
                                    _huella1Bytes = Enroller.Template.Bytes;
                                    _dedoActual++;
                                    Enroller = new DPFP.Processing.Enrollment(); 
                                    ActualizarEstadoUI("✔️ Dedo 1 OK. \nPASO 2/3: Ahora coloque su ÍNDICE IZQUIERDO 4 veces.", System.Windows.Media.Brushes.DarkOrange);
                                    IniciarCaptura();
                                }
                                else if (_dedoActual == 2)
                                {
                                    _huella2Bytes = Enroller.Template.Bytes;
                                    _dedoActual++;
                                    Enroller = new DPFP.Processing.Enrollment(); 
                                    ActualizarEstadoUI("✔️ Dedo 2 OK. \nPASO 3/3: Por último, coloque su PULGAR 4 veces.", System.Windows.Media.Brushes.Purple);
                                    IniciarCaptura();
                                }
                                else if (_dedoActual == 3)
                                {
                                    _huella3Bytes = Enroller.Template.Bytes;
                                    _huellasModificadas = true;
                                    btnGuardarRegistro.IsEnabled = true; 
                                    ActualizarEstadoUI("✅ ¡ÉXITO! Las 3 huellas han sido registradas. Ya puede guardar.", System.Windows.Media.Brushes.Green);
                                    DetenerCaptura(); 
                                }
                                break;

                            case DPFP.Processing.Enrollment.Status.Failed:
                                Enroller = new DPFP.Processing.Enrollment(); // Reinicio seguro
                                DetenerCaptura();
                                ActualizarEstadoUI($"❌ Fallo al leer Dedo {_dedoActual}. Limpie el sensor e intente de nuevo.", System.Windows.Media.Brushes.Red);
                                IniciarCaptura();
                                break;

                            case DPFP.Processing.Enrollment.Status.Insufficient:
                                ActualizarEstadoUI($"[Dedo {_dedoActual}] Faltan {Enroller.FeaturesNeeded} toques. Vuelva a poner el mismo dedo.", System.Windows.Media.Brushes.DarkSlateGray);
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
                                cuadroHuella.Source = bi;
                        });
                    }
                }
            }
            catch { }
        }

        private void CargarGradosCombo()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT IdGrado, NombreGrado FROM Cat_Grados ORDER BY IdGrado ASC";
                    DataTable dt = new DataTable();
                    using (SQLiteDataAdapter adaptador = new SQLiteDataAdapter(query, conexion))
                    {
                        adaptador.Fill(dt);
                    }
                    cmbGrado.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar catálogo de grados: " + ex.Message); }
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

        #region Lógica de Registro / Actualización
        private void BtnFoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Seleccionar imagen de perfil";
            op.Filter = "Archivos de imagen|*.jpg;*.jpeg;*.png";
            if (op.ShowDialog() == true) 
            {
                imgFoto.Source = new BitmapImage(new Uri(op.FileName));
                _fotoModificada = true; 
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
                MessageBox.Show("Faltan datos obligatorios. Llene todos los campos de texto.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_esEdicion && !_huellasModificadas)
            {
                MessageBox.Show("Debe completar el registro de los 3 dedos antes de guardar.", "Huellas Incompletas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "";
                    
                    if (_esEdicion)
                    {
                        query = @"UPDATE Personal_Naval SET Nombres = @nom, Apellidos = @ape, IdGrado = @idGrado, IdJefatura = @idJefa";
                        if (_fotoModificada) query += ", FotoPerfil = @foto";
                        if (_huellasModificadas) query += ", Huella = @h1, Huella2 = @h2, Huella3 = @h3";
                        query += " WHERE Matricula = @matOriginal";
                    }
                    else
                    {
                        query = @"INSERT INTO Personal_Naval 
                                 (Matricula, Nombres, Apellidos, IdGrado, IdJefatura, FotoPerfil, Huella, Huella2, Huella3) 
                                 VALUES (@mat, @nom, @ape, @idGrado, @idJefa, @foto, @h1, @h2, @h3)";
                    }

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    
                    cmd.Parameters.AddWithValue("@mat", txtMatricula.Text.Trim());
                    cmd.Parameters.AddWithValue("@nom", txtNombres.Text.Trim());
                    cmd.Parameters.AddWithValue("@ape", txtApellidos.Text.Trim());
                    if (_esEdicion) cmd.Parameters.AddWithValue("@matOriginal", _matriculaOriginal);
                    
                    cmd.Parameters.AddWithValue("@idGrado", Convert.ToInt32(cmbGrado.SelectedValue));
                    cmd.Parameters.AddWithValue("@idJefa", Convert.ToInt32(cmbJefatura.SelectedValue));
                    
                    if (_fotoModificada)
                    {
                        if (imgFoto.Source != null && this.FindName("bdrFoto") is Border bordeFoto)
                        {
                            int escala = 4; 
                            int w = (int)bordeFoto.ActualWidth * escala;
                            int h = (int)bordeFoto.ActualHeight * escala;

                            System.Windows.Media.DrawingVisual dibujoVisual = new System.Windows.Media.DrawingVisual();
                            using (System.Windows.Media.DrawingContext contexto = dibujoVisual.RenderOpen())
                            {
                                contexto.PushTransform(new System.Windows.Media.ScaleTransform(escala, escala));
                                contexto.DrawRectangle(new System.Windows.Media.VisualBrush(bordeFoto), null, new Rect(0, 0, bordeFoto.ActualWidth, bordeFoto.ActualHeight));
                            }

                            RenderTargetBitmap renderTarget = new RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            renderTarget.Render(dibujoVisual);

                            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                            encoder.QualityLevel = 90; 
                            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                            
                            using (MemoryStream ms = new MemoryStream())
                            {
                                encoder.Save(ms);
                                cmd.Parameters.AddWithValue("@foto", ms.ToArray()); 
                            }
                        }
                    }
                    else if (!_esEdicion) 
                    {
                        cmd.Parameters.AddWithValue("@foto", DBNull.Value);
                    }

                    if (_huellasModificadas)
                    {
                        cmd.Parameters.AddWithValue("@h1", _huella1Bytes);
                        cmd.Parameters.AddWithValue("@h2", _huella2Bytes);
                        cmd.Parameters.AddWithValue("@h3", _huella3Bytes);
                    }

                    cmd.ExecuteNonQuery();

                    MessageBox.Show(_esEdicion ? "Datos actualizados correctamente." : "Personal y sus 3 huellas registrados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close(); 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error crítico al guardar en Base de Datos: " + ex.Message, "Error SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

                private void CargarJefaturasCombo()
        {
            try
            {
                using (System.Data.SQLite.SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT IdJefatura, NombreJefatura FROM Cat_Jefaturas ORDER BY NombreJefatura ASC";
                    System.Data.DataTable dt = new System.Data.DataTable();
                    using (System.Data.SQLite.SQLiteDataAdapter adaptador = new System.Data.SQLite.SQLiteDataAdapter(query, conexion))
                    {
                        adaptador.Fill(dt);
                    }
                    cmbJefatura.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar jefaturas: " + ex.Message); }
        }


        private void BtnCancelar_Click(object sender, RoutedEventArgs e) { this.Close(); }
        #endregion
    }
}