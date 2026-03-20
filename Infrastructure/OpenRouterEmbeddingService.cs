using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;

namespace RagBackend.Infrastructure
{
    /// <summary>
    /// Gets an embedding (a list of numbers) for a piece of text from OpenRouter.
    /// </summary>
    /// <remarks>
    /// An embedding is a list of numbers that tries to capture the meaning of text.
    /// We send the text to OpenRouter and return the numbers it gives us.
    /// </remarks>
    public class OpenRouterEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<OpenRouterEmbeddingService> _logger;

        /// <summary>
        /// Set up the service and prepare the web client.
        /// </summary>
        /// <param name="configuration">App settings (must contain OpenRouter:ApiKey).</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public OpenRouterEmbeddingService(IConfiguration configuration, ILogger<OpenRouterEmbeddingService> logger)
        {
            _logger = logger;

            _apiKey = configuration["OpenRouter:ApiKey"]
                      ?? throw new Exception("OpenRouter API key not configured.");

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/")
            };

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            _logger.LogInformation("OpenRouterEmbeddingService initialised with base URL {BaseUrl}",
                _httpClient.BaseAddress);
        }

        /// <summary>
        /// Ask OpenRouter for an embedding for the given text.
        /// </summary>
        /// <param name="text">Text to convert to numbers.</param>
        /// <returns>Array of float numbers (the embedding).</returns>
        /// <exception cref="Exception">If the API returns an error response or invalid JSON.</exception>
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            _logger.LogInformation(
                "Embedding request started. Text length = {Length} characters",
                text?.Length ?? 0);

            var request = new
            {
                model = "text-embedding-3-small",
                input = text
            };

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.PostAsJsonAsync("embeddings", request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP error calling OpenRouter embeddings endpoint.");
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "OpenRouter embedding error. StatusCode={StatusCode}, Body={Body}",
                    response.StatusCode,
                    error);

                throw new Exception($"OpenRouter embedding error: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var vector = root
                    .GetProperty("data")[0]
                    .GetProperty("embedding")
                    .EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToArray();

                _logger.LogInformation(
                    "Embedding request completed successfully. Vector length = {Length}",
                    vector.Length);

                return vector;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to parse embedding JSON response from OpenRouter. Raw body: {Body}",
                    json);
                throw;
            }
        }
    }
}