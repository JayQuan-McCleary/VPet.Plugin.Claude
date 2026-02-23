using System.Windows;

namespace VPet.Plugin.Claude
{
    public partial class ClaudeSettingsWindow : Window
    {
        private readonly ClaudePlugin _plugin;

        public ClaudeSettingsWindow(ClaudePlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = _plugin.PluginSettings;

            txtApiKey.Password = s.ApiKey ?? "";
            cmbModel.Text = string.IsNullOrWhiteSpace(s.Model) ? "claude-sonnet-4-6" : s.Model;
            txtApiUrl.Text = s.ApiUrl ?? "";
            txtMaxTokens.Text = (s.MaxTokens > 0 ? s.MaxTokens : 1024).ToString();
            txtMaxHistory.Text = (s.MaxHistoryMessages > 0 ? s.MaxHistoryMessages : 20).ToString();
            chkStreaming.IsChecked = s.EnableStreaming;
            txtSystemPrompt.Text = s.SystemPrompt ?? "";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = _plugin.PluginSettings;

            s.ApiKey = txtApiKey.Password;
            s.Model = cmbModel.Text;
            s.ApiUrl = txtApiUrl.Text;
            s.SystemPrompt = txtSystemPrompt.Text;
            s.EnableStreaming = chkStreaming.IsChecked ?? true;

            if (int.TryParse(txtMaxTokens.Text, out int maxTokens) && maxTokens > 0)
                s.MaxTokens = maxTokens;

            if (int.TryParse(txtMaxHistory.Text, out int maxHistory) && maxHistory > 0)
                s.MaxHistoryMessages = maxHistory;

            // Re-create the service with updated settings
            _plugin.ClaudeService?.ClearHistory();

            // Save to disk
            _plugin.Save();

            MessageBox.Show("Settings saved!", "Claude AI", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ClaudeService?.ClearHistory();
            MessageBox.Show("Chat history cleared!", "Claude AI", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
