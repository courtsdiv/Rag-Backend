
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RagBackend.Application.Interfaces;
using RagBackend.Domain.Errors;

namespace RagBackend.Infrastructure
{
    /// <summary>
    /// Service responsible for communicating with the vector store.
    /// 
    /// A vector store is used to:
    /// - store embeddings (numeric representations of text)
    /// - search those embeddings to find relevant document content
    /// 
    /// This class:
    /// - makes HTTP requests to the vector database
    /// - handles request/response formatting
    /// - translates low‑level failures into domain‑level exceptions
    /// 
    /// It lives in the Infrastructure layer because it depends on
    /// external services and low‑level networking.
    /// </summary>
    public sealed class VectorStoreService : IVectorStore
    {
        /// <summary>
        /// Name of the collection used to store document embeddings.
        /// </summary>
        private const string CollectionName = "rag_chunks_v12";

        /// <summary>
        /// Name of the HTTP header used to send the API key.
        /// </summary>
        private const string ApiKeyHeaderName = "api-key";

        /// <summary>
        /// HTTP client used to communicate with the vector store API.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Logger used to record errors and diagnostic information.
        /// </summary>
        private readonly ILogger<VectorStoreService> _logger;

        /// <summary>
        /// Base URL of the vector store service.
        /// This value is read from configuration.
        /// </summary>
        private readonly string _url;

        /// <summary>
        /// API key used to authenticate requests to the vector store.
        /// </summary>
        private readonly string _apiKey;

        /// <summary>
        /// Constructor.
        /// 
        /// Reads configuration values and prepares the HTTP client.
        /// </summary>
        public VectorStoreService(
            IConfiguration configuration,
            ILogger<VectorStoreService> logger)
        {
            _logger = logger;

            // Read vector store connection details from configuration
            _url = configuration["Qdrant:Url"] ?? string.Empty;
            _apiKey = configuration["Qdrant:ApiKey"] ?? string.Empty;

            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Validates configuration and applies it to the HTTP client.
        /// 
        /// This method ensures:
        /// - the URL is present and valid
        /// - the API key is present
        /// - the HTTP client is correctly configured
        /// </summary>
        private void EnsureVectorStoreConfig()
        {
            if (string.IsNullOrWhiteSpace(_url))
                throw new VectorStoreUnavailableException(
                    "Vector store URL is missing or empty.");

            if (!Uri.IsWellFormedUriString(_url, UriKind.Absolute))
                throw new VectorStoreUnavailableException(
                    $"Invalid vector store URL: '{_url}'");

            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new VectorStoreUnavailableException(
                    "Vector store API key is missing or empty.");

            // Apply base address once
            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(_url);

            // Apply API key header once
            if (!_httpClient.DefaultRequestHeaders.Contains(ApiKeyHeaderName))
                _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, _apiKey);
        }

        /// <summary>
        /// Ensures that the vector collection exists.
        /// 
        /// This is called before inserting or searching data to guarantee
        /// the collection is correctly configured.
        /// </summary>
        public async Task EnsureCollectionAsync(int vectorSize)
        {
            EnsureVectorStoreConfig();

            try
            {
                var requestBody = new
                {
                    vectors = new
                    {
                        size = vectorSize,
                        distance = "Cosine"
                    }
                };

                var response =
                    await _httpClient.PutAsJsonAsync(
                        $"/collections/{CollectionName}",
                        requestBody);

                // Status code 409 means the collection already exists
                if (!response.IsSuccessStatusCode &&
                    (int)response.StatusCode != 409)
                {
                    var error =
                        await response.Content.ReadAsStringAsync();

                    _logger.LogError(
                        "Vector store EnsureCollection error: {StatusCode} - {Error}",
                        response.StatusCode,
                        error);

                    throw new VectorStoreUnavailableException(
                        $"Vector store EnsureCollection failed with status {(int)response.StatusCode}.");
                }
            }
            catch (VectorStoreUnavailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector store EnsureCollection request failed.");
                throw new VectorStoreUnavailableException(
                    "Vector store EnsureCollection request failed.");
            }
        }

        /// <summary>
        /// Stores a text chunk and its embedding in the vector store.
        /// </summary>
        public async Task UpsertAsync(float[] embedding, string chunk)
        {
            EnsureVectorStoreConfig();

            try
            {
                // Create a deterministic ID so duplicate chunks are not stored twice
                var pointId = CreateDeterministicId(chunk);

                var requestBody = new
                {
                    points = new[]
                    {
                        new
                        {
                            id = pointId,
                            vector = embedding,
                            payload = new { text = chunk }
                        }
                    }
                };

                var response =
                    await _httpClient.PutAsJsonAsync(
                        $"/collections/{CollectionName}/points",
                        requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var error =
                        await response.Content.ReadAsStringAsync();

                    _logger.LogError(
                        "Vector store Upsert error: {StatusCode} - {Error}",
                        response.StatusCode,
                        error);

                    throw new VectorStoreUnavailableException(
                        $"Vector store Upsert failed with status {(int)response.StatusCode}.");
                }
            }
            catch (VectorStoreUnavailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector store Upsert request failed.");
                throw new VectorStoreUnavailableException(
                    "Vector store Upsert request failed.");
            }
        }

        /// <summary>
        /// Searches the vector store for text chunks similar to a query embedding.
        /// </summary>
        public async Task<List<string>> SearchAsync(float[] embedding, int limit)
        {
            EnsureVectorStoreConfig();

            try
            {
                var requestBody = new
                {
                    vector = embedding,
                    limit,
                    with_payload = true
                };

                var response =
                    await _httpClient.PostAsJsonAsync(
                        $"/collections/{CollectionName}/points/search",
                        requestBody);

                var json =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Vector store Search error: {StatusCode} - {Error}",
                        response.StatusCode,
                        json);

                    throw new VectorStoreUnavailableException(
                        $"Vector store Search failed with status {(int)response.StatusCode}.");
                }

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("result", out var resultElement) ||
                    resultElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Vector store Search returned no results.");
                    return new List<string>();
                }

                var results = new List<string>();

                foreach (var item in resultElement.EnumerateArray())
                {
                    if (item.TryGetProperty("payload", out var payload) &&
                        payload.TryGetProperty("text", out var textElement))
                    {
                        var text = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            results.Add(text);
                    }
                }

                return results;
            }
            catch (VectorStoreUnavailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector store Search request failed.");
                throw new VectorStoreUnavailableException(
                    "Vector store Search request failed.");
            }
        }

        /// <summary>
        /// Creates a deterministic GUID based on the text content.
        /// 
        /// This ensures that identical chunks always produce the same ID,
        /// preventing duplicate entries in the vector store.
        /// </summary>
        private static Guid CreateDeterministicId(string text)
        {
            var hashBytes =
                SHA256.HashData(Encoding.UTF8.GetBytes(text));

            var guidBytes = new byte[16];
            Array.Copy(hashBytes, guidBytes, 16);

            return new Guid(guidBytes);
        }
    }
}