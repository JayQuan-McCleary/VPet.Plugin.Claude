using System;
using System.IO;
using Newtonsoft.Json;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.Claude
{
    public class LLMSettings
    {
        public LLMProvider Provider { get; set; } = LLMProvider.Anthropic;

        public string ApiKey { get; set; } = "";

        public string Model { get; set; } = "claude-sonnet-4-6";

        public string ApiUrl { get; set; } = "";

        public string SystemPrompt { get; set; } = "";

        public int MaxTokens { get; set; } = 1024;

        public int MaxHistoryMessages { get; set; } = 20;

        public bool EnableStreaming { get; set; } = true;

        private static string GetSettingsPath(MainPlugin plugin)
        {
            return ExtensionValue.BaseDirectory + $"\\LLMSettings{plugin.MW.PrefixSave}.json";
        }

        private static string GetLegacySettingsPath(MainPlugin plugin)
        {
            return ExtensionValue.BaseDirectory + $"\\ClaudeSettings{plugin.MW.PrefixSave}.json";
        }

        public void Save(MainPlugin plugin)
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(GetSettingsPath(plugin), json);
            }
            catch
            {
                // Silently fail if we can't save
            }
        }

        public static LLMSettings Load(MainPlugin plugin)
        {
            try
            {
                // Try new settings file first
                string path = GetSettingsPath(plugin);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<LLMSettings>(json) ?? new LLMSettings();
                }

                // Fall back to legacy ClaudeSettings file (seamless migration)
                string legacyPath = GetLegacySettingsPath(plugin);
                if (File.Exists(legacyPath))
                {
                    string json = File.ReadAllText(legacyPath);
                    // Deserializes fine — missing Provider field defaults to Anthropic
                    return JsonConvert.DeserializeObject<LLMSettings>(json) ?? new LLMSettings();
                }
            }
            catch
            {
                // Return defaults if loading fails
            }

            return new LLMSettings();
        }
    }
}
