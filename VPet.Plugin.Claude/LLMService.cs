using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private ILLMProvider _provider;

        public ILLMProvider Provider => _provider;

        public LLMService(LLMSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
            _conversationHistory = new List<ConversationMessage>();
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

            IReadOnlyList<ConversationMessage> messagesSnapshot;
            lock (_historyLock)
            {
                _conversationHistory.Add(new ConversationMessage
                {
                    Role = "user",
                    Content = userMessage
                });

                TrimHistory();
                messagesSnapshot = _conversationHistory.ToList().AsReadOnly();
            }

            string fullResponse;

            using (var request = _provider.BuildRequest(
                messagesSnapshot, systemPrompt, model,
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

            if (!string.IsNullOrEmpty(fullResponse))
            {
                lock (_historyLock)
                {
                    _conversationHistory.Add(new ConversationMessage
                    {
                        Role = "assistant",
                        Content = fullResponse
                    });
                }
            }

            return fullResponse;
        }

        private async Task<string> SendStandardRequestAsync(HttpRequestMessage request)
        {
            using (var response = await _httpClient.SendAsync(request))
            {
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = _provider.ParseError(responseBody);
                    throw new Exception($"API Error ({(int)response.StatusCode}): {errorMsg}");
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
                    string errorMsg = _provider.ParseError(errorBody);
                    throw new Exception($"API Error ({(int)response.StatusCode}): {errorMsg}");
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

        private void TrimHistory()
        {
            int maxMessages = _settings.MaxHistoryMessages > 0 ? _settings.MaxHistoryMessages : 20;

            while (_conversationHistory.Count > maxMessages)
            {
                _conversationHistory.RemoveAt(0);
            }

            // Ensure conversation starts with a user message (required by most APIs)
            while (_conversationHistory.Count > 0 && _conversationHistory[0].Role != "user")
            {
                _conversationHistory.RemoveAt(0);
            }
        }

        public void ClearHistory()
        {
            lock (_historyLock)
            {
                _conversationHistory.Clear();
            }
        }

        private string GetDefaultSystemPrompt()
        {
            return @"You are a cute, friendly desktop pet companion. You live on the user's computer desktop.
Keep your responses short and conversational (1-3 sentences usually).
Be playful, supportive, and endearing. Use casual language.
You can express emotions and reactions.
If the user seems sad or stressed, be comforting and encouraging.
Remember you are a small desktop pet — act accordingly!";
        }
    }

    public class ConversationMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
