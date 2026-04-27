using System.Windows;
using System.Windows.Input;

namespace VPet.Plugin.Claude
{
    public partial class PresetNameDialog : Window
    {
        public string EnteredName { get; private set; }

        public PresetNameDialog(string title, string label, string defaultValue)
        {
            InitializeComponent();
            this.Title = title ?? "Save as Preset";
            txtLabel.Text = label ?? "Preset name:";
            txtInput.Text = defaultValue ?? "";

            Loaded += (s, e) =>
            {
                txtInput.Focus();
                txtInput.SelectAll();
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            EnteredName = txtInput.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            EnteredName = null;
            this.DialogResult = false;
            this.Close();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnOk_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
