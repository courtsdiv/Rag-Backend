using System.Net.Http.Headers;
using System.Text.Json;
using RagBackend.Application.Interfaces;
using RagBackend.Domain.Errors;

namespace RagBackend.Infrastructure
{
    /// <summary>
    /// Service responsible for generating text responses by calling
    /// an external AI text generation provider.
    /// 
    /// This service sends a prompt (including context and instructions)
    /// to the provider and returns the generated answer.
    /// 
    /// This class lives in the Infrastructure layer because it:
    /// - makes HTTP requests
    /// - depends on an external service
    /// - handles low-level communication details
    /// </summary>
    public sealed class LlmService : IChatCompletionService
    {
        /// <summary>
        /// API path used to send chat completion requests
        /// to the AI provider.
        /// </summary>
        private const string ChatCompletionsPath = "chat/completions";

        /// <summary>
        /// Default language model used if none is specified
        /// in configuration.
        /// </summary>
        private const string DefaultModel = "meta-llama/llama-3.1-8b-instruct";

        /// <summary>
        /// HTTP client used to communicate with the AI provider.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Logger used to record errors and diagnostic information.
        /// </summary>
        private readonly ILogger<LlmService> _logger;

        /// <summary>
        /// Name of the language model to use for generating answers.
        /// This value is read from configuration.
        /// </summary>
        private readonly string _chatModel;

        /// <summary>
        /// Constructor.
        /// 
        /// Dependencies are injected so that:
        /// - this service is easy to test
        /// - configuration values can be changed without modifying code
        /// </summary>
        public LlmService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<LlmService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read the API key from configuration and attach it to all requests
            var apiKey = configuration["OpenRouter:ApiKey"];

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);
            }

            // Read the model name from configuration,
            // falling back to a default if none is provided
            _chatModel =
                configuration["OpenRouter:ChatModel"] ?? DefaultModel;
        }

        /// <summary>
        /// Sends a prompt to the AI provider and returns the generated response.
        /// 
        /// The prompt typically contains:
        /// - retrieved document context
        /// - the user's question
        /// - instructions to prevent guessing
        /// </summary>
        public async Task<string> GetAnswerAsync(string prompt)
        {
            // Do not attempt to generate a response for empty prompts
            if (string.IsNullOrWhiteSpace(prompt))
                return string.Empty;

            try
            {
                // Build the request body expected by the chat completion API
                var requestBody = new
                {
                    model = _chatModel,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                // Send the request to the AI provider
                var response =
                    await _httpClient.PostAsJsonAsync(
                        ChatCompletionsPath,
                        requestBody);

                // Read the raw response body
                var body =
                    await response.Content.ReadAsStringAsync();

                // Handle non-success HTTP responses
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "LLM chat error: {StatusCode} - {Body}",
                        response.StatusCode,
                        body);

                    // Translate the low-level failure into a domain-level exception
                    throw new LlmUnavailableException(
                        "LLM provider returned a non-success response.");
                }

                // Parse the JSON response to extract the generated answer
                using var doc = JsonDocument.Parse(body);

                // Safely navigate the expected response structure
                if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                    choices.GetArrayLength() == 0)
                    return string.Empty;

                var firstChoice = choices[0];

                if (!firstChoice.TryGetProperty("message", out var message))
                    return string.Empty;

                if (!message.TryGetProperty("content", out var contentElement))
                    return string.Empty;

                // Return the generated text response
                return contentElement.GetString() ?? string.Empty;
            }
            catch (LlmUnavailableException)
            {
                // Allow known infrastructure errors to propagate unchanged
                throw;
            }
            catch (Exception ex)
            {
                // Log unexpected failures and convert them into a controlled exception
                _logger.LogError(ex, "LLM chat request failed.");

                throw new LlmUnavailableException(
                    "Failed to generate a response from the AI provider.");
            }
        }
    }
}