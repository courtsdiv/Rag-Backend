using RagBackend.Domain.Errors;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RagBackend.Infrastructure.Interfaces;

namespace RagBackend.Infrastructure
{
    public class QdrantService : IQdrantService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _url;
        private readonly string? _apiKey;
        private readonly ILogger<QdrantService> _logger;

        private const string CollectionName = "rag_chunks_v12";

        public QdrantService(IConfiguration configuration, ILogger<QdrantService> logger)
        {
            // Store config values, but DO NOT validate here
            _url = configuration["Qdrant:Url"];
            _apiKey = configuration["Qdrant:ApiKey"]; 

            _logger = logger;
            _httpClient = new HttpClient();
        }

        // Centralised safe config validation
        private void EnsureConfig()
        {
            if (string.IsNullOrWhiteSpace(_url))
            {
                _logger.LogError("Qdrant URL missing or empty.");
                throw new QdrantConfigException("Qdrant URL is missing or empty.");
            }

            if (!Uri.IsWellFormedUriString(_url, UriKind.Absolute))
            {
                _logger.LogError("Qdrant URL invalid: {Url}", _url);
                throw new QdrantConfigException($"Invalid Qdrant URL: '{_url}'");
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("Qdrant API key missing or empty.");
                throw new QdrantConfigException("Qdrant API key is missing or empty.");
            }
            // Configure HttpClient only when config is valid
            if (_httpClient.BaseAddress == null)
            {
                _logger.LogInformation("Configuring HttpClient BaseAddress for Qdrant.");
                _httpClient.BaseAddress = new Uri(_url);
            }

            if (!_httpClient.DefaultRequestHeaders.Contains("api-key"))
            {
                _logger.LogInformation("Adding Qdrant API key header.");
                _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            }
        }

        public async Task EnsureCollectionAsync(int vectorSize)
        {
            _logger.LogInformation("Qdrant EnsureCollectionAsync started.");
            EnsureConfig(); // validate here!

            var requestBody = new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = "Cosine"
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{CollectionName}",
                requestBody
            );

            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 409)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Qdrant EnsureCollection error: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Qdrant EnsureCollection error: {response.StatusCode} - {error}");
            }
            _logger.LogInformation("Qdrant EnsureCollectionAsync completed successfully.");
        }

        public async Task UpsertAsync(float[] vector, string text)
        {
            EnsureConfig(); // validate here!

            var hashBytes = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(text)
            );

            var guidBytes = new byte[16];
            Array.Copy(hashBytes, guidBytes, 16);

            var pointId = new Guid(guidBytes);

            var point = new
            {
                id = pointId,
                vector = vector,
                payload = new { text }
            };

            var body = new { points = new[] { point } };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{CollectionName}/points",
                body
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Qdrant Upsert error: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Qdrant Upsert error: {response.StatusCode} - {error}");
            }
            _logger.LogInformation("Qdrant UpsertAsync completed successfully.");
        }

        public async Task<List<string>> SearchAsync(float[] queryVector, int limit = 3)
        {
            _logger.LogInformation("Qdrant SearchAsync started with limit {Limit}.", limit);
            EnsureConfig(); // validate here!

            var body = new
            {
                vector = queryVector,
                limit = limit,
                with_payload = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{CollectionName}/points/search",
                body
            );
            _logger.LogInformation("Qdrant SearchAsync received response with status code {StatusCode}.", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Qdrant Search error: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Qdrant Search error: {response.StatusCode} - {error}");
            }
            _logger.LogInformation("Qdrant SearchAsync response successful, processing results.");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Qdrant Search returned no results");
                return new List<string>();
            }

            var texts = new List<string>();

            foreach (var item in resultElement.EnumerateArray())
            {
                if (item.TryGetProperty("payload", out var payloadElement) &&
                    payloadElement.TryGetProperty("text", out var textElement))
                {
                    var value = textElement.GetString();
                    if (!string.IsNullOrEmpty(value))
                        texts.Add(value);
                }
            }

            _logger.LogInformation("Qdrant SearchAsync completed with {Count} results.", texts.Count);

            return texts;
        }
    }
}