using Microsoft.AspNetCore.Mvc;
using RagBackend.Application.Services;
using RagBackend.Domain.Errors;
using RagBackend.Domain.Models;

namespace RagBackend.API
{
    /// <summary>
    /// API controller responsible for handling HTTP requests related to the RAG pipeline.
    /// 
    /// This controller is intentionally thin:
    /// - It validates HTTP input
    /// - Calls RagService to perform business logic
    /// - Converts results or errors into HTTP responses
    /// 
    /// It does NOT contain RAG logic, LLM logic, or database logic.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RagController : ControllerBase
    {

        /// Application service that runs the RAG workflow (index, search, answer).
        private readonly RagService _ragService;

       
        /// Logger for recording errors or unexpected behaviour at the API level.
        private readonly ILogger<RagController> _logger;

        
        /// Number of results to retrieve from the vector database.
        private readonly int _topK;

      
        /// Constructor.
        /// All dependencies are injected via dependency injection (DI).
        public RagController(
            RagService ragService,
            IConfiguration config,
            ILogger<RagController> logger)
        {
            _ragService = ragService;
            _logger = logger;

            // Read TopK from configuration, defaulting to 3 if not set.
            _topK = config.GetValue<int>("Retrieval:TopK", 3);
        }

        /// Indexes raw text so it can be retrieved later via semantic search.
        /// This endpoint is typically used after uploading a document.
        [HttpPost("index")]
        public async Task<IActionResult> IndexText([FromBody] string text)
        {
            // Basic input validation at the API boundary
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
                await _ragService.IndexTextAsync(text);

                return Ok(new
                {
                    message = "Text indexed successfully."
                });
            }
            catch (Exception ex)
            {
                // If indexing fails, this is most likely due to the vector database
                _logger.LogError(ex, "Failed to index text.");
                return ErrorResults.QdrantUnavailable();
            }
        }

        /// Searches the vector database for the most relevant text chunks for a query.
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
                var results = await _ragService.SearchAsync(query, _topK);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search indexed content.");
                return ErrorResults.QdrantUnavailable();
            }
        }

        /// Generates a grounded answer to a user question using retrieved context only.
        /// This is the main RAG endpoint used by the frontend chat interface.
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

            try
            {
                var (answer, context) =
                    await _ragService.GetAnswerAsync(question, _topK);

                return Ok(new
                {
                    question,
                    context,
                    answer
                });
            }
            catch (QdrantConfigException)
            {
                // Configuration or availability issue with the vector database
                return ErrorResults.QdrantUnavailable();
            }
            catch (Exception ex)
            {
                // Any remaining errors are assumed to come from the LLM provider
                _logger.LogError(ex, "Failed to generate answer.");
                return ErrorResults.LlmUnavailable();
            }
        }
    }
}