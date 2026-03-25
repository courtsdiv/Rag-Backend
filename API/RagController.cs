using Microsoft.AspNetCore.Mvc;
using RagBackend.Domain.Models;
using RagBackend.Domain.Errors;
using RagBackend.Infrastructure;
using RagBackend.Infrastructure.Interfaces;
using RagBackend.Domain.Utils;

// Small update for PR workflow testing - ignore this change

namespace RagBackend.API
{
    /// <summary>
    /// Simple controller for RAG.
    /// Has three endpoints:
    /// - index: save text into Qdrant
    /// - search: find similar texts
    /// - answer: use retrieved context to answer a question
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RagController : ControllerBase
    {
        // Service that turns text into embedding vectors.
        private readonly IOpenRouterEmbeddingService _embeddingService;

        // Service that stores and finds vectors (Qdrant client).
        private readonly IQdrantService _qdrantService;

        // Service that calls a chat model to produce answers.
        private readonly IOpenRouterChatService _chatService;

        // The size of embeddings produced by the embedding model.
        private const int EmbeddingSize = 1536;

        private readonly int _topK;

        private readonly ILogger<RagController> _logger;

        public RagController(
            IOpenRouterEmbeddingService embeddingService,
            IQdrantService qdrantService,
            IOpenRouterChatService chatService,
            IConfiguration config,
            ILogger<RagController> logger)
        {
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _chatService = chatService;
            _logger = logger;

            // Default to 3 if the setting is missing.
            _topK = config.GetValue<int>("Retrieval:TopK", 3);
        }

        /// <summary>
        /// Index a piece of text: get its vector and store it with the text.
        /// </summary>
        [HttpPost("index")]
        public async Task<IActionResult> IndexText([FromBody] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return BadRequest(new ApiError
                {
                    Message = "Text cannot be empty.",
                    ErrorCode = "INVALID_INPUT"
                });
            }

            _logger.LogInformation("Index request received. Original text length = {Length}", text.Length);

            // ---- CLEAN TEXT FIRST ----
            var cleaned = TextCleaner.Clean(text);
            _logger.LogInformation("Cleaned text length = {Length}", cleaned.Length);

            // ---- CHUNK CLEANED TEXT ----
            var chunks = TextChunker.ChunkText(cleaned);
            _logger.LogInformation("Created {Count} cleaned chunks.", chunks.Count);

            // Ensure collection exists
            try
            {
                _logger.LogInformation("Qdrant EnsureCollection started (index)");
                await _qdrantService.EnsureCollectionAsync(EmbeddingSize);
                _logger.LogInformation("Qdrant EnsureCollection completed (index)");
            }
            catch (Exception)
            {
                return ErrorResults.QdrantUnavailable();
            }

            // Embed + upsert each chunk
            foreach (var chunk in chunks)
            {
                float[] vector;

                try
                {
                    _logger.LogInformation("Embedding chunk with length = {Length}", chunk.Length);
                    vector = await _embeddingService.GetEmbeddingAsync(chunk);
                }
                catch (Exception)
                {
                    _logger.LogWarning("Embedding failed for a chunk. Skipping.");
                    continue;
                }

                try
                {
                    _logger.LogInformation("Upserting chunk into Qdrant...");
                    await _qdrantService.UpsertAsync(vector, chunk);
                }
                catch (Exception)
                {
                    _logger.LogWarning("Upsert failed for a chunk. Skipping.");
                    continue;
                }
            }

            return Ok(new { message = "Text indexed successfully." });
        }

        /// <summary>
        /// Search for texts similar to the given query.
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new ApiError
                {
                    Message = "Query cannot be empty.",
                    ErrorCode = "INVALID_INPUT"
                });
            }

            _logger.LogInformation("Search request received. Query: {Query}", query);

            // 1. Ensure Qdrant collection exists
            try
            {
                _logger.LogInformation("Qdrant EnsureCollection started (search)");
                await _qdrantService.EnsureCollectionAsync(EmbeddingSize);
                _logger.LogInformation("Qdrant EnsureCollection completed (search)");
            }
            catch (QdrantConfigException ex)
            {
                _logger.LogWarning("Qdrant configuration error (search): {Message}", ex.Message);
                return ErrorResults.QdrantUnavailable();
            }
            catch (Exception)
            {
                _logger.LogWarning("Qdrant EnsureCollection failed (search). Returning QDRANT_UNAVAILABLE.");
                return ErrorResults.QdrantUnavailable();
            }

            float[] queryVector;

            // 2. Get embedding for the query
            try
            {
                _logger.LogInformation("Embedding request started (search)");
                queryVector = await _embeddingService.GetEmbeddingAsync(query);
                _logger.LogInformation("Embedding request completed (search)");
            }
            catch (Exception)
            {
                _logger.LogWarning("Embedding service failure (search). Returning LLM_UNAVAILABLE.");
                return ErrorResults.LlmUnavailable();
            }

            // 3. Search Qdrant
            List<string> results;
            try
            {
                _logger.LogInformation("Qdrant Search started (search)");
                results = await _qdrantService.SearchAsync(queryVector, limit: _topK);
                _logger.LogInformation("Qdrant Search completed (search) with {Count} results", results.Count);
            }
            catch (Exception)
            {
                _logger.LogWarning("Qdrant search failure (search). Returning QDRANT_UNAVAILABLE.");
                return ErrorResults.QdrantUnavailable();
            }

            _logger.LogInformation("Search request completed successfully.");

            return Ok(results);
        }

        /// <summary>
        /// Answer a question using retrieved text as context.
        /// </summary>
        [HttpPost("answer")]
        public async Task<IActionResult> GetAnswer([FromBody] string question)
        {

            if (string.IsNullOrWhiteSpace(question))
            {
                return BadRequest(new ApiError
                {
                    Message = "Question cannot be empty.",
                    ErrorCode = "INVALID_INPUT"
                });
            }

            _logger.LogInformation("Incoming question: {Question}", question);
            _logger.LogInformation("Using TopK = {TopK}", _topK);

            // 1. Ensure Qdrant collection exists
            try
            {
                _logger.LogInformation("Qdrant EnsureCollection started (answer)");
                await _qdrantService.EnsureCollectionAsync(EmbeddingSize);
                _logger.LogInformation("Qdrant EnsureCollection completed (answer)");
            }
            catch (QdrantConfigException ex)
            {
                _logger.LogWarning("Qdrant configuration error (answer): {Message}", ex.Message);
                return ErrorResults.QdrantUnavailable();
            }
            catch (Exception)
            {
                _logger.LogWarning("Qdrant EnsureCollection failed (answer). Returning QDRANT_UNAVAILABLE.");
                return ErrorResults.QdrantUnavailable();
            }

            float[] queryEmbedding;

            // 2. Get embedding for the question
            try
            {
                _logger.LogInformation("Embedding request started (answer)");
                queryEmbedding = await _embeddingService.GetEmbeddingAsync(question);
                _logger.LogInformation("Embedding request completed (answer)");
            }
            catch (Exception)
            {
                _logger.LogWarning("Embedding service failure (answer). Returning LLM_UNAVAILABLE.");
                return ErrorResults.LlmUnavailable();
            }

            // 3. Search Qdrant
            List<string> topChunks;
            try
            {
                _logger.LogInformation("Qdrant Search started (answer)");
                topChunks = await _qdrantService.SearchAsync(queryEmbedding, limit: _topK);
                _logger.LogInformation("Qdrant Search completed (answer) with {Count} chunks", topChunks.Count);
            }
            catch (Exception)
            {
                _logger.LogWarning("Qdrant search failure (answer). Returning QDRANT_UNAVAILABLE.");
                return ErrorResults.QdrantUnavailable();
            }

            var context = string.Join("\n\n", topChunks).Trim();

            var prompt = $@"
            ## CONTEXT
            ---------------------
            {context}
            ---------------------

            ## QUESTION
            {question}

            ## INSTRUCTIONS
            - Use ONLY the context above when answering.
            - If the answer is not in the context, say:
            'The provided documents do not contain this information.'
            - Keep your answer short and factual.
            ".Trim();

            _logger.LogDebug("Final prompt sent to LLM: {Prompt}", prompt);

            string answer;

            // 4. Ask LLM to answer using the prompt
            try
            {
                _logger.LogInformation("LLM request started (answer)");
                answer = await _chatService.GetAnswerAsync(prompt);
                _logger.LogInformation("LLM request completed (answer)");
            }
            catch (Exception)
            {
                _logger.LogWarning("LLM answer failure (answer). Returning LLM_UNAVAILABLE.");
                return ErrorResults.LlmUnavailable();
            }

            _logger.LogInformation("Answer request completed successfully.");

            // 5. Return success response
            return Ok(new
            {
                question,
                context = topChunks,
                answer
            });
        }
    }
}