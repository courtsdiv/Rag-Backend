using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagBackend.Application.Interfaces;

namespace RagBackend.Infrastructure
{
    public sealed class OpenRouterEmbeddingService : IEmbeddingService
    {
        // Constants (endpoint + default model)
        private const string EmbeddingsPath = "embeddings";
        private const string DefaultModel = "text-embedding-3-small";

        // Dependencies (constructor-injected)
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenRouterEmbeddingService> _logger;

        // Configuration values (read once in constructor)
        private readonly string _embeddingModel;

        // Constructor (sets dependencies + reads config)
        public OpenRouterEmbeddingService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenRouterEmbeddingService> logger)
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

            // Read embedding model from config (fallback to default)
            _embeddingModel = configuration["OpenRouter:EmbeddingModel"] ?? DefaultModel;

            // Optional: default Accept header
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // Public method (generates an embedding vector for text)
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            // Request payload sent to OpenRouter
            var requestBody = new
            {
                model = _embeddingModel,
                input = text
            };

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.PostAsJsonAsync(EmbeddingsPath, requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call OpenRouter embeddings endpoint.");
                throw;
            }

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenRouter embedding error: {StatusCode} - {Body}", response.StatusCode, body);
                throw new HttpRequestException(
                    $"OpenRouter embedding request failed with status code {(int)response.StatusCode}.");
            }

            try
            {
                using var doc = JsonDocument.Parse(body);

                var embeddingArray = doc.RootElement
                    .GetProperty("data")[0]
                    .GetProperty("embedding")
                    .EnumerateArray();

                return embeddingArray.Select(x => x.GetSingle()).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse OpenRouter embedding response.");
                throw new JsonException("Invalid JSON from OpenRouter embeddings endpoint.", ex);
            }
        }
    }
}