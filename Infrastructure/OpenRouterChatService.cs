using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagBackend.Infrastructure.Interfaces;

namespace RagBackend.Infrastructure
{
    public class OpenRouterChatService : IOpenRouterChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly ILogger<OpenRouterChatService> _logger;

        /// <summary>
        /// Create the chat service and set up the HTTP client.
        /// </summary>
        public OpenRouterChatService(IConfiguration config, ILogger<OpenRouterChatService> logger)
        {
            _logger = logger;
            _apiKey = config["OpenRouter:ApiKey"];   


            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/")
            };

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }

            _logger.LogInformation("OpenRouterChatService initialised with base URL {BaseUrl}",
                _httpClient.BaseAddress);
        }

        /// <summary>
        /// Ensure the API key is present before calling OpenRouter.
        /// </summary>
        private void EnsureConfig()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("OpenRouter API key is missing or empty.");
                throw new Exception("OpenRouter API key is missing or empty.");
            }
        }

        // This implements the method required by IOpenRouterChatService
        public async Task<string> GetAnswerAsync(string prompt)
        {
            EnsureConfig(); // validate here, not in constructor

            var requestBody = new
            {
                model = "meta-llama/Llama-3.1-8B-instruct",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP error calling OpenRouter chat endpoint.");
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenRouter chat error: {StatusCode} - {Body}", response.StatusCode, body);
                throw new Exception($"OpenRouter chat error: {response.StatusCode} - {body}");
            }

            var json = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var content = root
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return content ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to parse chat JSON response from OpenRouter. Raw body: {Body}",
                    json);
                throw;
            }
        }
    }
}