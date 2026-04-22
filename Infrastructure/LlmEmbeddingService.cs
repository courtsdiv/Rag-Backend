using System.Net.Http.Headers;
using System.Text.Json;
using RagBackend.Application.Interfaces;
using RagBackend.Domain.Errors;

namespace RagBackend.Infrastructure
{
    /// <summary>
    /// Service responsible for converting text into embedding vectors
    /// by calling an external embedding provider.
    /// 
    /// Embeddings are numeric representations of text that capture meaning.
    /// They are used for semantic search in the vector store.
    /// 
    /// This class lives in the Infrastructure layer because it:
    /// - makes HTTP requests
    /// - depends on external services
    /// - handles low‑level communication details
    /// </summary>
    public sealed class LlmEmbeddingService : IEmbeddingService
    {
        /// <summary>
        /// API path used to request embeddings from the provider.
        /// </summary>
        private const string EmbeddingsPath = "embeddings";

        /// <summary>
        /// Default embedding model used if none is specified in configuration.
        /// </summary>
        private const string DefaultModel = "text-embedding-3-small";

        /// <summary>
        /// HTTP client used to communicate with the embedding provider.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Logger used to record errors and diagnostic information.
        /// </summary>
        private readonly ILogger<LlmEmbeddingService> _logger;

        /// <summary>
        /// Name of the embedding model to use.
        /// This value is read from configuration.
        /// </summary>
        private readonly string _embeddingModel;

        /// <summary>
        /// Constructor.
        /// 
        /// Dependencies are injected to:
        /// - keep this service testable
        /// - allow configuration to be changed without code changes
        /// </summary>
        public LlmEmbeddingService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<LlmEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read the API key from configuration and attach it to all requests
            var apiKey = configuration["OpenRouter:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenRouter API key is missing from configuration.");
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            // Tell the server we expect JSON responses
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // Read the embedding model from configuration
            _embeddingModel =
                configuration["OpenRouter:EmbeddingModel"] ?? DefaultModel;
        }

        /// <summary>
        /// Converts a piece of text into an embedding vector.
        /// 
        /// This method:
        /// 1. Sends the text to the embedding provider
        /// 2. Receives a numeric vector in response
        /// 3. Returns the vector for storage or search
        /// </summary>
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            // Do not attempt to embed empty text
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            try
            {
                // Create the request body expected by the embedding API
                var requestBody = new
                {
                    model = _embeddingModel,
                    input = text
                };

                // Send the request to the embedding endpoint
                var response =
                    await _httpClient.PostAsJsonAsync(
                        EmbeddingsPath,
                        requestBody);

                // Read the raw response body
                var body =
                    await response.Content.ReadAsStringAsync();

                // Handle non‑successful HTTP responses
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Embedding service error: {StatusCode} - {Body}",
                        response.StatusCode,
                        body);

                    // Translate low‑level failure into a domain‑level exception
                    throw new LlmUnavailableException(
                        "The AI model is currently unavailable.");
                }

                // Parse the JSON response to extract the embedding vector
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Defensive validation of the embedding response shape
                if (!root.TryGetProperty("data", out var dataArray) ||
                    dataArray.ValueKind != JsonValueKind.Array ||
                    dataArray.GetArrayLength() == 0)
                {
                    _logger.LogError(
                        "Invalid embedding response from provider: {Body}",
                        body);

                    throw new LlmUnavailableException(
                        "Embedding provider returned an invalid response.");
                }

                // Convert the JSON array into a float array
                return dataArray[0]
                    .GetProperty("embedding")
                    .EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToArray();
            }
            catch (LlmUnavailableException)
            {
                // Allow known infrastructure errors to bubble up unchanged
                throw;
            }
            catch (Exception ex)
            {
                // Log unexpected failures and convert them into a controlled exception
                _logger.LogError(ex, "Embedding service failed.");

                throw new LlmUnavailableException(
                    "Failed to generate embeddings from the embedding provider.");
            }
        }
    }
}