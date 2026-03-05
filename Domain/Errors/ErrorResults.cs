using Microsoft.AspNetCore.Mvc;
using RagBackend.Domain.Models;

namespace RagBackend.Domain.Errors
{
    /// <summary>
    /// Central location for creating consistent API error responses.
    /// Helps keep controllers clean and all error formatting in one place.
    /// </summary>
    public static class ErrorResults
    {
        public static IActionResult QdrantUnavailable() =>
            new ObjectResult(new ApiError
            {
                Message = "The vector database is currently unavailable.",
                ErrorCode = "QDRANT_UNAVAILABLE"
            })
            {
                StatusCode = 503
            };

        public static IActionResult LlmUnavailable() =>
            new ObjectResult(new ApiError
            {
                Message = "The AI model is currently unavailable.",
                ErrorCode = "LLM_UNAVAILABLE"
            })
            {
                StatusCode = 503
            };

        public static IActionResult Unexpected(string? msg = null) =>
            new ObjectResult(new ApiError
            {
                Message = msg ?? "An unexpected error occurred. Please try again.",
                ErrorCode = "UNEXPECTED_ERROR"
            })
            {
                StatusCode = 500
            };
    }
}