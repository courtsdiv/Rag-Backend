using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagBackend.Application.Interfaces;

namespace RagBackend.Infrastructure
{
    public sealed class OpenRouterChatService : IChatCompletionService
    {
        // Constants (endpoint + default model)
        private const string ChatCompletionsPath = "chat/completions";
        private const string DefaultModel = "meta-llama/llama-3.1-8b-instruct";

        // Dependencies (constructor-injected)
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenRouterChatService> _logger;

        // Configuration values (read once in constructor)
        private readonly string _chatModel;

        // Constructor (sets dependencies + reads config)
        public OpenRouterChatService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenRouterChatService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read API key from config (used for Authorization header)
            var apiKey = configuration["OpenRouter:ApiKey"];

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);
            }

            // Read chat model from config (fallback to default)
            _chatModel = configuration["OpenRouter:ChatModel"] ?? DefaultModel;
        }

        // Public method (sends a prompt to the LLM and returns the answer)
        public async Task<string> GetAnswerAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return string.Empty;

            // Request payload sent to OpenRouter
            var requestBody = new
            {
                model = _chatModel,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.PostAsJsonAsync(ChatCompletionsPath, requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call OpenRouter chat endpoint.");
                throw;
            }

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenRouter chat error: {StatusCode} - {Body}", response.StatusCode, body);

                throw new HttpRequestException(
                    $"OpenRouter chat request failed with status code {(int)response.StatusCode}.");
            }

            try
            {
                using var doc = JsonDocument.Parse(body);

                // Expected shape: { choices: [ { message: { content: "..." } } ] }
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    return string.Empty;

                var firstChoice = choices[0];

                if (!firstChoice.TryGetProperty("message", out var message))
                    return string.Empty;

                if (!message.TryGetProperty("content", out var contentElement))
                    return string.Empty;

                return contentElement.GetString() ?? string.Empty;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse OpenRouter chat response.");
                throw;
            }
        }
    }
}