using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VPet.Plugin.Claude.Providers
{
    public class AnthropicProvider : ILLMProvider
    {
        private const string ApiVersion = "2023-06-01";

        public string DisplayName => "Claude";
        public string DefaultApiUrl => "https://api.anthropic.com/v1/messages";
        public string DefaultModel => "claude-sonnet-4-6";

        public IReadOnlyList<string> SuggestedModels { get; } = new[]
        {
            "claude-sonnet-4-6",
            "claude-haiku-4-5-20251001",
            "claude-opus-4-6"
        };

        public HttpRequestMessage BuildRequest(
            IReadOnlyList<ConversationMessage> messages,
            string systemPrompt,
            string model,
            int maxTokens,
            bool streaming,
            string apiKey,
            string apiUrl)
        {
            string url = string.IsNullOrWhiteSpace(apiUrl) ? DefaultApiUrl : apiUrl;

            var body = new
            {
                model = model,
                max_tokens = maxTokens > 0 ? maxTokens : 1024,
                system = systemPrompt,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = streaming
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(
                JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", ApiVersion);

            return request;
        }

        public string ParseResponse(string responseBody)
        {
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

        public string ParseStreamEvent(string eventData)
        {
            var obj = JObject.Parse(eventData);
            string eventType = obj["type"]?.ToString();

            if (eventType == "content_block_delta")
            {
                var delta = obj["delta"];
                if (delta?["type"]?.ToString() == "text_delta")
                {
                    return delta["text"]?.ToString();
                }
            }
            else if (eventType == "error")
            {
                string errorMsg = obj["error"]?["message"]?.ToString() ?? "Unknown streaming error";
                throw new System.Exception($"Stream Error: {errorMsg}");
            }

            return null;
        }

        public string ParseError(string responseBody)
        {
            try
            {
                var error = JObject.Parse(responseBody);
                return error["error"]?["message"]?.ToString() ?? responseBody;
            }
            catch (JsonException)
            {
                return responseBody;
            }
        }
    }
}
