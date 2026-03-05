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
        // This HttpClient sends web requests to the OpenRouter service.
        private readonly HttpClient _httpClient;

        // The API key we read from app settings. It is used to authenticate requests.
        private readonly string _apiKey;

        /// <summary>
        /// Set up the service and prepare the web client.
        /// </summary>
        /// <param name="configuration">App settings (must contain OpenRouter:ApiKey).</param>
        public OpenRouterEmbeddingService(IConfiguration configuration)
        {
            // Read the API key from settings. If it is missing, stop and show an error.
            _apiKey = configuration["OpenRouter:ApiKey"]
                     ?? throw new Exception("OpenRouter API key not configured.");
            // Create a client that talks to the OpenRouter API.
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/")
            };

            // Add the API key to the request headers so OpenRouter knows we are allowed to use the API.
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            // These headers are optional. They can help some services, but are not required.
            // _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");
            // _httpClient.DefaultRequestHeaders.Add("X-Title", "RagBackend");
        }

        /// <summary>
        /// Ask OpenRouter for an embedding for the given text.
        /// </summary>
        /// <param name="text">Text to convert to numbers.</param>
        /// <returns>Array of float numbers (the embedding).</returns>
        /// <exception cref="Exception">If the API returns an error response.</exception>
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            // Prepare the request body. We tell the API which model to use and give the text.
            var request = new
            {
                model = "text-embedding-3-small",
                input = text
            };

            // Send the request to the 'embeddings' endpoint. This is a POST request.
            var response = await _httpClient.PostAsJsonAsync("embeddings", request);

            // If the API returned an error status, read the error message and throw.
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenRouter embedding error: {response.StatusCode} - {error}");
            }

            // Read the response body as text (JSON format).
            var json = await response.Content.ReadAsStringAsync();

            // Parse the JSON and find the embedding numbers.
            // The API returns something like: { data: [ { embedding: [0.1, 0.2, ...] } ] }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Convert the JSON array values into float numbers in C#.
            var vector = root
                .GetProperty("data")[0]
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(x => x.GetSingle())
                .ToArray();

            // Return the list of numbers. This is the embedding you can store or compare.
            return vector;
        }
    }
}
