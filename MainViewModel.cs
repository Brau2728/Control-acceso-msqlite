using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        }

        public void AccesoAutorizado(Marino marino)
        {
            _limpiezaTimer.Stop();
            MarinoActual = marino;
            StatusMensaje = "✅ ACCESO AUTORIZADO";
            StatusColor = new SolidColorBrush(Color.FromRgb(34, 139, 34)); 
            _limpiezaTimer.Start(); 
        }

        public void AccesoDenegado()
        {
            _limpiezaTimer.Stop();
            
            // 💡 SOLUCIÓN: Quitamos el Freeze() porque la imagen carga de internet
            BitmapImage imgError = new BitmapImage(new Uri("https://cdn-icons-png.flaticon.com/512/1144/1144760.png"));

            MarinoActual = new Marino 
            {
                Matricula = "--------",
                Nombre = "USUARIO NO",
                Apellidos = "REGISTRADO",
                Grado = "DESCONOCIDO",
                Jefatura = "DENEGADO",
                FotoImagen = imgError
            };
            
            StatusMensaje = "❌ HUELLA NO RECONOCIDA";
            StatusColor = Brushes.DarkRed;
            _limpiezaTimer.Start(); 
        }

        public void AccesoDenegadoBaja(Marino marino)
        {
            _limpiezaTimer.Stop();
            MarinoActual = marino;
            StatusMensaje = "❌ ACCESO DENEGADO (BAJA)";
            StatusColor = Brushes.DarkRed;
            _limpiezaTimer.Start();
        }

        public void AccesoConNovedad(Marino marino)
        {
            _limpiezaTimer.Stop();
            MarinoActual = marino;
            StatusMensaje = $"⚠️ ATENCIÓN: {marino.Novedad}";
            StatusColor = Brushes.DarkOrange;
            _limpiezaTimer.Start();
        }

        public void MalaCaptura()
        {
            _limpiezaTimer.Stop();
            
            // 💡 SOLUCIÓN: Quitamos el Freeze() porque la imagen carga de internet
            BitmapImage imgMala = new BitmapImage(new Uri("https://cdn-icons-png.flaticon.com/512/2807/2807350.png"));

            MarinoActual = new Marino 
            {
                Matricula = "ERROR",
                Nombre = "VUELVA A",
                Apellidos = "INTENTAR",
                Grado = "MALA LECTURA",
                Jefatura = "SENSOR SUCIO O MOVIMIENTO",
                FotoImagen = imgMala
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