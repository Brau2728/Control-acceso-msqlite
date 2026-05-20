using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace prueba1
{
    // Modelo de datos local estructurado para el árbol jerárquico
    public class JefaturaNodo
    {
        public int IdJefatura { get; set; }
        public string NombreJefatura { get; set; } = string.Empty;
        public int? IdPadre { get; set; }
        public List<JefaturaNodo> Subramas { get; set; } = new List<JefaturaNodo>();
    }

    public partial class GestionJefaturasControl : UserControl
    {
        private int _idAreaSeleccionada = 0;
        private List<JefaturaNodo> _listaPlanaGlobal = new List<JefaturaNodo>();

        public GestionJefaturasControl()
        {
            InitializeComponent();
            RefrescarModulo();
        }

        private void RefrescarModulo()
        {
            CargarDatosDesdeBD();
            ConstruirOrganigrama();
            CargarComboPadres();
            BtnLimpiar_Click(null, null);
        }

        // 1. Descarga los datos de SQLite a una lista plana en memoria
        private void CargarDatosDesdeBD()
        {
            _listaPlanaGlobal.Clear();
            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    string query = "SELECT IdJefatura, NombreJefatura, IdPadre FROM Cat_Jefaturas ORDER BY NombreJefatura ASC";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _listaPlanaGlobal.Add(new JefaturaNodo
                            {
                                IdJefatura = Convert.ToInt32(reader["IdJefatura"]),
                                NombreJefatura = reader["NombreJefatura"].ToString(),
                                IdPadre = reader["IdPadre"] != DBNull.Value ? Convert.ToInt32(reader["IdPadre"]) : (int?)null
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al leer base de datos: " + ex.Message); }
        }

        // 2. Transforma la lista plana en un árbol jerárquico recursivo
        private void ConstruirOrganigrama()
        {
            // Limpiamos los enlaces previos de subramas para reconstruir limpiamente
            foreach (var item in _listaPlanaGlobal) item.Subramas.Clear();

            var diccionario = _listaPlanaGlobal.ToDictionary(x => x.IdJefatura);
            var nodosRaiz = new List<JefaturaNodo>();

            foreach (var area in _listaPlanaGlobal)
            {
                if (area.IdPadre == null)
                {
                    nodosRaiz.Add(area); // Es una jefatura de nivel superior
                }
                else if (diccionario.TryGetValue(area.IdPadre.Value, out var padre))
                {
                    padre.Subramas.Add(area); // Se anexa como hijo de su rama correspondiente
                }
            }

            tvOrganigrama.ItemsSource = null;
            tvOrganigrama.ItemsSource = nodosRaiz;
        }

        // 3. Llena el ComboBox eliminando el área seleccionada (para evitar que un área dependa de sí misma)
        private void CargarComboPadres()
        {
            var listaCombo = new List<JefaturaNodo>();
            // Opción por defecto para áreas raíz (Principales)
            listaCombo.Add(new JefaturaNodo { IdJefatura = 0, NombreJefatura = "[ NINGUNA - ÁREA PRINCIPAL ]" });

            foreach (var area in _listaPlanaGlobal)
            {
                if (area.IdJefatura != _idAreaSeleccionada)
                {
                    listaCombo.Add(area);
                }
            }

            cmbAreaPadre.ItemsSource = null;
            cmbAreaPadre.ItemsSource = listaCombo;
            cmbAreaPadre.SelectedIndex = 0;
        }

        // 4. Cuando el usuario da clic a un elemento del árbol, se carga en el formulario
        private void TvOrganigrama_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (tvOrganigrama.SelectedItem is JefaturaNodo nodo)
            {
                _idAreaSeleccionada = nodo.IdJefatura;
                txtNombreArea.Text = nodo.NombreJefatura;
                
                lblTituloForm.Text = "✏️ Editar / Mover Área";
                btnEliminar.Visibility = Visibility.Visible;

                CargarComboPadres(); // Recarga el combo para bloquear que sea su propio padre
                cmbAreaPadre.SelectedValue = nodo.IdPadre ?? 0;
            }
        }

        // 5. Botón de Guardar (Soporta Altas y Modificaciones)
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombreArea.Text))
            {
                MessageBox.Show("El nombre del área es obligatorio.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                {
                    SQLiteCommand cmd = new SQLiteCommand();
                    cmd.Connection = conexion;

                    int? idPadreSeleccionado = Convert.ToInt32(cmbAreaPadre.SelectedValue);
                    if (idPadreSeleccionado == 0) idPadreSeleccionado = null; // Nivel raíz

                    if (_idAreaSeleccionada == 0) // NUEVA ÁREA
                    {
                        cmd.CommandText = "INSERT INTO Cat_Jefaturas (NombreJefatura, IdPadre) VALUES (@nombre, @idPadre)";
                    }
                    else // MODIFICACIÓN OPERATIVA
                    {
                        cmd.CommandText = "UPDATE Cat_Jefaturas SET NombreJefatura = @nombre, IdPadre = @idPadre WHERE IdJefatura = @id";
                        cmd.Parameters.AddWithValue("@id", _idAreaSeleccionada);
                    }

                    cmd.Parameters.AddWithValue("@nombre", txtNombreArea.Text.Trim().ToUpper());
                    cmd.Parameters.AddWithValue("@idPadre", (object)idPadreSeleccionado ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Estructura del organigrama actualizada con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    RefrescarModulo();
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al guardar: " + ex.Message); }
        }

        // 6. Botón de Eliminar
        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_idAreaSeleccionada == 0) return;

            // Validación de seguridad elemental: Verificar si tiene subramas hijas primero
            var tieneHijos = _listaPlanaGlobal.Any(x => x.IdPadre == _idAreaSeleccionada);
            if (tieneHijos)
            {
                MessageBox.Show("No se puede eliminar esta área porque contiene subramas dependientes. Mueve o elimina primero las subramas hijas.", "Acción Bloqueada", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show("¿Está seguro de eliminar esta área del sistema permanente?", "Confirmar Baja", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (SQLiteConnection conexion = ConexionDB.ObtenerConexion())
                    {
                        string query = "DELETE FROM Cat_Jefaturas WHERE IdJefatura = @id";
                        using (SQLiteCommand cmd = new SQLiteCommand(query, conexion))
                        {
                            cmd.Parameters.AddWithValue("@id", _idAreaSeleccionada);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    RefrescarModulo();
                }
                catch (Exception ex) { MessageBox.Show("Error al eliminar: " + ex.Message); }
            }
        }

        // 7. Limpia el formulario para crear un registro nuevo
        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            _idAreaSeleccionada = 0;
            txtNombreArea.Text = "";
            lblTituloForm.Text = "➕ Crear Nueva Área / Rama";
            btnEliminar.Visibility = Visibility.Collapsed;
            if (cmbAreaPadre.Items.Count > 0) cmbAreaPadre.SelectedIndex = 0;
        }
    }
}