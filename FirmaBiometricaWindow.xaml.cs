using System;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using DPFP;
using DPFP.Verification;

namespace prueba1
{
    public partial class FirmaBiometricaWindow : Window, DPFP.Capture.EventHandler
    {
        private DPFP.Capture.Capture Capturer;
        private Verification Verificator;

        public string NombreFirmante { get; private set; } = "";
        public bool FirmaExitosa { get; private set; } = false;

        public FirmaBiometricaWindow()
        {
            InitializeComponent();
            this.Loaded += (s, e) => { InitHuella(); IniciarCaptura(); };
            this.Closed += (s, e) => { DetenerCaptura(); };
        }

        private void InitHuella()
        {
            try
            {
                Capturer = new DPFP.Capture.Capture();
                Verificator = new Verification();
                if (Capturer != null) Capturer.EventHandler = this;
            }
            catch (Exception ex) { MessageBox.Show("Error iniciando lector: " + ex.Message); }
        }

        private void IniciarCaptura() { if (Capturer != null) try { Capturer.StartCapture(); } catch { } }
        private void DetenerCaptura() { if (Capturer != null) try { Capturer.StopCapture(); } catch { } }

        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            DPFP.Processing.FeatureExtraction extractor = new DPFP.Processing.FeatureExtraction();
            DPFP.Capture.CaptureFeedback feedback = DPFP.Capture.CaptureFeedback.None;
            DPFP.FeatureSet features = new DPFP.FeatureSet();

            extractor.CreateFeatureSet(Sample, DPFP.Processing.DataPurpose.Verification, ref feedback, ref features);

            if (feedback == DPFP.Capture.CaptureFeedback.Good)
            {
                VerificarIdentidad(features);
            }
            else
            {
                ActualizarUI("Mala lectura. Intente de nuevo.", System.Windows.Media.Brushes.DarkOrange);
            }
        }

        // 💡 FUNCIÓN AUXILIAR PARA COMPARAR HUELLAS
        private bool CompararHuellaFirma(DPFP.FeatureSet features, object huellaBDObj)
        {
            if (huellaBDObj == null || huellaBDObj == DBNull.Value) return false;
            try 
            {
                byte[] huellaBD = (byte[])huellaBDObj;
                DPFP.Template templateGuardado = new DPFP.Template(new MemoryStream(huellaBD));
                Verification.Result result = new Verification.Result();
                Verificator.Verify(features, templateGuardado, ref result);
                return result.Verified;
            }
            catch { return false; }
        }

        private void VerificarIdentidad(DPFP.FeatureSet featuresCapturadas)
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    // 💡 SOLUCIÓN: Buscamos en las 3 columnas de huellas
                    string query = "SELECT Nombres, Apellidos, IdGrado, Huella, Huella2, Huella3 FROM Personal_Naval WHERE Estatus = 'ACTIVO'";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool match1 = CompararHuellaFirma(featuresCapturadas, reader["Huella"]);
                            bool match2 = CompararHuellaFirma(featuresCapturadas, reader["Huella2"]);
                            bool match3 = CompararHuellaFirma(featuresCapturadas, reader["Huella3"]);

                            if (match1 || match2 || match3)
                            {
                                string grado = ObtenerNombreGrado(Convert.ToInt32(reader["IdGrado"]));
                                NombreFirmante = $"{grado} {reader["Nombres"]} {reader["Apellidos"]}";
                                FirmaExitosa = true;

                                this.Dispatcher.Invoke(() => { this.DialogResult = true; }); 
                                return;
                            }
                        }
                    }
                }
                ActualizarUI("Huella no autorizada o no registrada.", System.Windows.Media.Brushes.Red);
            }
            catch (Exception ex) { ActualizarUI("Error de BD: " + ex.Message, System.Windows.Media.Brushes.Red); }
        }

        private void ActualizarUI(string msg, System.Windows.Media.Brush color)
        {
            this.Dispatcher.Invoke(() => { txtEstadoFirma.Text = msg; txtEstadoFirma.Foreground = color; });
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) { this.DialogResult = false; }

        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) { }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) { }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) { }
        
        private string ObtenerNombreGrado(int id)
        {
            string[] grados = { "Otro", "Marinero", "Cabo", "Tercer Maestre", "Segundo Maestre", "Primer Maestre", "TTE. Corbeta", "TTE. Fragata", "TTE. Navío", "CAP. Corbeta", "CAP. Fragata", "CAP. Navío", "Contralmirante", "Vicealmirante", "Almirante" };
            if (id >= 1 && id <= grados.Length) return grados[id - 1];
            return "DESCONOCIDO";
        }
    }
}