using Microsoft.Win32;
using System;
using System.Data.SQLite; // <--- EL CAMBIO SALVAVIDAS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using prueva1;

namespace prueba1 
{
    public partial class RegistroPersonal : Window
    {
        private byte[] fotoBytes = null; 

        public RegistroPersonal()
        {
            InitializeComponent();
        }

        private void BtnFoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Seleccionar imagen";
            op.Filter = "Archivos de imagen|*.jpg;*.jpeg;*.png";
            
            if (op.ShowDialog() == true)
            {
                imgFoto.Source = new BitmapImage(new Uri(op.FileName));
                fotoBytes = File.ReadAllBytes(op.FileName);
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMatricula.Text) || 
                string.IsNullOrWhiteSpace(txtNombres.Text) ||
                cmbGrado.SelectedItem == null || 
                cmbJefatura.SelectedItem == null)
            {
                MessageBox.Show("Faltan datos obligatorios.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // AQUÍ ESTABA EL ERROR: Cambiamos SqlConnection por SQLiteConnection
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = @"INSERT INTO Personal_Naval 
                                     (Matricula, Nombres, Apellidos, IdGrado, IdJefatura, FotoPerfil) 
                                     VALUES 
                                     (@mat, @nom, @ape, @idGrado, @idJefa, @foto)";

                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    
                    cmd.Parameters.AddWithValue("@mat", txtMatricula.Text);
                    cmd.Parameters.AddWithValue("@nom", txtNombres.Text);
                    cmd.Parameters.AddWithValue("@ape", txtApellidos.Text);
                    
                    int idGrado = int.Parse(((ComboBoxItem)cmbGrado.SelectedItem).Tag.ToString());
                    int idJefa = int.Parse(((ComboBoxItem)cmbJefatura.SelectedItem).Tag.ToString());
                    
                    cmd.Parameters.AddWithValue("@idGrado", idGrado);
                    cmd.Parameters.AddWithValue("@idJefa", idJefa);

                    if (fotoBytes != null)
                        cmd.Parameters.AddWithValue("@foto", fotoBytes);
                    else
                        cmd.Parameters.AddWithValue("@foto", DBNull.Value);

                    int filas = cmd.ExecuteNonQuery();

                    if (filas > 0)
                    {
                        MessageBox.Show("Personal registrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close(); 
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar en BD: " + ex.Message);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnRotarIzq_Click(object sender, RoutedEventArgs e) { rtRotacion.Angle -= 90; }
        private void BtnRotarDer_Click(object sender, RoutedEventArgs e) { rtRotacion.Angle += 90; }
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e) { stEscala.ScaleX += 0.1; stEscala.ScaleY += 0.1; }
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) { 
            if (stEscala.ScaleX > 0.2) { stEscala.ScaleX -= 0.1; stEscala.ScaleY -= 0.1; } 
        }
    }
}