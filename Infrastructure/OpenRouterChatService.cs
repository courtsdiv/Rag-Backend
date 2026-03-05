using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RagBackend.Infrastructure
{
    /// <summary>
    /// Service for calling OpenRouter's chat API to get a text answer.
    /// </summary>
    /// <remarks>
    /// Sends a prompt to a chat model and returns the model's reply as plain text.
    /// </remarks>
    public class OpenRouterChatService
    {
        private const string BaseUrl = "https://openrouter.ai/api/v1/";
        private const string ModelName = "meta-llama/llama-3.1-8b-instruct";

        // HttpClient used to send requests to the OpenRouter API.
        private readonly HttpClient _httpClient;

        // API key read from configuration.
        private readonly string _apiKey;

        /// <summary>
        /// Create the chat service and set up the HTTP client.
        /// </summary>
        /// <param name="config">App configuration containing the OpenRouter API key.</param>
        public OpenRouterChatService(IConfiguration config)
        {
            // Read the API key from configuration. Throw a clear error if it's missing.
            _apiKey = config["OpenRouter:ApiKey"]
                      ?? throw new Exception("OpenRouter API key missing.");

            // Create an HttpClient with the OpenRouter API base address.
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };

            // Add the Authorization header so OpenRouter accepts our requests.
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        /// <summary>
        /// Send a prompt to the chat model and return the model's reply text.
        /// </summary>
        /// <param name="prompt">The text prompt to send to the chat model.</param>
        /// <returns>Reply text from the model, or an empty string if missing.</returns>
        /// <exception cref="Exception">Thrown when the API returns an error response.</exception>
        public async Task<string> GetAnswerAsync(string prompt)
        {
            // Prepare the request body the API expects: model + messages array.
            var body = new
            {
                model = ModelName,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            // Send the request to the chat completions endpoint.
            var response = await _httpClient.PostAsJsonAsync("chat/completions", body);

            // If the API call failed, read the body and throw an exception with details.
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenRouter chat error: {response.StatusCode} - {err}");
            }

            // Read the JSON response as text.
            var json = await response.Content.ReadAsStringAsync();

            // Parse the JSON and pull out the content of the first choice's message.
            // Expected structure: { "choices": [ { "message": { "content": "..." } } ] }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var first = choices[0];

            if (!first.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var contentElement))
            {
                return string.Empty;
            }

            var answer = contentElement.GetString();

            // Return the answer, or empty string if null.
            return answer ?? string.Empty;
        }
    }
}
