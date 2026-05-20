using System;
using System.Data.SQLite;
using System.Windows;
using prueva1;

namespace prueba1
{
    public partial class LoginWindow : Window
    {
        public string RolUsuario { get; private set; } = "admin";

        public LoginWindow()
        {
            InitializeComponent();
        }

       private void Login_Click(object sender, RoutedEventArgs e)
{
    string usuario = txtUser.Text.Trim();
    string password = txtPass.Password;

    if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
    {
        MessageBox.Show("Por favor ingresa usuario y contraseña.");
        return;
    }

    try
    {
        using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
        {
            // OJO: Ahora pedimos el PasswordHash y el Rol, no solo validamos directo
            string query = "SELECT PasswordHash, Rol FROM Usuarios_Sistema WHERE Username=@user";
            SQLiteCommand cmd = new SQLiteCommand(query, conexion);
            cmd.Parameters.AddWithValue("@user", usuario);

            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read()) // Si encontró al usuario
                {
                    string hashGuardado = reader["PasswordHash"].ToString();
                    string rolGuardado = reader["Rol"].ToString();

                    // BCrypt se encarga de verificar si la contraseña coincide con el Hash
                    bool esValida = BCrypt.Net.BCrypt.Verify(password, hashGuardado);

                    if (esValida)
                    {
                        RolUsuario = rolGuardado;
                        this.DialogResult = true; 
                    }
                    else
                    {
                        MessageBox.Show("Contraseña incorrecta.", "Error de acceso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Usuario no encontrado.", "Error de acceso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error: " + ex.Message);
    }
}

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}