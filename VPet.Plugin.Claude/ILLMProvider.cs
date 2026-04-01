using System.Collections.Generic;
using System.Net.Http;

namespace VPet.Plugin.Claude
{
    public interface ILLMProvider
    {
        string DisplayName { get; }
        string DefaultApiUrl { get; }
        string DefaultModel { get; }
        IReadOnlyList<string> SuggestedModels { get; }

        HttpRequestMessage BuildRequest(
            IReadOnlyList<ConversationMessage> messages,
            string systemPrompt,
            string model,
            int maxTokens,
            bool streaming,
            string apiKey,
            string apiUrl);

        string ParseResponse(string responseBody);

        /// <summary>
        /// Parse a single SSE data payload during streaming.
        /// Returns the text delta, or null if this event is not a text chunk.
        /// </summary>
        string ParseStreamEvent(string eventData);

        string ParseError(string responseBody);
    }
}
