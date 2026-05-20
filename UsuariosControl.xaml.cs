using System;
using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace prueba1
{
    public partial class UsuariosControl : UserControl
    {
        private int _idUsuarioSeleccionado = 0;

        public UsuariosControl()
        {
            InitializeComponent();
            CargarUsuarios();
        }

        private void CargarUsuarios()
        {
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT Id, Username, Rol FROM Usuarios_Sistema"; 
                    SQLiteDataAdapter adaptador = new SQLiteDataAdapter(new SQLiteCommand(query, conexion));
                    DataTable dt = new DataTable();
                    adaptador.Fill(dt);
                    dgUsuarios.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al cargar usuarios: " + ex.Message); }
        }

        private void BtnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUserName.Text) || cmbUserRol.SelectedItem == null)
            {
                MessageBox.Show("El nombre de usuario y el rol son obligatorios.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    SQLiteCommand cmd = new SQLiteCommand();
                    cmd.Connection = conexion;

                    if (_idUsuarioSeleccionado == 0)
                    {
                        if (string.IsNullOrWhiteSpace(txtUserPass.Password))
                        {
                            MessageBox.Show("Debe asignar una contraseña al nuevo usuario.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        cmd.CommandText = "INSERT INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES (@user, @pass, @rol)";
                        cmd.Parameters.AddWithValue("@user", txtUserName.Text.Trim());
                        string hashContrasena = BCrypt.Net.BCrypt.HashPassword(txtUserPass.Password);
                        cmd.Parameters.AddWithValue("@pass", hashContrasena);
                        cmd.Parameters.AddWithValue("@rol", ((ComboBoxItem)cmbUserRol.SelectedItem).Content.ToString());
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(txtUserPass.Password))
                        {
                            cmd.CommandText = "UPDATE Usuarios_Sistema SET Username=@user, Rol=@rol WHERE Id=@id";
                        }
                        else
                        {
                            cmd.CommandText = "UPDATE Usuarios_Sistema SET Username=@user, PasswordHash=@pass, Rol=@rol WHERE Id=@id";
                            string hashContrasenaEdicion = BCrypt.Net.BCrypt.HashPassword(txtUserPass.Password);
                            cmd.Parameters.AddWithValue("@pass", hashContrasenaEdicion);
                        }
                        cmd.Parameters.AddWithValue("@user", txtUserName.Text.Trim());
                        cmd.Parameters.AddWithValue("@rol", ((ComboBoxItem)cmbUserRol.SelectedItem).Content.ToString());
                        cmd.Parameters.AddWithValue("@id", _idUsuarioSeleccionado);
                    }

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Usuario guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnLimpiarUsuario_Click(null, null);
                    CargarUsuarios();
                }
            }
            catch (SQLiteException ex)
            {
                if (ex.ResultCode == SQLiteErrorCode.Constraint)
                    MessageBox.Show("Ese nombre de usuario ya existe. Elija otro.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void BtnEditarUsuario_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                _idUsuarioSeleccionado = Convert.ToInt32(boton.CommandParameter);

                try
                {
                    using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                    {
                        SQLiteCommand cmd = new SQLiteCommand("SELECT Username, Rol FROM Usuarios_Sistema WHERE Id=@id", conexion);
                        cmd.Parameters.AddWithValue("@id", _idUsuarioSeleccionado);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtUserName.Text = reader["Username"].ToString();
                                foreach (ComboBoxItem item in cmbUserRol.Items)
                                {
                                    if (item.Content.ToString() == reader["Rol"].ToString())
                                    {
                                        cmbUserRol.SelectedItem = item;
                                        break;
                                    }
                                }
                                txtUserPass.Password = ""; 
                            }
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error al cargar usuario: " + ex.Message); }
            }
        }

        private void BtnEliminarUsuario_Click(object sender, RoutedEventArgs e)
        {
            Button boton = sender as Button;
            if (boton != null && boton.CommandParameter != null)
            {
                int idEliminar = Convert.ToInt32(boton.CommandParameter);
                if (MessageBox.Show("¿Está seguro de eliminar este acceso permanentemente?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                        {
                            SQLiteCommand cmd = new SQLiteCommand("DELETE FROM Usuarios_Sistema WHERE Id=@id", conexion);
                            cmd.Parameters.AddWithValue("@id", idEliminar);
                            cmd.ExecuteNonQuery();
                        }
                        CargarUsuarios();
                    }
                    catch (Exception ex) { MessageBox.Show("Error al eliminar: " + ex.Message); }
                }
            }
        }

        private void BtnLimpiarUsuario_Click(object sender, RoutedEventArgs e)
        {
            _idUsuarioSeleccionado = 0;
            txtUserName.Text = "";
            txtUserPass.Password = "";
            cmbUserRol.SelectedIndex = -1;
        }
    }
}