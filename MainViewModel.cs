using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;

namespace prueba1
{
    public class MainViewModel : INotifyPropertyChanged
    {
        // Propiedades de Interfaz
        private string _statusMensaje = "ESPERANDO PERSONAL...";
        private Brush _statusColor = Brushes.Gray;
        private string _fechaHoraActual = string.Empty;
        private Marino? _marinoActual;

        // Timers y Control
        private DispatcherTimer _relojTimer;
        private DispatcherTimer _limpiezaTimer;
        private int _indiceSimulacion = 0;

        public string StatusMensaje { get => _statusMensaje; set { _statusMensaje = value; OnPropertyChanged(); } }
        public Brush StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }
        public string FechaHoraActual { get => _fechaHoraActual; set { _fechaHoraActual = value; OnPropertyChanged(); } }
        public Marino? MarinoActual { get => _marinoActual; set { _marinoActual = value; OnPropertyChanged(); } }

        // Datos de Prueba
        private List<Marino> _dbSimulada = new List<Marino>
        {
            new Marino { Matricula = "C-4981372", Grado = "TTE. NAV.", Nombre = "EDUARDO", Apellidos = "GRANDE AGUIRRE", EstadoAsistencia = "PRESENTE", Jefatura = "COMUNAV" },
            new Marino { Matricula = "C-8129301", Grado = "CABO", Nombre = "JUAN", Apellidos = "PÉREZ LÓPEZ", EstadoAsistencia = "RETARDO", Jefatura = "TALLERES" },
            new Marino { Matricula = "C-1122334", Grado = "3ER MTRE.", Nombre = "LUIS", Apellidos = "MÉNDEZ RUIZ", EstadoAsistencia = "HOSPITALIZADO", Jefatura = "HOSPITAL NAVAL" }
        };

        public MainViewModel()
        {
            // Configurar Reloj (Actualiza cada segundo)
            _relojTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _relojTimer.Tick += (s, e) => FechaHoraActual = DateTime.Now.ToString("dd/MM/yyyy | HH:mm:ss");
            _relojTimer.Start();

            // Configurar Timer de Limpieza (5 segundos)
            _limpiezaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _limpiezaTimer.Tick += (s, e) => LimpiarPantalla();
        }

        public void ProcesarLectura()
        {
            // Reinicio Inteligente: Si hay un proceso activo, lo detenemos para iniciar de cero
            _limpiezaTimer.Stop();

            var marino = _dbSimulada[_indiceSimulacion];
            MarinoActual = marino;

            // Lógica de Semáforo Institucional
            switch (marino.EstadoAsistencia)
            {
                case "PRESENTE":
                    StatusMensaje = "✅ ACCESO AUTORIZADO";
                    StatusColor = new SolidColorBrush(Color.FromRgb(34, 139, 34)); // Verde
                    break;
                case "RETARDO":
                    StatusMensaje = "🟠 RETARDO REGISTRADO";
                    StatusColor = Brushes.Orange;
                    break;
                default:
                    StatusMensaje = $"ℹ️ STATUS: {marino.EstadoAsistencia}";
                    StatusColor = new SolidColorBrush(Color.FromRgb(0, 51, 102)); // Azul Marino
                    break;
            }

            // Iniciar cuenta regresiva para limpiar
            _limpiezaTimer.Start();

            // Ciclar datos
            _indiceSimulacion = (_indiceSimulacion + 1) % _dbSimulada.Count;
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