using LinePutScript.Localization.WPF;
using System;
using System.Threading.Tasks;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.Claude
{
    public class ClaudeTalkBox : TalkBox
    {
        private readonly ClaudePlugin _plugin;

        public ClaudeTalkBox(ClaudePlugin plugin) : base(plugin)
        {
            _plugin = plugin;
        }

        // Stable name so VPet's chat interface selection doesn't drift when the user switches provider.
        public override string APIName => "AI Chat";

        public override void Responded(string content)
        {
            if (string.IsNullOrEmpty(content))
                return;

            DisplayThink();

            if (string.IsNullOrWhiteSpace(_plugin.PluginSettings.ApiKey))
            {
                DisplayThinkToSayRnd("Please set your API key in AI Chat Settings first!".Translate());
                return;
            }

            Dispatcher.Invoke(() => this.IsEnabled = false);

            if (_plugin.PluginSettings.EnableStreaming)
            {
                var sis = new SayInfoWithStream();
                DisplayThinkToSayRnd(sis);

                Task.Run(async () =>
                {
                    try
                    {
                        await _plugin.LLMService.SendMessageAsync(content, (delta) =>
                        {
                            sis.UpdateText(delta);
                        });
                        sis.FinishGenerate();
                    }
                    catch (Exception ex)
                    {
                        sis.UpdateAllText("API call failed".Translate() + "\n" + ex.Message);
                        sis.FinishGenerate();
                    }
                    finally
                    {
                        Dispatcher.Invoke(() => this.IsEnabled = true);
                    }
                });
            }
            else
            {
                Task.Run(async () =>
                {
                    try
                    {
                        string response = await _plugin.LLMService.SendMessageAsync(content);
                        if (!string.IsNullOrEmpty(response))
                            DisplayThinkToSayRnd(response);
                        else
                            DisplayThinkToSayRnd("(No response)".Translate());
                    }
                    catch (Exception ex)
                    {
                        DisplayThinkToSayRnd("API call failed".Translate() + "\n" + ex.Message);
                    }
                    finally
                    {
                        Dispatcher.Invoke(() => this.IsEnabled = true);
                    }
                });
            }
        }

        public override void Setting()
        {
            _plugin.Setting();
        }
    }
}
