using RagBackend.Domain.Models;
using System.Text.RegularExpressions;

namespace RagBackend.Application.Services
{
    /// <summary>
    /// This service handles chat-specific behaviour.
    /// 
    /// It is responsible for:
    /// - Checking whether a user question is clear enough to answer
    /// - Preventing vague questions from triggering the full RAG pipeline
    /// - Calling the RAG service when a question is valid
    /// - Returning either an answer, a clarification request, or a fallback response
    /// 
    /// This class does NOT talk to the database or the LLM directly.
    /// It orchestrates the flow and applies safety rules.
    /// </summary>
    public sealed class ChatService
    {
        /// <summary>
        /// Application service that runs the core RAG workflow
        /// (retrieval + answer generation).
        /// </summary>
        private readonly RagService _ragService;

        /// <summary>
        /// Logger used to record errors and useful diagnostic information.
        /// </summary>
        private readonly ILogger<ChatService> _logger;

        /// <summary>
        /// Constructor.
        /// 
        /// Dependencies are injected so that:
        /// - the service is easy to test
        /// - implementations can be swapped if needed
        /// </summary>
        public ChatService(RagService ragService, ILogger<ChatService> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        /// <summary>
        /// Processes a single chat message from the user.
        /// 
        /// This method:
        /// 1. Validates the input
        /// 2. Checks system health
        /// 3. Detects vague questions
        /// 4. Calls the RAG pipeline for valid questions
        /// 5. Returns a structured response for the UI
        /// </summary>
        public async Task<ChatMessageResponse> ProcessMessageAsync(string message, int topK)
        {
            // If the user sends an empty message, ask them to type something
            if (string.IsNullOrWhiteSpace(message))
            {
                return new ChatMessageResponse
                {
                    Reply = "Please type a question.",
                    IsFallback = true,
                    NeedsClarification = true
                };
            }

            try
            {
                // System health check:
                // This ensures that the vector store is available before
                // we perform intent checks or answer generation.
                await _ragService.SearchAsync("health check", topK: 1);

                // Low-intent guard:
                // Prevents vague or incomplete questions from triggering RAG.
                if (IsLowIntent(message))
                {
                    return new ChatMessageResponse
                    {
                        Reply = "Could you be more specific?",
                        IsFallback = true,
                        NeedsClarification = true
                    };
                }

                // Call the RAG pipeline for valid questions
                var (answer, context) =
                    await _ragService.GetAnswerAsync(message, topK);

                // If no relevant context was found, return a safe fallback
                if (context == null || context.Count == 0)
                {
                    return new ChatMessageResponse
                    {
                        Reply = "The provided documents do not contain this information.",
                        Context = new List<string>(),
                        IsFallback = true,
                        NeedsClarification = false
                    };
                }

                // Successful answer
                return new ChatMessageResponse
                {
                    Reply = answer,
                    Context = context,
                    IsFallback = false,
                    NeedsClarification = false
                };
            }
            catch (Exception ex)
            {
                // Log unexpected errors and allow the controller
                // to convert them into HTTP error responses
                _logger.LogError(ex, "Failed to process chat message.");
                throw;
            }
        }

        /// <summary>
        /// Regular expression used to match very short or incomplete question stems.
        /// Examples:
        /// - "what"
        /// - "how"
        /// - "why"
        /// - "what is"
        /// </summary>
        private static readonly Regex LowIntentRegex = new(
            @"^(what|why|how)\s*$|^(what is|what are|how does|how do|why is|why are)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Determines whether a user message is too vague to answer.
        /// Returns true if the message is considered low intent.
        /// </summary>
        private static bool IsLowIntent(string message)
        {
            var text = message.Trim().ToLowerInvariant();

            // Messages that are too short are always considered vague
            if (text.Length < 3)
                return true;

            // Bare question words like "what" or "how"
            if (LowIntentRegex.IsMatch(text))
                return true;

            // Incomplete questions such as "what is a"
            if (Regex.IsMatch(
                text,
                @"^(what is|what are|how does|how do|why is|why are)\s+(a|an|the)\s*$",
                RegexOptions.IgnoreCase))
                return true;

            // A question is low intent ONLY if it has no meaningful topic
            return !HasMeaningfulTopic(text);
        }

        /// <summary>
        /// Checks whether the message contains a meaningful topic word.
        /// This helps distinguish between:
        /// - "what is a supplier" (valid)
        /// - "what is a" (vague)
        /// </summary>
        private static bool HasMeaningfulTopic(string text)
        {
            // Remove common question stems
            var remainder = Regex.Replace(
                text,
                @"^(what is|what are|how does|how do|why is|why are)\s+",
                "",
                RegexOptions.IgnoreCase);

            // Remove leading articles
            remainder = Regex.Replace(
                remainder,
                @"^(a|an|the)\s+",
                "",
                RegexOptions.IgnoreCase);

            // Split the remaining text into words
            var words = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Require at least one word that looks like a real topic
            return words.Any(w => w.Length >= 3);
        }
    }
}