using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VPet.Plugin.Claude.Providers
{
    public class OpenAICompatibleProvider : ILLMProvider
    {
        public string DisplayName => "ChatGPT";
        public string DefaultApiUrl => "https://api.openai.com/v1/chat/completions";
        public string DefaultModel => "gpt-4o-mini";

        public IReadOnlyList<string> SuggestedModels { get; } = new[]
        {
            "gpt-4o-mini",
            "gpt-4o",
            "gpt-4.1-mini",
            "gpt-4.1"
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

            // Build messages array with system prompt as first message
            var apiMessages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                apiMessages.Add(new { role = "system", content = systemPrompt });
            }

            apiMessages.AddRange(messages.Select(m => (object)new { role = m.Role, content = m.Content }));

            var body = new
            {
                model = model,
                max_tokens = maxTokens > 0 ? maxTokens : 1024,
                messages = apiMessages.ToArray(),
                stream = streaming
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(
                JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            return request;
        }

        public string ParseResponse(string responseBody)
        {
            var result = JObject.Parse(responseBody);
            return result["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;
        }

        public string ParseStreamEvent(string eventData)
        {
            // OpenAI sends "data: [DONE]" as the final event — caller should check before calling this
            if (eventData == "[DONE]")
                return null;

            var obj = JObject.Parse(eventData);
            return obj["choices"]?[0]?["delta"]?["content"]?.ToString();
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
