using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace prueba1
{
    // --- CLASES AUXILIARES PARA EL CALENDARIO Y LA TABLA VISUAL ---
    public class DiaKardex
    {
        public string Dia { get; set; }
        public Brush ColorFondo { get; set; }
        public Brush ColorTexto { get; set; }
        public string ToolTipInfo { get; set; }
    }

    public class RegistroGrid
    {
        public string FechaHora { get; set; }
        public string MensajeAcceso { get; set; }
        public string NovedadMomento { get; set; }
        public Brush ColorTextoEstado { get; set; }
    }

    public partial class KardexWindow : Window
    {
        private string _matricula = "";

        public KardexWindow(string matricula)
        {
            InitializeComponent();
            _matricula = matricula;
            dpDesde.SelectedDate = DateTime.Now.AddDays(-15); // Ver última quincena por defecto
            dpHasta.SelectedDate = DateTime.Now;
            
            CargarDatosPersonales();
            CargarHistorialInteligente();
        }

        private void CargarDatosPersonales()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT Nombres, Apellidos, IdGrado, IdJefatura, FotoPerfil FROM Personal_Naval WHERE Matricula=@mat";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@mat", _matricula);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtNombreMarino.Text = $"{reader["Nombres"]} {reader["Apellidos"]}";
                            txtDatosMarino.Text = $"Matrícula: {_matricula}"; // Aquí podrías cruzar con la tabla de Grados/Jefaturas

                            // Cargar la foto si existe
                            if (reader["FotoPerfil"] != DBNull.Value)
                            {
                                byte[] fotoBytes = (byte[])reader["FotoPerfil"];
                                imgFotoKardex.Source = ConvertirBytesAImagen(fotoBytes);
                            }
                            else
                            {
                                imgFotoKardex.Source = new BitmapImage(new Uri("https://cdn-icons-png.flaticon.com/512/3135/3135715.png"));
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void BtnConsultar_Click(object sender, RoutedEventArgs e) 
        { 
            CargarHistorialInteligente(); 
        }

        // MOTOR PRINCIPAL DE AUDITORÍA Y FALTAS
        private void CargarHistorialInteligente()
        {
            if (!dpDesde.SelectedDate.HasValue || !dpHasta.SelectedDate.HasValue) return;

            DateTime fechaInicio = dpDesde.SelectedDate.Value.Date;
            DateTime fechaFin = dpHasta.SelectedDate.Value.Date;

            // Validación para no trabar el sistema si piden 10 años de golpe
            if ((fechaFin - fechaInicio).TotalDays > 60)
            {
                MessageBox.Show("Para visualizar el mapa de asistencia, el rango máximo permitido es de 60 días.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 1. Extraemos todos los accesos crudos de la base de datos
                DataTable dtAccesosCrudos = new DataTable();
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT FechaHora, MensajeAcceso, NovedadMomento FROM Registro_Accesos WHERE Matricula=@mat AND FechaHora >= @desde AND FechaHora <= @hasta ORDER BY FechaHora ASC";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@mat", _matricula);
                    cmd.Parameters.AddWithValue("@desde", fechaInicio.ToString("yyyy-MM-dd 00:00:00"));
                    cmd.Parameters.AddWithValue("@hasta", fechaFin.ToString("yyyy-MM-dd 23:59:59"));

                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(cmd);
                    adaptador.Fill(dtAccesosCrudos);
                }

                // 2. Preparamos las listas que llenarán los gráficos
                List<DiaKardex> calendario = new List<DiaKardex>();
                List<RegistroGrid> registrosTabla = new List<RegistroGrid>();

                // Convertimos el Brush desde Hexadecimal
                Brush brushPresenteFondo = (Brush)new BrushConverter().ConvertFrom("#D1FAE5"); // Verde muy claro
                Brush brushPresenteTexto = (Brush)new BrushConverter().ConvertFrom("#065F46"); // Verde oscuro
                
                Brush brushFaltaFondo = (Brush)new BrushConverter().ConvertFrom("#FEE2E2"); // Rojo muy claro
                Brush brushFaltaTexto = (Brush)new BrushConverter().ConvertFrom("#991B1B"); // Rojo oscuro

                Brush brushFuturoFondo = (Brush)new BrushConverter().ConvertFrom("#F1F5F9"); // Gris claro
                Brush brushFuturoTexto = (Brush)new BrushConverter().ConvertFrom("#94A3B8"); // Gris oscuro

                // 3. LA MAGIA: Iteramos día por día en el calendario
                for (DateTime d = fechaInicio; d <= fechaFin; d = d.AddDays(1))
                {
                    // Buscamos si en los registros crudos hay alguna lectura para este día específico
                    var accesosDelDia = dtAccesosCrudos.AsEnumerable()
                        .Where(row => Convert.ToDateTime(row["FechaHora"]).Date == d.Date)
                        .ToList();

                    if (accesosDelDia.Count > 0)
                    {
                        // SÍ VINO ESE DÍA (Asistencia o Justificación)
                        calendario.Add(new DiaKardex 
                        { 
                            Dia = d.Day.ToString(), 
                            ColorFondo = brushPresenteFondo, 
                            ColorTexto = brushPresenteTexto,
                            ToolTipInfo = d.ToString("dd/MMM/yyyy") + " - Asistencia Registrada"
                        });

                        // Agregamos todas las lecturas de ese día a la tabla
                        foreach (var acceso in accesosDelDia)
                        {
                            registrosTabla.Add(new RegistroGrid 
                            { 
                                FechaHora = Convert.ToDateTime(acceso["FechaHora"]).ToString("dd/MMM/yyyy - HH:mm:ss"), 
                                MensajeAcceso = acceso["MensajeAcceso"].ToString(), 
                                NovedadMomento = acceso["NovedadMomento"].ToString(),
                                ColorTextoEstado = brushPresenteTexto
                            });
                        }
                    }
                    else
                    {
                        // NO HAY REGISTRO DE ESE DÍA EN LA BASE DE DATOS
                        if (d > DateTime.Now.Date)
                        {
                            // Es un día del futuro, no es falta, solo es gris
                            calendario.Add(new DiaKardex { Dia = d.Day.ToString(), ColorFondo = brushFuturoFondo, ColorTexto = brushFuturoTexto, ToolTipInfo = d.ToString("dd/MMM/yyyy") + " - Sin evaluar" });
                        }
                        else
                        {
                            // Es un día del pasado y no vino: FALTA
                            calendario.Add(new DiaKardex { Dia = d.Day.ToString(), ColorFondo = brushFaltaFondo, ColorTexto = brushFaltaTexto, ToolTipInfo = d.ToString("dd/MMM/yyyy") + " - Falta Injustificada" });
                            
                            // Inyectamos visualmente la falta en la tabla para que quede documentada
                            registrosTabla.Add(new RegistroGrid 
                            { 
                                FechaHora = d.ToString("dd/MMM/yyyy") + " - SIN LECTURA", 
                                MensajeAcceso = "⚠️ FALTA NO JUSTIFICADA", 
                                NovedadMomento = "El sistema no detectó registro biométrico.",
                                ColorTextoEstado = brushFaltaTexto
                            });
                        }
                    }
                }

                // Invertimos la lista de la tabla para que las fechas más recientes salgan hasta arriba
                registrosTabla.Reverse();

                // Llenamos la UI
                dgHistorial.ItemsSource = registrosTabla;
                icCalendario.ItemsSource = calendario;
            }
            catch (Exception ex) 
            { 
                MessageBox.Show("Error al construir Kardex: " + ex.Message); 
            }
        }

        // Utilidad para transformar el arreglo de Bytes a Imagen visible
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
    }
}