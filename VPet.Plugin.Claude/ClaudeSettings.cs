using System;
using System.IO;
using Newtonsoft.Json;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.Claude
{
    /// <summary>
    /// Stores all configuration settings for the Claude AI plugin.
    /// Settings are persisted as a JSON file alongside the plugin.
    /// </summary>
    public class ClaudeSettings
    {
        /// <summary>Your Anthropic API key (starts with sk-ant-)</summary>
        public string ApiKey { get; set; } = "";

        /// <summary>Claude model to use (e.g. claude-sonnet-4-20250514)</summary>
        public string Model { get; set; } = "claude-sonnet-4-6";

        /// <summary>Custom API endpoint URL. Leave empty for default Anthropic API.</summary>
        public string ApiUrl { get; set; } = "";

        /// <summary>System prompt that defines the pet's personality.</summary>
        public string SystemPrompt { get; set; } = "";

        /// <summary>Maximum tokens for the response.</summary>
        public int MaxTokens { get; set; } = 1024;

        /// <summary>Maximum conversation history messages to keep.</summary>
        public int MaxHistoryMessages { get; set; } = 20;

        /// <summary>Whether to use streaming for responses.</summary>
        public bool EnableStreaming { get; set; } = true;

        private static string GetSettingsPath(MainPlugin plugin)
        {
            return ExtensionValue.BaseDirectory + $"\\ClaudeSettings{plugin.MW.PrefixSave}.json";
        }

        /// <summary>Save settings to a JSON file.</summary>
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

        /// <summary>Load settings from a JSON file.</summary>
        public static ClaudeSettings Load(MainPlugin plugin)
        {
            try
            {
                string path = GetSettingsPath(plugin);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<ClaudeSettings>(json) ?? new ClaudeSettings();
                }
            }
            catch
            {
                // Return defaults if loading fails
            }

            return new ClaudeSettings();
        }
    }
}
