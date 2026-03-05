using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RagBackend.Infrastructure
{
    /// <summary>
    /// Client to talk to Qdrant (a vector database).
    /// </summary>
    /// <remarks>
    /// This class does three simple things: make sure a collection exists,
    /// save a vector with some text, and search for similar vectors to get their texts. 
    /// </remarks>
    public class QdrantService
    {
        // HTTP client used to call the Qdrant API.
        private readonly HttpClient _httpClient;

        // The collection name where we store text chunks and vectors.
        private const string CollectionName = "rag_chunks";

        /// <summary>
        /// Create a new QdrantService.
        /// </summary>
        /// <param name="configuration">App settings with Qdrant URL and API key.</param>
        /// <exception cref="Exception">Thrown when URL or API key are missing.</exception>
        public QdrantService(IConfiguration configuration)
        {
            // Get the Qdrant base URL from configuration.
            var url = configuration["Qdrant:Url"]
                ?? throw new Exception("Qdrant URL not configured.");

            // Get the Qdrant API key from configuration.
            var apiKey = configuration["Qdrant:ApiKey"]
                ?? throw new Exception("Qdrant API key not configured.");

            // Create an HttpClient with the Qdrant base address.
            // Example base URL: "https://YOUR-CLUSTER-URL/".
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(url)
            };

            // Add the API key to the request headers so Qdrant accepts our requests.
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        /// <summary>
        /// Make sure the collection exists in Qdrant with the given vector size.
        /// If it already exists, nothing bad happens.
        /// </summary>
        /// <param name="vectorSize">How many numbers each vector has.</param>
        /// <returns>Task that completes when the request is done.</returns>
        /// <exception cref="Exception">Thrown on unexpected Qdrant error.</exception>
        public async Task EnsureCollectionAsync(int vectorSize)
        {
            // Request body telling Qdrant how big vectors are and what distance to use.
            var requestBody = new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = "Cosine"
                }
            };

            // Send a PUT request to create or update the collection.
            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{CollectionName}",
                requestBody
            );

            // Qdrant returns 200/201 for success and 409 if the collection already exists.
            // Anything else is treated as an error.
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 409)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Qdrant EnsureCollection error: {response.StatusCode} - {error}");
            }
        }

        /// <summary>
        /// Save or update one point: a vector and its text.
        /// The ID is made from the text so saving the same text again replaces the old one.
        /// </summary>
        /// <param name="vector">The vector numbers to store.</param>
        /// <param name="text">The text to store with the vector.</param>
        /// <returns>Task that completes when the upsert finishes.</returns>
        /// <exception cref="Exception">Thrown on Qdrant error.</exception>
        public async Task UpsertAsync(float[] vector, string text)
        {
            // Make a stable id from the text: hash the text and use first 16 bytes as a GUID.
            // This way the same text gives the same id and will be updated instead of duplicated.
            var hashBytes = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(text)
            );

            var guidBytes = new byte[16];
            Array.Copy(hashBytes, guidBytes, 16); // copy first 16 bytes into GUID array
            var pointId = new Guid(guidBytes);

            // Create the point object Qdrant expects: id, vector and payload with the text.
            var point = new
            {
                id = pointId,  // Qdrant accepts Guid as a UUID value
                vector = vector,
                payload = new
                {
                    text = text
                }
            };

            var body = new
            {
                points = new[] { point }
            };

            // Send the upsert request to Qdrant to store the point.
            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{CollectionName}/points",
                body
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Qdrant Upsert error: {response.StatusCode} - {error}");
            }
        }

        /// <summary>
        /// Find vectors similar to the query vector and return their stored texts.
        /// </summary>
        /// <param name="queryVector">The vector to search with.</param>
        /// <param name="limit">How many results to return (default 3).</param>
        /// <returns>List of texts from matching points.</returns>
        /// <exception cref="Exception">Thrown on Qdrant error.</exception>
        public async Task<List<string>> SearchAsync(float[] queryVector, int limit = 3)

        {
            // Build a search request and ask Qdrant to include payloads (our text).
            var body = new
            {
                vector = queryVector,
                limit = limit,
                with_payload = true // ask Qdrant to include stored payloads (our text)
            };

            // Send the search request to Qdrant.
            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{CollectionName}/points/search",
                body
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Qdrant Search error: {response.StatusCode} - {error}");
            }

            // Read the raw JSON response from Qdrant.
            var json = await response.Content.ReadAsStringAsync();

            // Parse the JSON and get the "result" array if it exists.
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array)
            {
                // If we don't get a result array, return an empty list.
                return new List<string>();
            }

            var texts = new List<string>();

            // Go through each item in the result and pull out payload.text when present.
            foreach (var item in resultElement.EnumerateArray())
            {
                if (item.TryGetProperty("payload", out var payloadElement) &&
                    payloadElement.TryGetProperty("text", out var textElement))
                {
                    var value = textElement.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        texts.Add(value);
                    }
                }
            }

            return texts;
        }
    }
}