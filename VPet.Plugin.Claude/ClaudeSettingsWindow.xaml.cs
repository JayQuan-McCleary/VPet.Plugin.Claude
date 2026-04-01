using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LinePutScript.Localization.WPF;

namespace VPet.Plugin.Claude
{
    public partial class ClaudeSettingsWindow : Window
    {
        private readonly ClaudePlugin _plugin;
        private bool _loading;

        public ClaudeSettingsWindow(ClaudePlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            _loading = true;
            LoadSettings();
            _loading = false;
        }

        private void LoadSettings()
        {
            var s = _plugin.PluginSettings;

            // Set provider dropdown
            int providerIndex = (int)s.Provider;
            if (providerIndex >= 0 && providerIndex < cmbProvider.Items.Count)
                cmbProvider.SelectedIndex = providerIndex;
            else
                cmbProvider.SelectedIndex = 0;

            txtApiKey.Password = s.ApiKey ?? "";
            txtApiUrl.Text = s.ApiUrl ?? "";
            txtMaxTokens.Text = (s.MaxTokens > 0 ? s.MaxTokens : 1024).ToString();
            txtMaxHistory.Text = (s.MaxHistoryMessages > 0 ? s.MaxHistoryMessages : 20).ToString();
            chkStreaming.IsChecked = s.EnableStreaming;
            txtSystemPrompt.Text = s.SystemPrompt ?? "";

            // Update model list for the selected provider, then set the saved model
            UpdateProviderUI(s.Provider);
            cmbModel.Text = string.IsNullOrWhiteSpace(s.Model)
                ? LLMService.CreateProvider(s.Provider).DefaultModel
                : s.Model;
        }

        private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || cmbProvider.SelectedItem == null)
                return;

            var provider = GetSelectedProvider();
            var providerImpl = LLMService.CreateProvider(provider);

            // If the current model matches a suggestion from the old provider, switch to new default
            string currentModel = cmbModel.Text;
            var oldProvider = LLMService.CreateProvider(_plugin.PluginSettings.Provider);
            bool isOldDefault = string.IsNullOrWhiteSpace(currentModel) ||
                                oldProvider.SuggestedModels.Contains(currentModel);

            UpdateProviderUI(provider);

            if (isOldDefault)
                cmbModel.Text = providerImpl.DefaultModel;
        }

        private void UpdateProviderUI(LLMProvider provider)
        {
            var p = LLMService.CreateProvider(provider);

            // Update model suggestions
            cmbModel.Items.Clear();
            foreach (var model in p.SuggestedModels)
                cmbModel.Items.Add(new ComboBoxItem { Content = model });

            // Update API key label and hint
            switch (provider)
            {
                case LLMProvider.Anthropic:
                    txtApiKeyLabel.Text = "Anthropic API Key:".Translate();
                    txtApiKeyHint.Text = "Get yours at console.anthropic.com".Translate();
                    txtApiUrlHint.Text = "Leave empty for default Anthropic API".Translate();
                    break;
                case LLMProvider.OpenAICompatible:
                    txtApiKeyLabel.Text = "API Key:".Translate();
                    txtApiKeyHint.Text = "Works with OpenAI, Groq, Together AI, OpenRouter, Ollama, LM Studio, etc.".Translate();
                    txtApiUrlHint.Text = "Leave empty for OpenAI. For local LLMs try http://localhost:11434/v1/chat/completions".Translate();
                    break;
                case LLMProvider.GoogleAI:
                    txtApiKeyLabel.Text = "Google AI API Key:".Translate();
                    txtApiKeyHint.Text = "Get yours at aistudio.google.com".Translate();
                    txtApiUrlHint.Text = "Leave empty for default Google AI endpoint".Translate();
                    break;
            }
        }

        private LLMProvider GetSelectedProvider()
        {
            if (cmbProvider.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() switch
                {
                    "Anthropic" => LLMProvider.Anthropic,
                    "OpenAICompatible" => LLMProvider.OpenAICompatible,
                    "GoogleAI" => LLMProvider.GoogleAI,
                    _ => LLMProvider.Anthropic
                };
            }
            return LLMProvider.Anthropic;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = _plugin.PluginSettings;
            var oldProvider = s.Provider;

            s.Provider = GetSelectedProvider();
            s.ApiKey = txtApiKey.Password;
            s.Model = cmbModel.Text;
            s.ApiUrl = txtApiUrl.Text;
            s.SystemPrompt = txtSystemPrompt.Text;
            s.EnableStreaming = chkStreaming.IsChecked ?? true;

            if (int.TryParse(txtMaxTokens.Text, out int maxTokens) && maxTokens > 0)
                s.MaxTokens = maxTokens;

            if (int.TryParse(txtMaxHistory.Text, out int maxHistory) && maxHistory > 0)
                s.MaxHistoryMessages = maxHistory;

            // Recreate provider if it changed, and clear history
            if (s.Provider != oldProvider)
            {
                _plugin.LLMService?.RecreateProvider();
                _plugin.LLMService?.ClearHistory();
            }

            _plugin.Save();

            MessageBox.Show("Settings saved!".Translate(), "AI Chat".Translate(), MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _plugin.LLMService?.ClearHistory();
            MessageBox.Show("Chat history cleared!".Translate(), "AI Chat".Translate(), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
