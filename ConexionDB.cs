using System;
using System.Data.SQLite;
using System.IO;

using System;
using System.Data.SQLite;
using System.IO;

namespace prueba1 
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

            // =======================================================
            // TODO TU CÓDIGO DE TABLAS INTACTO ABAJO DE ESTA LÍNEA
            // =======================================================
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
                Huella2 BLOB, 
                Huella3 BLOB, 
                Estatus TEXT DEFAULT 'ACTIVO',
                Novedad TEXT DEFAULT 'PRESENTE'
            );
            
            CREATE TABLE IF NOT EXISTS Registro_Accesos (
                IdRegistro INTEGER PRIMARY KEY AUTOINCREMENT,
                Matricula TEXT,
                FechaHora DATETIME,
                MensajeAcceso TEXT,
                NovedadMomento TEXT
            );

            CREATE TABLE IF NOT EXISTS Historial_Reportes (
                IdReporte INTEGER PRIMARY KEY AUTOINCREMENT,
                FechaGeneracion TEXT,
                Turno TEXT,
                GeneradoPor TEXT,
                RutaArchivo TEXT
            );

             -- =======================================================
             -- TABLA: Catálogo Dinámico de Grados Navales
             -- =======================================================
            CREATE TABLE IF NOT EXISTS Cat_Grados (
                IdGrado INTEGER PRIMARY KEY AUTOINCREMENT,
                NombreGrado TEXT NOT NULL UNIQUE
            );

            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (1, 'OTRO');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (2, 'MARINERO');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (3, 'CABO');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (4, 'TERCER MAESTRE');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (5, 'SEGUNDO MAESTRE');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (6, 'PRIMER MAESTRE');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (7, 'TENIENTE DE CORBETA');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (8, 'TENIENTE DE FRAGATA');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (9, 'TENIENTE DE NAVÍO');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (10, 'CAPITÁN DE CORBETA');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (11, 'CAPITÁN DE FRAGATA');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (12, 'CAPITÁN DE NAVÍO');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (13, 'CONTRALMIRANTE');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (14, 'VICEALMIRANTE');
            INSERT OR IGNORE INTO Cat_Grados (IdGrado, NombreGrado) VALUES (15, 'ALMIRANTE');

            -- =======================================================
            -- TABLA: Catálogo Dinámico de Jefaturas y Organigrama
            -- =======================================================
            CREATE TABLE IF NOT EXISTS Cat_Jefaturas (
                IdJefatura INTEGER PRIMARY KEY AUTOINCREMENT,
                NombreJefatura TEXT NOT NULL UNIQUE,
                IdPadre INTEGER,
                FOREIGN KEY (IdPadre) REFERENCES Cat_Jefaturas(IdJefatura) ON DELETE SET NULL
            );
            
            INSERT OR IGNORE INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES ('admin', '$2a$11$0n.O5K/Z.H1q/gL9bJqN.u8rO/l71I/gZ6J5gXvVzR4A7gX6D5yvW', 'ADMIN');
            INSERT OR IGNORE INTO Usuarios_Sistema (Username, PasswordHash, Rol) VALUES ('guardia', '$2a$11$0n.O5K/Z.H1q/gL9bJqN.u8rO/l71I/gZ6J5gXvVzR4A7gX6D5yvW', 'GUARDIA');

            INSERT OR IGNORE INTO Cat_Jefaturas (IdJefatura, NombreJefatura, IdPadre) VALUES (1, 'COMANDANCIA / DIRECCIÓN', NULL);
            INSERT OR IGNORE INTO Cat_Jefaturas (IdJefatura, NombreJefatura, IdPadre) VALUES (2, 'TALLERES', 1);
            INSERT OR IGNORE INTO Cat_Jefaturas (IdJefatura, NombreJefatura, IdPadre) VALUES (3, 'SERVICIOS', 1);
            INSERT OR IGNORE INTO Cat_Jefaturas (IdJefatura, NombreJefatura, IdPadre) VALUES (4, 'DETALL', 1);
            INSERT OR IGNORE INTO Cat_Jefaturas (IdJefatura, NombreJefatura, IdPadre) VALUES (5, 'COMUNAV', 1);
            ";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, conexion))
            {
                cmd.ExecuteNonQuery();
            }

            // Parches silenciosos para Bases de Datos que ya existen
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN Estatus TEXT DEFAULT 'ACTIVO'", conexion)) { cmd.ExecuteNonQuery(); } } catch { }
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN Novedad TEXT DEFAULT 'PRESENTE'", conexion)) { cmd.ExecuteNonQuery(); } } catch { }
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN Huella2 BLOB", conexion)) { cmd.ExecuteNonQuery(); } } catch { }
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN Huella3 BLOB", conexion)) { cmd.ExecuteNonQuery(); } } catch { }
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN FechaInicioNovedad DATE", conexion)) { cmd.ExecuteNonQuery(); } } catch { }
            try { using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE Personal_Naval ADD COLUMN FechaFinNovedad DATE", conexion)) { cmd.ExecuteNonQuery(); } } catch { }

            return conexion;
        }
    }
}