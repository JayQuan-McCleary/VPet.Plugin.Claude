using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using LinePutScript.Localization.WPF;
using Microsoft.Win32;

namespace VPet.Plugin.Claude
{
    public partial class ChatHistoryWindow : Window
    {
        private readonly ClaudePlugin _plugin;
        private readonly List<HistoryRow> _rows;

        public ChatHistoryWindow(ClaudePlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            _rows = BuildRows();

            string path = _plugin.LLMService?.GetHistoryFilePath();
            txtHistoryPath.Text = string.IsNullOrEmpty(path)
                ? "History is in-memory only (disable/enable persistence in Settings).".Translate()
                : ("Saved to: ".Translate() + path);

            lstMessages.ItemsSource = _rows;

            if (_rows.Count == 0)
            {
                lstMessages.ItemsSource = new[]
                {
                    new HistoryRow
                    {
                        RoleDisplay = "(empty)".Translate(),
                        Content = "No messages yet — chat with your pet first!".Translate(),
                        BubbleColor = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
                        RoleColor = Brushes.Gray
                    }
                };
            }

            // Scroll to bottom on open so most recent messages are visible
            Loaded += (s, e) => scrollViewer.ScrollToEnd();
        }

        private List<HistoryRow> BuildRows()
        {
            var snapshot = _plugin.LLMService?.GetHistorySnapshot();
            if (snapshot == null)
                return new List<HistoryRow>();

            return snapshot.Select(m =>
            {
                bool isUser = string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase);
                return new HistoryRow
                {
                    RoleDisplay = isUser ? "You".Translate() : "Pet".Translate(),
                    Content = m.Content ?? "",
                    BubbleColor = isUser
                        ? new SolidColorBrush(Color.FromRgb(0xE8, 0xF1, 0xF8))
                        : new SolidColorBrush(Color.FromRgb(0xFA, 0xF5, 0xEC)),
                    RoleColor = isUser
                        ? new SolidColorBrush(Color.FromRgb(0x3F, 0x7F, 0xA0))
                        : new SolidColorBrush(Color.FromRgb(0xB0, 0x70, 0x20))
                };
            }).ToList();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var snapshot = _plugin.LLMService?.GetHistorySnapshot();
            if (snapshot == null || snapshot.Count == 0)
            {
                MessageBox.Show("No messages to export.".Translate(), "Chat History".Translate());
                return;
            }

            var dlg = new SaveFileDialog
            {
                FileName = $"chat-history-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Chat history exported {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(new string('-', 60));
                foreach (var m in snapshot)
                {
                    string role = string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)
                        ? "You" : "Pet";
                    sb.AppendLine($"[{role}]");
                    sb.AppendLine(m.Content);
                    sb.AppendLine();
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Exported.".Translate(), "Chat History".Translate());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed: ".Translate() + ex.Message,
                    "Chat History".Translate(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = _plugin.LLMService?.GetHistoryFilePath();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("No history file (persistence is disabled).".Translate(),
                    "Chat History".Translate());
                return;
            }

            try
            {
                string folder = Path.GetDirectoryName(path);
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    Process.Start("explorer.exe", folder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open folder: ".Translate() + ex.Message,
                    "Chat History".Translate(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private class HistoryRow
        {
            public string RoleDisplay { get; set; }
            public string Content { get; set; }
            public Brush BubbleColor { get; set; }
            public Brush RoleColor { get; set; }
        }
    }
}
