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
        public string FotoPath { get; set; } = "https://cdn-icons-png.flaticon.com/512/3135/3135715.png";

        public string NombreCompleto => $"{Nombre} {Apellidos}";
    }
}