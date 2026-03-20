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

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<OpenRouterChatService> _logger;

        /// <summary>
        /// Create the chat service and set up the HTTP client.
        /// </summary>
        /// <param name="config">App configuration containing the OpenRouter API key.</param>
        public OpenRouterChatService(IConfiguration config, ILogger<OpenRouterChatService> logger)
        {
            _logger = logger;

            _apiKey = config["OpenRouter:ApiKey"]
                      ?? throw new Exception("OpenRouter API key missing.");

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            _logger.LogInformation(
                "OpenRouterChatService initialised with base URL {BaseUrl} and model {ModelName}",
                BaseUrl, ModelName);
        }

        /// <summary>
        /// Send a prompt to the chat model and return the model's reply text.
        /// </summary>
        /// <param name="prompt">The text prompt to send to the chat model.</param>
        /// <returns>Reply text from the model, or an empty string if missing.</returns>
        /// <exception cref="Exception">Thrown when the API returns an error response.</exception>
        public async Task<string> GetAnswerAsync(string prompt)
        {
            _logger.LogInformation(
                "LLM chat request started. Prompt length = {Length} characters",
                prompt?.Length ?? 0);

            var body = new
            {
                model = ModelName,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.PostAsJsonAsync("chat/completions", body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP error calling OpenRouter chat/completions endpoint.");
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "OpenRouter chat error. StatusCode={StatusCode}, Body={Body}",
                    response.StatusCode,
                    err);

                throw new Exception($"OpenRouter chat error: {response.StatusCode} - {err}");
            }

            var json = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("choices", out var choices) ||
                    choices.GetArrayLength() == 0)
                {
                    _logger.LogWarning("OpenRouter chat response contained no choices.");
                    return string.Empty;
                }

                var first = choices[0];

                if (!first.TryGetProperty("message", out var message) ||
                    !message.TryGetProperty("content", out var contentElement))
                {
                    _logger.LogWarning("OpenRouter chat response missing message.content field.");
                    return string.Empty;
                }

                var answer = contentElement.GetString() ?? string.Empty;

                _logger.LogInformation("LLM chat request completed successfully. Answer length = {Length}",
                    answer.Length);

                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to parse OpenRouter chat JSON response. Raw body: {Body}",
                    json);
                throw;
            }
        }
    }
}