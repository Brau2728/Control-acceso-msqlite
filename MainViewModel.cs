using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace prueba1
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _statusMensaje = "ESPERANDO PERSONAL...";
        private Brush _statusColor = Brushes.Gray;
        private string _fechaHoraActual = string.Empty;
        private Marino? _marinoActual;

        private DispatcherTimer _relojTimer;
        private DispatcherTimer _limpiezaTimer;

        // Propiedad para almacenar la imagen por defecto descargada
        private BitmapImage _defaultProfileImage;

        public string StatusMensaje { get => _statusMensaje; set { _statusMensaje = value; OnPropertyChanged(); } }
        public Brush StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }
        public string FechaHoraActual { get => _fechaHoraActual; set { _fechaHoraActual = value; OnPropertyChanged(); } }
        public Marino? MarinoActual { get => _marinoActual; set { _marinoActual = value; OnPropertyChanged(); } }

        public MainViewModel()
        {
            _relojTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _relojTimer.Tick += (s, e) => FechaHoraActual = DateTime.Now.ToString("dd/MM/yyyy | HH:mm:ss");
            _relojTimer.Start();

            _limpiezaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _limpiezaTimer.Tick += (s, e) => LimpiarPantalla();

            // Cargamos la imagen por defecto desde la carpeta local assets de forma inmediata
            LoadDefaultImage();
        }

        // Método mejorado para asegurar que WPF encuentre la imagen
        private void LoadDefaultImage()
        {
            try
            {
            // Ruta física en el directorio de ejecución (bin/Debug/assets/iconmarino.jpg)

                string localImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "iconmarino.jpg");

                if (File.Exists(localImagePath))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 💡 FIX: Leemos la imagen a la memoria primero. 
                        // Esto garantiza que WPF la cargue completa antes del Freeze() y permite que el archivo no quede bloqueado.
                        byte[] imageBytes = File.ReadAllBytes(localImagePath);

                        using (MemoryStream ms = new MemoryStream(imageBytes))
                        {
                            _defaultProfileImage = new BitmapImage();
                            _defaultProfileImage.BeginInit();
                            _defaultProfileImage.CacheOption = BitmapCacheOption.OnLoad;
                            _defaultProfileImage.StreamSource = ms;
                            _defaultProfileImage.EndInit();
                            _defaultProfileImage.Freeze(); // Ahora sí, la congelamos de forma segura
                        }
                    });
                }
                else
                {
                    _defaultProfileImage = null; // Si no existe el archivo, no mostramos nada
                }
            }
            catch (Exception)
            {
                // Si hay algún error (falta de permisos, etc.), la dejamos nula para no crashear
                _defaultProfileImage = null; 
            }
        }

        public void AccesoAutorizado(Marino marino)
        {
            _limpiezaTimer.Stop();
            
            // Asignamos la imagen por defecto si marino.FotoImagen es null
            if (marino.FotoImagen == null)
            {
                marino.FotoImagen = _defaultProfileImage;
            }

            MarinoActual = marino;
            StatusMensaje = "✅ ACCESO AUTORIZADO";
            StatusColor = new SolidColorBrush(Color.FromRgb(34, 139, 34)); 
            _limpiezaTimer.Start(); 
        }

        public void AccesoDenegado()
        {
            _limpiezaTimer.Stop();
            
            MarinoActual = new Marino 
            {
                Matricula = "--------",
                Nombre = "USUARIO NO",
                Apellidos = "REGISTRADO",
                Grado = "DESCONOCIDO",
                Jefatura = "DENEGADO",
                FotoImagen = _defaultProfileImage // Usamos la imagen por defecto local
            };
            
            StatusMensaje = "❌ HUELLA NO RECONOCIDA";
            StatusColor = Brushes.DarkRed;
            _limpiezaTimer.Start(); 
        }

        public void AccesoDenegadoBaja(Marino marino)
        {
            _limpiezaTimer.Stop();
            
            if (marino.FotoImagen == null)
            {
                marino.FotoImagen = _defaultProfileImage;
            }

            MarinoActual = marino;
            StatusMensaje = "❌ ACCESO DENEGADO (BAJA)";
            StatusColor = Brushes.DarkRed;
            _limpiezaTimer.Start();
        }

        public void AccesoConNovedad(Marino marino)
        {
            _limpiezaTimer.Stop();

            if (marino.FotoImagen == null)
            {
                marino.FotoImagen = _defaultProfileImage;
            }

            MarinoActual = marino;
            StatusMensaje = $"⚠️ ATENCIÓN: {marino.Novedad}";
            StatusColor = Brushes.DarkOrange;
            _limpiezaTimer.Start();
        }

        public void MalaCaptura()
        {
            _limpiezaTimer.Stop();
            
            MarinoActual = new Marino 
            {
                Matricula = "ERROR",
                Nombre = "VUELVA A",
                Apellidos = "INTENTAR",
                Grado = "MALA LECTURA",
                Jefatura = "SENSOR SUCIO O MOVIMIENTO",
                FotoImagen = _defaultProfileImage // Usamos la imagen por defecto local
            };
            
            StatusMensaje = "⚠️ MALA LECTURA";
            StatusColor = Brushes.DarkOrange;
            _limpiezaTimer.Start(); 
        }

        private void LimpiarPantalla()
        {
            MarinoActual = null;
            StatusMensaje = "ESPERANDO PERSONAL...";
            StatusColor = Brushes.Gray;
            _limpiezaTimer.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}