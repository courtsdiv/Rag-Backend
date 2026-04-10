using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagBackend.Application.Interfaces;
using RagBackend.Domain.Errors;

namespace RagBackend.Infrastructure
{
    public sealed class QdrantService : IQdrantService
    {
        // Constants (collection name + header names)
        private const string CollectionName = "rag_chunks_v12";
        private const string ApiKeyHeaderName = "api-key";

        // Dependencies
        private readonly HttpClient _httpClient;
        private readonly ILogger<QdrantService> _logger;

        // Configuration values (read once in constructor)
        private readonly string _url;
        private readonly string _apiKey;

        // Constructor (reads config, initialises HttpClient)
        public QdrantService(IConfiguration configuration, ILogger<QdrantService> logger)
        {
            _logger = logger;

            // Read config values (validated lazily)
            _url = configuration["Qdrant:Url"] ?? string.Empty;
            _apiKey = configuration["Qdrant:ApiKey"] ?? string.Empty;

            _httpClient = new HttpClient();
        }

        // Internal helper (validates config and applies it to HttpClient)
        private void EnsureConfig()
        {
            if (string.IsNullOrWhiteSpace(_url))
                throw new QdrantConfigException("Qdrant URL is missing or empty.");

            if (!Uri.IsWellFormedUriString(_url, UriKind.Absolute))
                throw new QdrantConfigException($"Invalid Qdrant URL: '{_url}'");

            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new QdrantConfigException("Qdrant API key is missing or empty.");

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(_url);

            if (!_httpClient.DefaultRequestHeaders.Contains(ApiKeyHeaderName))
                _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, _apiKey);
        }

        // Public method (ensures the vector collection exists)
        public async Task EnsureCollectionAsync(int vectorSize)
        {
            EnsureConfig();

            var requestBody = new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = "Cosine"
                }
            };

            var response =
                await _httpClient.PutAsJsonAsync($"/collections/{CollectionName}", requestBody);

            // Qdrant returns 409 if the collection already exists (treat as success)
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 409)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Qdrant EnsureCollection error: {StatusCode} - {Error}",
                    response.StatusCode,
                    error);

                throw new HttpRequestException(
                    $"Qdrant EnsureCollection failed with status {(int)response.StatusCode}.");
            }
        }

        // Public method (stores a chunk and its embedding)
        public async Task UpsertAsync(float[] embedding, string chunk)
        {
            EnsureConfig();

            var pointId = CreateDeterministicId(chunk);

            var point = new
            {
                id = pointId,
                vector = embedding,
                payload = new { text = chunk }
            };

            var body = new { points = new[] { point } };

            var response =
                await _httpClient.PutAsJsonAsync($"/collections/{CollectionName}/points", body);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Qdrant Upsert error: {StatusCode} - {Error}",
                    response.StatusCode,
                    error);

                throw new HttpRequestException(
                    $"Qdrant Upsert failed with status {(int)response.StatusCode}.");
            }
        }

        // Public method (searches for similar chunks using an embedding)
        public async Task<List<string>> SearchAsync(float[] embedding, int limit)
        {
            EnsureConfig();

            var body = new
            {
                vector = embedding,
                limit,
                with_payload = true
            };

            var response =
                await _httpClient.PostAsJsonAsync(
                    $"/collections/{CollectionName}/points/search",
                    body
                );

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Qdrant Search error: {StatusCode} - {Error}",
                    response.StatusCode,
                    json);

                throw new HttpRequestException(
                    $"Qdrant Search failed with status {(int)response.StatusCode}.");
            }

            try
            {
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("result", out var resultElement) ||
                    resultElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Qdrant Search returned no results.");
                    return new List<string>();
                }

                var texts = new List<string>();

                foreach (var item in resultElement.EnumerateArray())
                {
                    if (item.TryGetProperty("payload", out var payloadElement) &&
                        payloadElement.TryGetProperty("text", out var textElement))
                    {
                        var value = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            texts.Add(value);
                    }
                }

                return texts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Qdrant search response.");
                throw;
            }
        }

        // Internal helper (creates a stable ID for deduplicating chunks)
        private static Guid CreateDeterministicId(string text)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));

            var guidBytes = new byte[16];
            Array.Copy(hashBytes, guidBytes, 16);

            return new Guid(guidBytes);
        }
    }
}