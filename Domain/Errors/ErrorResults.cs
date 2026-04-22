using Microsoft.AspNetCore.Mvc;
using RagBackend.Domain.Models;

namespace RagBackend.Domain.Errors
{
    // Static helpers for returning consistent API error responses
    public static class ErrorResults
    {
        // Returned when the vector database is unavailable or misconfigured
        public static IActionResult QdrantUnavailable() =>
            new ObjectResult(new ApiError
            {
                Message = "The vector database is currently unavailable.",
                ErrorCode = "QDRANT_UNAVAILABLE"
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };

        // Returned when the LLM provider cannot be reached
        public static IActionResult LlmUnavailable() =>
            new ObjectResult(new ApiError
            {
                Message = "The AI model is currently unavailable.",
                ErrorCode = "LLM_UNAVAILABLE"
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };

        // Fallback for unexpected server-side errors
        public static IActionResult Unexpected(string? message = null) =>
            new ObjectResult(new ApiError
            {
                Message = message ?? "An unexpected error occurred.",
                ErrorCode = "UNEXPECTED_ERROR"
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
    }

    // Exception thrown when Qdrant configuration is missing or invalid
    public class QdrantConfigException : Exception
    {
        public QdrantConfigException(string message) : base(message) { }
    }
}