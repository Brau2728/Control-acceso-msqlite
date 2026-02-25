using System;
using System.Data.SQLite;
using System.IO;

namespace prueba1 // Asegúrate de que coincida con tu namespace (prueba1 o prueva1)
{
    public class ConexionDB
    {
        private static string dbName = "SemaforoMarina.db";
        private static string connectionString = $"Data Source={dbName};Version=3;";

        public static SQLiteConnection ObtenerConexion()
        {
            if (!File.Exists(dbName))
            {
                SQLiteConnection.CreateFile(dbName);
            }

            SQLiteConnection conexion = new SQLiteConnection(connectionString);
            conexion.Open();

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
                Huella BLOB,
                Estatus TEXT DEFAULT 'ACTIVO',
                Novedad TEXT DEFAULT 'PRESENTE'
            );
            
            -- ¡NUEVA TABLA PARA EL HISTORIAL DE ACCESOS! --
            CREATE TABLE IF NOT EXISTS Registro_Accesos (
                IdRegistro INTEGER PRIMARY KEY AUTOINCREMENT,
                Matricula TEXT,
                FechaHora DATETIME,
                MensajeAcceso TEXT,
                NovedadMomento TEXT
            );
            
            INSERT OR IGNORE INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES ('admin', '1234', 'ADMIN');
            INSERT OR IGNORE INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES ('guardia', '1234', 'GUARDIA');
            ";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, conexion))
            {
                cmd.ExecuteNonQuery();
            }

            // Parches para BD existentes
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN Estatus TEXT DEFAULT 'ACTIVO'", conexion)) { cmd.ExecuteNonQuery(); } } catch { }
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN Novedad TEXT DEFAULT 'PRESENTE'", conexion)) { cmd.ExecuteNonQuery(); } } catch { }

            return conexion;
        }
    }
}