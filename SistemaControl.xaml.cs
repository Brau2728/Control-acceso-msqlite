using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace prueba1
{
    public partial class SistemaControl : UserControl
    {
        public SistemaControl()
        {
            InitializeComponent();
        }

        private void BtnRespaldo_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = "Respaldo_SemaforoMarina_" + DateTime.Now.ToString("yyyyMMdd");
            dlg.Filter = "Base de Datos SQLite (.db)|*.db|Archivo de Backup (.bak)|*.bak";
            dlg.Title = "Seleccione dónde guardar la copia de seguridad";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string rutaOrigen = "SemaforoMarina.db";
                    if (File.Exists(rutaOrigen))
                    {
                        File.Copy(rutaOrigen, dlg.FileName, overwrite: true);
                        MessageBox.Show("Copia de seguridad creada correctamente.", "Respaldo Exitoso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No se encontró la base de datos principal para respaldar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo crear el respaldo. Detalle: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnRestaurar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Archivos de Respaldo (.db, .bak)|*.db;*.bak|Todos los archivos (*.*)|*.*";
            dlg.Title = "Seleccione el archivo de respaldo a restaurar";

            if (dlg.ShowDialog() == true)
            {
                if (MessageBox.Show("¡ADVERTENCIA! Esto sobreescribirá la información actual con la del respaldo. ¿Desea continuar?", "Restaurar Sistema", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Sugerimos al recolector de basura liberar recursos para evitar bloqueos del archivo .db
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        File.Copy(dlg.FileName, "SemaforoMarina.db", overwrite: true);
                        MessageBox.Show("El sistema ha sido restaurado exitosamente. La aplicación se cerrará para aplicar los cambios; por favor, vuelva a abrirla.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        Application.Current.Shutdown(); 
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("No se pudo restaurar. Es posible que la base de datos esté en uso activo. Detalle: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}