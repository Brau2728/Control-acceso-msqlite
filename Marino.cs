using System.Windows.Media;

namespace prueba1
{
    public class Marino
    {
        public string Matricula { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string Grado { get; set; } = string.Empty;
        public string CuerpoServicio { get; set; } = string.Empty;
        public string Jefatura { get; set; } = string.Empty;
        public string EstadoAsistencia { get; set; } = "PRESENTE";
        
        // Propiedad para cargar la imagen en la interfaz
        public ImageSource? FotoImagen { get; set; }

        public string NombreCompleto => $"{Nombre} {Apellidos}";
    }
}