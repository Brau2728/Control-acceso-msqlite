using System;
using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace prueba1
{
    public partial class GestionGradosControl : UserControl
    {
        private int _idGradoSeleccionado = 0;

        public GestionGradosControl()
        {
            InitializeComponent();
            CargarGrados();
        }

        private void CargarGrados()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT IdGrado, NombreGrado FROM Cat_Grados ORDER BY IdGrado ASC";
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(new SQLiteCommand(query, conexion));
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);
                    dgGrados.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar grados: " + ex.Message); }
        }

        private void DgGrados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgGrados.SelectedItem is DataRowView row)
            {
                _idGradoSeleccionado = Convert.ToInt32(row["IdGrado"]);
                txtNombreGrado.Text = row["NombreGrado"].ToString();
                
                lblTituloForm.Text = "✏️ Editar Grado / Título";
                btnEliminar.Visibility = Visibility.Visible;
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombreGrado.Text))
            {
                MessageBox.Show("El nombre del grado no puede estar vacío.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    SQLiteCommand cmd = new SQLiteCommand();
                    cmd.Connection = conexion;

                    if (_idGradoSeleccionado == 0) // NUEVO REGISTRO
                    {
                        cmd.CommandText = "INSERT INTO Cat_Grados (NombreGrado) VALUES (@nombre)";
                    }
                    else // ACTUALIZAR EXISTENTE
                    {
                        cmd.CommandText = "UPDATE Cat_Grados SET NombreGrado = @nombre WHERE IdGrado = @id";
                        cmd.Parameters.AddWithValue("@id", _idGradoSeleccionado);
                    }

                    cmd.Parameters.AddWithValue("@nombre", txtNombreGrado.Text.Trim().ToUpper());
                    cmd.ExecuteNonQuery();
                    
                    MessageBox.Show("Catálogo actualizado con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnLimpiar_Click(null, null);
                    CargarGrados();
                }
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                MessageBox.Show("Este Grado o Título ya existe en el sistema.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { MessageBox.Show("Error al guardar: " + ex.Message); }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_idGradoSeleccionado == 0) return;

            // Bloqueamos la eliminación de los primeros 15 rangos para evitar romper registros base
            if (_idGradoSeleccionado <= 15)
            {
                MessageBox.Show("Por seguridad del sistema, los rangos militares por defecto (1 al 15) no pueden ser eliminados. Puede modificar su texto si detecta un error de ortografía.", "Operación Bloqueada", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (MessageBox.Show("¿Está seguro de eliminar este Título/Grado? (No debe haber personal activo usándolo).", "Confirmar Baja", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                    {
                        string query = "DELETE FROM Cat_Grados WHERE IdGrado = @id";
                        using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                        {
                            cmd.Parameters.AddWithValue("@id", _idGradoSeleccionado);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    BtnLimpiar_Click(null, null);
                    CargarGrados();
                }
                catch (Exception ex) { MessageBox.Show("Error al eliminar (Es probable que algún elemento aún esté usando este grado): " + ex.Message); }
            }
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            _idGradoSeleccionado = 0;
            txtNombreGrado.Text = "";
            lblTituloForm.Text = "➕ Registrar Nuevo Título";
            btnEliminar.Visibility = Visibility.Collapsed;
            dgGrados.SelectedItem = null;
        }
    }
}