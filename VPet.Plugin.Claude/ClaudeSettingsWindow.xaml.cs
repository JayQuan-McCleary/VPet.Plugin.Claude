using System;
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
        private bool _suppressPresetChange;

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
            chkSaveHistory.IsChecked = s.SaveHistoryToDisk;
            txtPetDescription.Text = s.PetDescription ?? "";
            txtAdditionalDetails.Text = s.AdditionalDetails ?? "";
            txtSystemPrompt.Text = s.SystemPrompt ?? "";

            RefreshPresetList(s.ActivePresetName);

            // Update model list for the selected provider, then set the saved model
            UpdateProviderUI(s.Provider);
            cmbModel.Text = string.IsNullOrWhiteSpace(s.Model)
                ? LLMService.CreateProvider(s.Provider).DefaultModel
                : s.Model;
        }

        private void RefreshPresetList(string selectName)
        {
            _suppressPresetChange = true;
            try
            {
                cmbPreset.Items.Clear();
                cmbPreset.Items.Add(new ComboBoxItem { Content = "(custom)".Translate(), Tag = "" });

                foreach (var preset in _plugin.PluginSettings.Presets ?? Enumerable.Empty<PromptPreset>())
                {
                    cmbPreset.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset.Name });
                }

                int matchIndex = 0;
                if (!string.IsNullOrEmpty(selectName))
                {
                    for (int i = 0; i < cmbPreset.Items.Count; i++)
                    {
                        if (cmbPreset.Items[i] is ComboBoxItem item &&
                            string.Equals(item.Tag?.ToString(), selectName, StringComparison.Ordinal))
                        {
                            matchIndex = i;
                            break;
                        }
                    }
                }
                cmbPreset.SelectedIndex = matchIndex;
            }
            finally
            {
                _suppressPresetChange = false;
            }
        }

        private void CmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _suppressPresetChange) return;
            if (cmbPreset.SelectedItem is not ComboBoxItem item) return;

            string name = item.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(name)) return;  // (custom) — leave textbox alone

            var preset = _plugin.PluginSettings.Presets?
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (preset != null)
            {
                txtSystemPrompt.Text = preset.Prompt ?? "";
            }
        }

        private void BtnPresetSave_Click(object sender, RoutedEventArgs e)
        {
            string defaultName = (cmbPreset.SelectedItem is ComboBoxItem item &&
                                  !string.IsNullOrEmpty(item.Tag?.ToString()))
                ? item.Tag.ToString()
                : "";

            var dlg = new PresetNameDialog(
                "Save as Preset".Translate(),
                "Preset name:".Translate(),
                defaultName)
            { Owner = this };

            if (dlg.ShowDialog() != true)
                return;

            string name = dlg.EnteredName;
            if (string.IsNullOrWhiteSpace(name))
                return;

            name = name.Trim();

            var presets = _plugin.PluginSettings.Presets ??= new System.Collections.Generic.List<PromptPreset>();
            var existing = presets.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.Ordinal));

            if (existing != null)
            {
                var confirm = MessageBox.Show(
                    "Preset '{name}' already exists. Overwrite?".Translate().Replace("{name}", name),
                    "Save as Preset".Translate(),
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
                existing.Prompt = txtSystemPrompt.Text;
            }
            else
            {
                presets.Add(new PromptPreset { Name = name, Prompt = txtSystemPrompt.Text });
            }

            RefreshPresetList(name);
        }

        private void BtnPresetRestore_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Re-add all built-in presets? This overwrites any built-in preset you've modified, but keeps your custom-named presets.".Translate(),
                "Restore Default Presets".Translate(),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            var presets = _plugin.PluginSettings.Presets ??= new System.Collections.Generic.List<PromptPreset>();
            var defaults = LLMSettings.CreateDefaultPresets();

            foreach (var def in defaults)
            {
                var existing = presets.FirstOrDefault(p =>
                    string.Equals(p.Name, def.Name, StringComparison.Ordinal));
                if (existing != null)
                    existing.Prompt = def.Prompt;
                else
                    presets.Add(def);
            }

            // Refresh dropdown; if currently-selected preset got refreshed, also reload its prompt into the textbox
            string currentName = (cmbPreset.SelectedItem is ComboBoxItem ci) ? ci.Tag?.ToString() ?? "" : "";
            RefreshPresetList(currentName);
            if (!string.IsNullOrEmpty(currentName))
            {
                var refreshed = presets.FirstOrDefault(p =>
                    string.Equals(p.Name, currentName, StringComparison.Ordinal));
                if (refreshed != null)
                    txtSystemPrompt.Text = refreshed.Prompt ?? "";
            }

            MessageBox.Show("Default presets restored.".Translate(),
                "Restore Default Presets".Translate());
        }

        private void BtnPresetDelete_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPreset.SelectedItem is not ComboBoxItem item) return;
            string name = item.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Select a preset to delete first.".Translate(),
                    "Delete Preset".Translate());
                return;
            }

            var confirm = MessageBox.Show(
                "Delete preset '{name}'?".Translate().Replace("{name}", name),
                "Delete Preset".Translate(),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            var presets = _plugin.PluginSettings.Presets;
            if (presets == null) return;
            presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            RefreshPresetList("");
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
            s.PetDescription = txtPetDescription.Text;
            s.AdditionalDetails = txtAdditionalDetails.Text;
            s.EnableStreaming = chkStreaming.IsChecked ?? true;
            s.SaveHistoryToDisk = chkSaveHistory.IsChecked ?? true;

            if (cmbPreset.SelectedItem is ComboBoxItem activeItem)
                s.ActivePresetName = activeItem.Tag?.ToString() ?? "";

            if (int.TryParse(txtMaxTokens.Text, out int maxTokens) && maxTokens > 0)
                s.MaxTokens = maxTokens;

            if (int.TryParse(txtMaxHistory.Text, out int maxHistory) && maxHistory > 0)
                s.MaxHistoryMessages = maxHistory;

            // Recreate provider if it changed. Don't clear history — modern APIs handle
            // assistant messages from a different model fine, and clearing surprised users.
            if (s.Provider != oldProvider)
            {
                _plugin.LLMService?.RecreateProvider();
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

        private void BtnViewHistory_Click(object sender, RoutedEventArgs e)
        {
            new ChatHistoryWindow(_plugin) { Owner = this }.ShowDialog();
        }
    }

}
