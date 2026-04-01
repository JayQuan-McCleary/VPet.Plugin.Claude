using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VPet.Plugin.Claude.Providers
{
    public class GoogleAIProvider : ILLMProvider
    {
        public string DisplayName => "Gemini";
        public string DefaultApiUrl => "https://generativelanguage.googleapis.com/v1beta/models";
        public string DefaultModel => "gemini-2.5-flash";

        public IReadOnlyList<string> SuggestedModels { get; } = new[]
        {
            "gemini-2.5-flash",
            "gemini-2.5-pro",
            "gemini-2.0-flash-lite"
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
            string baseUrl = string.IsNullOrWhiteSpace(apiUrl) ? DefaultApiUrl : apiUrl;

            // Google uses different endpoints for streaming vs non-streaming
            // URL format: {baseUrl}/{model}:generateContent?key={apiKey}
            //         or: {baseUrl}/{model}:streamGenerateContent?alt=sse&key={apiKey}
            string endpoint = streaming
                ? $"{baseUrl}/{model}:streamGenerateContent?alt=sse&key={apiKey}"
                : $"{baseUrl}/{model}:generateContent?key={apiKey}";

            // Convert messages: Anthropic/OpenAI "assistant" role -> Google "model" role
            var contents = messages.Select(m => new
            {
                role = m.Role == "assistant" ? "model" : m.Role,
                parts = new[] { new { text = m.Content } }
            }).ToArray();

            // Build request body
            var body = new JObject();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                body["system_instruction"] = new JObject
                {
                    ["parts"] = new JArray { new JObject { ["text"] = systemPrompt } }
                };
            }

            body["contents"] = JArray.FromObject(contents);
            body["generationConfig"] = new JObject
            {
                ["maxOutputTokens"] = maxTokens > 0 ? maxTokens : 1024
            };

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(
                body.ToString(Formatting.None), Encoding.UTF8, "application/json");

            // Google AI uses API key in the URL query param, no auth header needed
            return request;
        }

        public string ParseResponse(string responseBody)
        {
            var result = JObject.Parse(responseBody);
            return result["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                ?? string.Empty;
        }

        public string ParseStreamEvent(string eventData)
        {
            var obj = JObject.Parse(eventData);

            // Check for error in stream
            if (obj["error"] != null)
            {
                string errorMsg = obj["error"]?["message"]?.ToString() ?? "Unknown streaming error";
                throw new Exception($"Stream Error: {errorMsg}");
            }

            return obj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
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
