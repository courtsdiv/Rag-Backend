using Microsoft.AspNetCore.Mvc;
using RagBackend.Domain.Models;
using RagBackend.Domain.Errors;
using RagBackend.Infrastructure;

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
        private readonly OpenRouterEmbeddingService _embeddingService;

        // Service that stores and finds vectors (Qdrant client).
        private readonly QdrantService _qdrantService;

        // Service that calls a chat model to produce answers.
        private readonly OpenRouterChatService _chatService;

        // The size of embeddings produced by the embedding model.
        private const int EmbeddingSize = 1536;

        private readonly int _topK;

        public RagController(
            OpenRouterEmbeddingService embeddingService,
            QdrantService qdrantService,
            OpenRouterChatService chatService,
            IConfiguration config)
        {
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _chatService = chatService;

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

            try
            {
                await _qdrantService.EnsureCollectionAsync(EmbeddingSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Qdrant EnsureCollection failed (index): " + ex.Message);
                return ErrorResults.QdrantUnavailable();
            }

            float[] vector;
            try
            {
                vector = await _embeddingService.GetEmbeddingAsync(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Embedding service failure (index): " + ex.Message);
                return ErrorResults.LlmUnavailable();
            }

            try
            {
                await _qdrantService.UpsertAsync(vector, text);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Qdrant upsert failure: " + ex.Message);
                return ErrorResults.QdrantUnavailable();
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

            try
            {
                await _qdrantService.EnsureCollectionAsync(EmbeddingSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Qdrant EnsureCollection failed (search): " + ex.Message);
                return ErrorResults.QdrantUnavailable();
            }

            float[] queryVector;
            try
            {
                queryVector = await _embeddingService.GetEmbeddingAsync(query);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Embedding service failure (search): " + ex.Message);
                return ErrorResults.LlmUnavailable();
            }

            List<string> results;
            try
            {
                results = await _qdrantService.SearchAsync(queryVector, limit: _topK);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Qdrant search failure: " + ex.Message);
                return ErrorResults.QdrantUnavailable();
            }

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

            // 1. Ensure Qdrant collection exists
            try
            {
                await _qdrantService.EnsureCollectionAsync(EmbeddingSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Qdrant EnsureCollection failed (answer): " + ex.Message);
                return ErrorResults.QdrantUnavailable();
            }

            float[] queryEmbedding;

            // 2. Get embedding for the question (uses OpenRouter)
            try
            {
                queryEmbedding = await _embeddingService.GetEmbeddingAsync(question);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Embedding service failure (answer): " + ex.Message);
                return ErrorResults.LlmUnavailable();
            }

            Console.WriteLine($"[RAG] Question: \"{question}\"");
            Console.WriteLine($"[RAG] Using TopK = {_topK}");

            List<string> topChunks;

            // 3. Search Qdrant
            try
            {
                topChunks = await _qdrantService.SearchAsync(queryEmbedding, limit: _topK);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Qdrant search failure (answer): " + ex.Message);
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

            Console.WriteLine("----- FINAL PROMPT -----");
            Console.WriteLine(prompt);
            Console.WriteLine("------------------------");

            string answer;

            // 4. Ask LLM to answer using the prompt
            try
            {
                answer = await _chatService.GetAnswerAsync(prompt);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] LLM answer failure: " + ex.Message);
                return ErrorResults.LlmUnavailable();
            }

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