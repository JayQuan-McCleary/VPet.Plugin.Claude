using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VPet.Plugin.Claude
{
    /// <summary>
    /// Service that communicates with Anthropic's Claude Messages API.
    /// Supports both standard and streaming responses.
    /// </summary>
    public class ClaudeService
    {
        private readonly ClaudeSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly List<ConversationMessage> _conversationHistory;
        private readonly object _historyLock = new object();

        private const string DefaultApiUrl = "https://api.anthropic.com/v1/messages";
        private const string DefaultModel = "claude-sonnet-4-6";
        private const string ApiVersion = "2023-06-01";

        public ClaudeService(ClaudeSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
            _conversationHistory = new List<ConversationMessage>();
        }

        /// <summary>
        /// Send a message to Claude and get a response.
        /// </summary>
        /// <param name="userMessage">The user's message</param>
        /// <param name="onPartialResponse">Callback for each streaming text delta (can be null)</param>
        /// <returns>The complete response text</returns>
        public async Task<string> SendMessageAsync(string userMessage, Action<string> onPartialResponse = null)
        {
            string apiUrl = string.IsNullOrWhiteSpace(_settings.ApiUrl) ? DefaultApiUrl : _settings.ApiUrl;
            string model = string.IsNullOrWhiteSpace(_settings.Model) ? DefaultModel : _settings.Model;

            object requestBody;
            lock (_historyLock)
            {
                _conversationHistory.Add(new ConversationMessage
                {
                    Role = "user",
                    Content = userMessage
                });

                TrimHistory();

                // Snapshot messages while holding lock
                requestBody = new
                {
                    model = model,
                    max_tokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : 1024,
                    system = string.IsNullOrWhiteSpace(_settings.SystemPrompt)
                        ? GetDefaultSystemPrompt()
                        : _settings.SystemPrompt,
                    messages = _conversationHistory.Select(m => new
                    {
                        role = m.Role,
                        content = m.Content
                    }).ToArray(),
                    stream = _settings.EnableStreaming
                };
            }

            string json = JsonConvert.SerializeObject(requestBody);

            string fullResponse;

            using (var request = new HttpRequestMessage(HttpMethod.Post, apiUrl))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Headers.Add("x-api-key", _settings.ApiKey);
                request.Headers.Add("anthropic-version", ApiVersion);

                if (_settings.EnableStreaming)
                {
                    fullResponse = await SendStreamingRequestAsync(request, onPartialResponse);
                }
                else
                {
                    fullResponse = await SendStandardRequestAsync(request);
                }
            }

            // Add assistant response to history
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
                    string errorMsg = responseBody;
                    try
                    {
                        var error = JObject.Parse(responseBody);
                        errorMsg = error["error"]?["message"]?.ToString() ?? responseBody;
                    }
                    catch (JsonException) { }
                    throw new Exception($"API Error ({(int)response.StatusCode}): {errorMsg}");
                }

                var result = JObject.Parse(responseBody);
                var contentBlocks = result["content"] as JArray;

                if (contentBlocks == null || contentBlocks.Count == 0)
                    return string.Empty;

                var textParts = contentBlocks
                    .Where(b => b["type"]?.ToString() == "text")
                    .Select(b => b["text"]?.ToString())
                    .Where(t => !string.IsNullOrEmpty(t));

                return string.Join("\n", textParts);
            }
        }

        private async Task<string> SendStreamingRequestAsync(HttpRequestMessage request, Action<string> onPartialResponse)
        {
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    string errorMsg = errorBody;
                    try
                    {
                        var error = JObject.Parse(errorBody);
                        errorMsg = error["error"]?["message"]?.ToString() ?? errorBody;
                    }
                    catch (JsonException) { }
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

                        try
                        {
                            var eventData = JObject.Parse(data);
                            string eventType = eventData["type"]?.ToString();

                            if (eventType == "content_block_delta")
                            {
                                var delta = eventData["delta"];
                                if (delta?["type"]?.ToString() == "text_delta")
                                {
                                    string text = delta["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        sb.Append(text);
                                        onPartialResponse?.Invoke(text);
                                    }
                                }
                            }
                            else if (eventType == "error")
                            {
                                string errorMsg = eventData["error"]?["message"]?.ToString() ?? "Unknown streaming error";
                                throw new Exception($"Stream Error: {errorMsg}");
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

        /// <summary>
        /// Keep conversation history within a reasonable size.
        /// </summary>
        private void TrimHistory()
        {
            int maxMessages = _settings.MaxHistoryMessages > 0 ? _settings.MaxHistoryMessages : 20;

            while (_conversationHistory.Count > maxMessages)
            {
                _conversationHistory.RemoveAt(0);
            }

            // Ensure conversation starts with a user message (required by Anthropic API)
            while (_conversationHistory.Count > 0 && _conversationHistory[0].Role != "user")
            {
                _conversationHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Clear all conversation history.
        /// </summary>
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

    /// <summary>
    /// Represents a single message in the conversation history.
    /// </summary>
    public class ConversationMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
