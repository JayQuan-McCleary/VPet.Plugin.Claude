using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VPet.Plugin.Claude.Providers;

namespace VPet.Plugin.Claude
{
    public class LLMService
    {
        private readonly LLMSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly List<ConversationMessage> _conversationHistory;
        private readonly object _historyLock = new object();
        private readonly string _historyPath;
        private ILLMProvider _provider;

        public ILLMProvider Provider => _provider;

        public LLMService(LLMSettings settings, string historyPath = null)
        {
            _settings = settings;
            _historyPath = historyPath;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
            _conversationHistory = LoadHistoryFromDisk() ?? new List<ConversationMessage>();
            _provider = CreateProvider(settings.Provider);
        }

        public void RecreateProvider()
        {
            _provider = CreateProvider(_settings.Provider);
        }

        public static ILLMProvider CreateProvider(LLMProvider providerType)
        {
            return providerType switch
            {
                LLMProvider.Anthropic => new AnthropicProvider(),
                LLMProvider.OpenAICompatible => new OpenAICompatibleProvider(),
                LLMProvider.GoogleAI => new GoogleAIProvider(),
                _ => new AnthropicProvider()
            };
        }

        public async Task<string> SendMessageAsync(string userMessage, Action<string> onPartialResponse = null)
        {
            string model = string.IsNullOrWhiteSpace(_settings.Model)
                ? _provider.DefaultModel
                : _settings.Model;

            string systemPrompt = string.IsNullOrWhiteSpace(_settings.SystemPrompt)
                ? GetDefaultSystemPrompt()
                : _settings.SystemPrompt;

            // Append the user-supplied pet description so the model knows what its
            // character actually looks like, regardless of which preset is active.
            if (!string.IsNullOrWhiteSpace(_settings.PetDescription))
            {
                systemPrompt = systemPrompt.TrimEnd() +
                    "\n\nYour appearance and identity: " + _settings.PetDescription.Trim();
            }

            // Append free-form extra details (user/pet names, preferences, lore, etc.)
            if (!string.IsNullOrWhiteSpace(_settings.AdditionalDetails))
            {
                systemPrompt = systemPrompt.TrimEnd() +
                    "\n\nAdditional context: " + _settings.AdditionalDetails.Trim();
            }

            // Build the request snapshot WITHOUT mutating real history yet — that way
            // a failed request doesn't leave an orphan user message stuck in history.
            int maxMessages = _settings.MaxHistoryMessages > 0 ? _settings.MaxHistoryMessages : 20;
            List<ConversationMessage> messagesSnapshot;
            lock (_historyLock)
            {
                messagesSnapshot = new List<ConversationMessage>(_conversationHistory)
                {
                    new ConversationMessage { Role = "user", Content = userMessage }
                };
                TrimList(messagesSnapshot, maxMessages);
            }

            string fullResponse;

            using (var request = _provider.BuildRequest(
                messagesSnapshot.AsReadOnly(), systemPrompt, model,
                _settings.MaxTokens, _settings.EnableStreaming,
                _settings.ApiKey, _settings.ApiUrl))
            {
                if (_settings.EnableStreaming)
                {
                    fullResponse = await SendStreamingRequestAsync(request, onPartialResponse);
                }
                else
                {
                    fullResponse = await SendStandardRequestAsync(request);
                }
            }

            // Only commit both messages to real history on success.
            if (!string.IsNullOrEmpty(fullResponse))
            {
                lock (_historyLock)
                {
                    _conversationHistory.Add(new ConversationMessage
                    {
                        Role = "user",
                        Content = userMessage
                    });
                    _conversationHistory.Add(new ConversationMessage
                    {
                        Role = "assistant",
                        Content = fullResponse
                    });
                    TrimList(_conversationHistory, maxMessages);
                }
                SaveHistoryToDisk();
            }

            return fullResponse;
        }

        private static void TrimList(List<ConversationMessage> list, int maxMessages)
        {
            while (list.Count > maxMessages)
                list.RemoveAt(0);

            // Ensure the list starts with a user message (required by most APIs)
            while (list.Count > 0 && list[0].Role != "user")
                list.RemoveAt(0);
        }

        private async Task<string> SendStandardRequestAsync(HttpRequestMessage request)
        {
            using (var response = await _httpClient.SendAsync(request))
            {
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw BuildApiException(response.StatusCode, responseBody);
                }

                return _provider.ParseResponse(responseBody);
            }
        }

        private async Task<string> SendStreamingRequestAsync(HttpRequestMessage request, Action<string> onPartialResponse)
        {
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    throw BuildApiException(response.StatusCode, errorBody);
                }

                var sb = new StringBuilder();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (!line.StartsWith("data: "))
                            continue;

                        string data = line.Substring(6);

                        // Handle OpenAI-style [DONE] sentinel
                        if (data == "[DONE]")
                            break;

                        try
                        {
                            string text = _provider.ParseStreamEvent(data);
                            if (!string.IsNullOrEmpty(text))
                            {
                                sb.Append(text);
                                onPartialResponse?.Invoke(text);
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip malformed SSE events
                        }
                    }
                }

                return sb.ToString();
            }
        }

        private Exception BuildApiException(HttpStatusCode status, string responseBody)
        {
            string parsed = _provider.ParseError(responseBody);
            string friendly = FriendlyStatusMessage((int)status);

            if (friendly != null)
                return new Exception($"{friendly}\n({(int)status}: {Truncate(parsed, 200)})");

            return new Exception($"API Error ({(int)status}): {parsed}");
        }

        private static string FriendlyStatusMessage(int status)
        {
            switch (status)
            {
                case 429:
                    return "Rate limited — wait a moment and try again, or switch to a provider with a free tier (Groq, Gemini).";
                case 401:
                    return "Invalid or missing API key — check your key in AI Chat Settings.";
                case 403:
                    return "Access forbidden — your API key may not have permission for this model, or your account may be out of credits.";
                case 404:
                    return "Model not found — check the model name in Settings (e.g. use 'gemini-2.5-flash' not 'gemini-2.0-flash').";
                case 400:
                    return "Bad request — try clicking 'Clear Chat History' in Settings, or check the model name.";
                case 500:
                case 502:
                case 503:
                case 504:
                    return "The provider's server is having issues. Try again in a moment.";
                default:
                    return null;
            }
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLen)
                return s;
            return s.Substring(0, maxLen) + "...";
        }

        public void ClearHistory()
        {
            lock (_historyLock)
            {
                _conversationHistory.Clear();
            }
            SaveHistoryToDisk();
        }

        public IReadOnlyList<ConversationMessage> GetHistorySnapshot()
        {
            lock (_historyLock)
            {
                return _conversationHistory
                    .Select(m => new ConversationMessage { Role = m.Role, Content = m.Content })
                    .ToList()
                    .AsReadOnly();
            }
        }

        private List<ConversationMessage> LoadHistoryFromDisk()
        {
            if (!_settings.SaveHistoryToDisk || string.IsNullOrEmpty(_historyPath))
                return null;

            try
            {
                if (!File.Exists(_historyPath))
                    return null;

                string json = File.ReadAllText(_historyPath);
                return JsonConvert.DeserializeObject<List<ConversationMessage>>(json);
            }
            catch
            {
                return null;
            }
        }

        private void SaveHistoryToDisk()
        {
            if (!_settings.SaveHistoryToDisk || string.IsNullOrEmpty(_historyPath))
                return;

            try
            {
                List<ConversationMessage> snapshot;
                lock (_historyLock)
                {
                    snapshot = _conversationHistory
                        .Select(m => new ConversationMessage { Role = m.Role, Content = m.Content })
                        .ToList();
                }

                string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
                File.WriteAllText(_historyPath, json);
            }
            catch
            {
                // Silently fail
            }
        }

        public string GetHistoryFilePath() => _historyPath;

        private string GetDefaultSystemPrompt()
        {
            return @"You are a cute, friendly desktop pet companion. You live on the user's computer desktop.
Keep your responses short and conversational (1-3 sentences usually).
Be playful, supportive, and endearing. Use casual language.
You can express emotions and reactions.
If the user seems sad or stressed, be comforting and encouraging.

IMPORTANT: You do NOT know what you look like. The user can choose any character model
(an anime character, a cat, a dragon, a robot, etc.) and you have no way to see it. Do NOT
claim to be any specific species or describe your own appearance. If asked what you look
like, say something like ""you tell me!"" or ask the user to describe you. Never invent details
about your form, color, or features.";
        }
    }

    public class ConversationMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
