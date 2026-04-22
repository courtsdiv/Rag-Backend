using Microsoft.AspNetCore.Mvc;
using RagBackend.Application.Services;
using RagBackend.Domain.Errors;
using RagBackend.Domain.Models;

namespace RagBackend.API
{
    /// <summary>
    /// This controller acts as the entry point for HTTP requests related to the RAG system.
    /// 
    /// Its job is NOT to contain business logic.
    /// Instead, it:
    /// - Receives requests from the frontend
    /// - Validates basic input
    /// - Calls the appropriate application service
    /// - Converts results or errors into HTTP responses
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class RagController : ControllerBase
    {
        /// <summary>
        /// Service responsible for indexing documents and performing retrieval + generation.
        /// </summary>
        private readonly RagService _ragService;

        /// <summary>
        /// Service responsible for chat-specific behaviour
        /// (intent checking, fallbacks, calling the RAG pipeline).
        /// </summary>
        private readonly ChatService _chatService;

        /// <summary>
        /// Logger used to record useful information and errors for debugging.
        /// </summary>
        private readonly ILogger<RagController> _logger;

        /// <summary>
        /// Number of document chunks to retrieve from the vector store for each question.
        /// This value is read from configuration.
        /// </summary>
        private readonly int _topK;

        /// <summary>
        /// Constructor.
        /// 
        /// All dependencies are provided via dependency injection.
        /// This makes the controller easier to test and keeps it loosely coupled.
        /// </summary>
        public RagController(
            RagService ragService,
            ChatService chatService,
            IConfiguration config,
            ILogger<RagController> logger)
        {
            _ragService = ragService;
            _chatService = chatService;
            _logger = logger;

            // Read the number of chunks to retrieve (TopK) from configuration.
            // If the value is not present, default to 3.
            _topK = config.GetValue<int>("Retrieval:TopK", 3);
        }

        /// <summary>
        /// Index endpoint.
        /// 
        /// This endpoint accepts raw text from the frontend and stores it
        /// in the vector database so it can be searched later.
        /// </summary>
        [HttpPost("index")]
        public async Task<IActionResult> IndexText([FromBody] string text)
        {
            // Basic validation: empty text should not be indexed
            if (string.IsNullOrWhiteSpace(text))
            {
                return BadRequest(new ApiError
                {
                    Message = "Text cannot be empty",
                    ErrorCode = "INVALID_INPUT"
                });
            }

            try
            {
                // Pass the text to the application service for indexing
                await _ragService.IndexTextAsync(text);

                // Return a simple success message
                return Ok(new { message = "Text indexed successfully" });
            }
            catch (VectorStoreUnavailableException)
            {
                // If the vector store is unavailable, return a consistent error response
                return ErrorResults.VectorStoreUnavailable();
            }
        }

        /// <summary>
        /// Chat endpoint (Phase 2).
        /// 
        /// This is the main endpoint used by the chat UI.
        /// It receives a user message and returns:
        /// - an answer
        /// - retrieved context
        /// - or a clarification / fallback response
        /// </summary>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatMessageRequest request)
        {
            // Log that the chat endpoint has been hit (useful during debugging)
            _logger.LogInformation("HTTP POST /api/Rag/chat");

            // Validate the incoming message
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return BadRequest(new ApiError
                {
                    Message = "Message cannot be empty.",
                    ErrorCode = "INVALID_INPUT"
                });
            }

            try
            {
                // Delegate chat processing to the ChatService
                var response =
                    await _chatService.ProcessMessageAsync(request.Message, _topK);

                // Return the structured chat response
                return Ok(response);
            }
            catch (VectorStoreUnavailableException ex)
            {
                // Log the error and return a vector-store-specific failure response
                _logger.LogError(ex, "Vector store unavailable during chat.");
                return ErrorResults.VectorStoreUnavailable();
            }
            catch (Exception ex)
            {
                // Any remaining errors are treated as LLM-related failures
                _logger.LogError(ex, "LLM unavailable during chat.");
                return ErrorResults.LlmUnavailable();
            }
        }
    }
}
