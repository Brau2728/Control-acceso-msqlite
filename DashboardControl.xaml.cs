using System; // <-- Asegúrate de tener este using arriba
using System.Windows.Controls;
using System.Windows.Input; // <-- Y este también

namespace prueba1
{
    public partial class DashboardControl : UserControl
    {
        // 💡 Creamos una "Alerta" para avisar a la ventana principal
        public event EventHandler<string> TarjetaClickeada;

        public DashboardControl()
        {
            InitializeComponent();
        }

        public void ActualizarDatos(int total, int presentes, int faltas, int novedades)
        {
            txtTotal.Text = total.ToString();
            txtPresentes.Text = presentes.ToString();
            txtFaltas.Text = faltas.ToString();
            txtNovedades.Text = novedades.ToString();

            if (total > 0) pbPresentes.Value = (presentes * 100) / total;
            else pbPresentes.Value = 0;
        }

        // 💡 Cuando le das clic a una tarjeta, dispara la alerta con su TAG
        private void Tarjeta_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border bordeActivo && bordeActivo.Tag != null)
            {
                TarjetaClickeada?.Invoke(this, bordeActivo.Tag.ToString());
            }
        }
    }
}