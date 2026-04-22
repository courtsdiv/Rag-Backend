using Microsoft.AspNetCore.Mvc;
using RagBackend.Domain.Models;

namespace RagBackend.Domain.Errors
{
    /// <summary>
    /// This static class contains helper methods for returning
    /// consistent API error responses from controllers.
    /// 
    /// Instead of repeating error response code in every controller,
    /// these helpers centralise error creation in one place.
    /// </summary>
    public static class ErrorResults
    {
        /// <summary>
        /// Creates an error response used when the vector store
        /// (where document embeddings are stored) is unavailable
        /// or misconfigured.
        /// 
        /// This returns a 503 Service Unavailable status code,
        /// indicating a temporary backend dependency issue.
        /// </summary>
        public static IActionResult VectorStoreUnavailable()
        {
            return new ObjectResult(new ApiError
            {
                Message = "The vector store is currently unavailable.",
                ErrorCode = "VECTOR_STORE_UNAVAILABLE"
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }

        /// <summary>
        /// Creates an error response used when the LLM provider
        /// cannot be reached or fails to generate a response.
        /// 
        /// This also returns a 503 Service Unavailable status code,
        /// as the failure is due to an external dependency.
        /// </summary>
        public static IActionResult LlmUnavailable()
        {
            return new ObjectResult(new ApiError
            {
                Message = "The AI model is currently unavailable.",
                ErrorCode = "LLM_UNAVAILABLE"
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }

        /// <summary>
        /// Creates a generic error response for unexpected
        /// server-side failures.
        /// 
        /// This should be used sparingly, as more specific
        /// error responses are preferred when possible.
        /// </summary>
        public static IActionResult Unexpected(string? message = null)
        {
            return new ObjectResult(new ApiError
            {
                Message = message ?? "An unexpected error occurred.",
                ErrorCode = "UNEXPECTED_ERROR"
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    /// <summary>
    /// Exception thrown when the vector store is unavailable
    /// or incorrectly configured.
    /// 
    /// This exception is used to signal infrastructure-level
    /// failures and is translated into a user-facing error
    /// by the controller layer.
    /// </summary>
    public class VectorStoreUnavailableException : Exception
    {
        public VectorStoreUnavailableException(string message)
            : base(message)
        {
        }
    }
    /// <summary>
    /// Exception thrown when the LLM provider is unavailable
    /// or fails to generate a response.
    /// 
    /// This exception is used to signal infrastructure-level
    /// failures related to the LLM and is translated into a user-facing error
    /// by the controller layer.
    /// </summary>
    public class LlmUnavailableException : Exception
    {
        public LlmUnavailableException(string message)
            : base(message)
        {    
        }
    }
}
