using System.Windows.Controls;

namespace GlassDFIR.Views
{
    public partial class ToolRunView : UserControl
    {
        public ToolRunView()
        {
            InitializeComponent();
        }

        private void OutputLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }
    }
}
