using System.Windows;
using VPet_Simulator.Windows.Interface;
using LinePutScript.Localization.WPF;

namespace VPet.Plugin.Claude
{
    public class ClaudePlugin : MainPlugin
    {
        public LLMService LLMService { get; private set; }
        public LLMSettings PluginSettings { get; private set; }

        public override string PluginName => "ClaudeAI";

        public ClaudePlugin(IMainWindow mainwin) : base(mainwin)
        {
        }

        public override void LoadPlugin()
        {
            PluginSettings = LLMSettings.Load(this);

            LLMService = new LLMService(PluginSettings, LLMSettings.GetHistoryPath(this));

            MW.TalkAPI.Add(new ClaudeTalkBox(this));

            var menuItem = new System.Windows.Controls.MenuItem()
            {
                Header = "AI Chat Settings".Translate(),
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
