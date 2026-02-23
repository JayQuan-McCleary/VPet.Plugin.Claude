using System.Windows;
using VPet_Simulator.Windows.Interface;
using LinePutScript.Localization.WPF;

namespace VPet.Plugin.Claude
{
    /// <summary>
    /// VPet plugin that integrates Anthropic's Claude AI as the pet's chat backend.
    /// </summary>
    public class ClaudePlugin : MainPlugin
    {
        public ClaudeService ClaudeService { get; private set; }
        public ClaudeSettings PluginSettings { get; private set; }

        public override string PluginName => "ClaudeAI";

        public ClaudePlugin(IMainWindow mainwin) : base(mainwin)
        {
        }

        public override void LoadPlugin()
        {
            // Load saved settings
            PluginSettings = ClaudeSettings.Load(this);

            // Initialize the Claude API service
            ClaudeService = new ClaudeService(PluginSettings);

            // Register the TalkBox so VPet can use Claude for chat
            MW.TalkAPI.Add(new ClaudeTalkBox(this));

            // Add settings menu item
            var menuItem = new System.Windows.Controls.MenuItem()
            {
                Header = "Claude AI Settings".Translate(),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            menuItem.Click += (s, e) => { Setting(); };
            MW.Main.ToolBar.MenuMODConfig.Items.Add(menuItem);
        }

        public override void Setting()
        {
            new ClaudeSettingsWindow(this).ShowDialog();
        }

        public override void Save()
        {
            PluginSettings.Save(this);
        }
    }
}
