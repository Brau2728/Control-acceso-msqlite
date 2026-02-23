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
            string usuario = txtUser.Text;
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
                    string query = "SELECT Rol FROM Usuarios_Sistema WHERE Username=@user AND PasswordHash=@pass";
                    SQLiteCommand cmd = new SQLiteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@user", usuario);
                    cmd.Parameters.AddWithValue("@pass", password);

                    object resultado = cmd.ExecuteScalar();

                    if (resultado != null)
                    {
                        RolUsuario = resultado.ToString();
                        this.DialogResult = true; 
                    }
                    else
                    {
                        MessageBox.Show("Usuario o contraseña incorrectos.");
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