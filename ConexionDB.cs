using System.Data.SQLite;
using System.IO;

namespace prueva1
{
    public class ConexionDB
    {
        private static string dbName = "SemaforoMarina.db";
        private static string connectionString = $"Data Source={dbName};Version=3;";

        public static SQLiteConnection ObtenerConexion()
        {
            // 1. Si de plano no existe el archivo, lo crea
            if (!File.Exists(dbName))
            {
                SQLiteConnection.CreateFile(dbName);
            }

            // 2. Abrimos la conexión
            SQLiteConnection conexion = new SQLiteConnection(connectionString);
            conexion.Open();

            // 3. BLINDAJE: Siempre intentará crear las tablas si faltan (por si el archivo estaba vacío)
            string sql = @"
            CREATE TABLE IF NOT EXISTS Usuarios_Sistema (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                Username TEXT UNIQUE, 
                PasswordHash TEXT, 
                Rol TEXT
            );
            
            CREATE TABLE IF NOT EXISTS Personal_Naval (
                IdPersonal INTEGER PRIMARY KEY AUTOINCREMENT, 
                Matricula TEXT UNIQUE, 
                Nombres TEXT, 
                Apellidos TEXT, 
                IdGrado INTEGER, 
                IdJefatura INTEGER, 
                FotoPerfil BLOB, 
                Huella BLOB
            );
            
            -- Insertamos los usuarios base solo si no existen (evita duplicados)
            INSERT OR IGNORE INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES ('admin', '1234', 'ADMIN');
            INSERT OR IGNORE INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES ('guardia', '1234', 'GUARDIA');
            ";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, conexion))
            {
                cmd.ExecuteNonQuery();
            }

            // Devolvemos la conexión ya lista y con las tablas garantizadas
            return conexion;
        }
    }
}