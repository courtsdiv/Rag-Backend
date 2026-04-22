using RagBackend.Application.Interfaces;
using RagBackend.Domain.Utils;

namespace RagBackend.Application.Services
{
    /// <summary>
    /// Application service that runs the main RAG workflow.
    /// 
    /// RAG = Retrieve relevant text (from Qdrant) + Generate an answer (from the LLM).
    /// This class keeps the "business flow" in one place, and hides provider details behind interfaces.
    /// </summary>
    public class RagService
    {
        /// The number of values in each embedding vector.
        /// This must match the embedding model you are using (e.g., OpenRouter embedding model).
        private const int EmbeddingSize = 1536;

        // ---- Dependencies (injected through the constructor) ----

        /// Service used to turn text into a numeric vector (embedding) for semantic search.
        private readonly IEmbeddingService _embeddingService;

        /// Service used to store and search embeddings in the vector database (Qdrant).
        private readonly IQdrantService _qdrantService;

        /// Service used to send a prompt to the LLM and get an answer back.
        private readonly IChatCompletionService _chatService;

        /// Logger used to record warnings/errors without crashing the application.
        private readonly ILogger<RagService> _logger;

        /// Constructor: all dependencies are passed in using dependency injection (DI).
        /// This makes the class easier to test and allows providers to be swapped later.
        public RagService(
            IEmbeddingService embeddingService,
            IQdrantService qdrantService,
            IChatCompletionService chatService,
            ILogger<RagService> logger)
        {
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _chatService = chatService;
            _logger = logger;
        }

        /// <summary>
        /// Takes raw text, cleans it, splits it into chunks, embeds each chunk,
        /// and stores the chunk + vector in Qdrant so it can be searched later.
        /// </summary>
        public async Task IndexTextAsync(string text)
        {
            // Basic guard: no point doing work if the input is empty.
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Clean the text (remove weird whitespace, fix formatting etc.)
            var cleaned = TextCleaner.Clean(text);

            // Split into smaller pieces so retrieval works better.
            var chunks = TextChunker.ChunkText(cleaned);

            // Ensure the collection exists before we store vectors.
            // We pass EmbeddingSize so the collection matches the embedding model dimension.
            await _qdrantService.EnsureCollectionAsync(EmbeddingSize);

            foreach (var chunk in chunks)
            {
                // Skip empty chunks (extra safety)
                if (string.IsNullOrWhiteSpace(chunk))
                    continue;

                try
                {
                    // Turn this chunk into an embedding vector
                    var vector = await _embeddingService.GetEmbeddingAsync(chunk);

                    // Store the vector + original text chunk in Qdrant
                    await _qdrantService.UpsertAsync(vector, chunk);
                }
                catch (Exception ex)
                {
                    // If one chunk fails, we log it and continue.
                    // This prevents one bad chunk from stopping the whole indexing job.
                    _logger.LogWarning(ex, "Failed to index a text chunk. Skipping.");
                }
            }
        }

        /// <summary>
        /// Converts a user query into an embedding vector and searches Qdrant for the most similar chunks.
        /// Returns the topK most relevant text chunks.
        /// </summary>
        public async Task<List<string>> SearchAsync(string query, int topK)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            // Ensure the collection exists before searching
            await _qdrantService.EnsureCollectionAsync(EmbeddingSize);

            // Turn the query into a vector
            var queryVector = await _embeddingService.GetEmbeddingAsync(query);

            // Find the most similar chunks in Qdrant
            var results = await _qdrantService.SearchAsync(queryVector, limit: topK);

            return results;
        }

        /// <summary>
        /// Main "ask a question" method:
        /// 1) Embed the question
        /// 2) Retrieve topK relevant chunks from Qdrant
        /// 3) Build a grounded prompt using only that context
        /// 4) Send prompt to LLM and return the answer + context
        /// </summary>
        public async Task<(string Answer, List<string> Context)> GetAnswerAsync(string question, int topK)
        {
            if (string.IsNullOrWhiteSpace(question))
                return (string.Empty, new List<string>());

            // Ensure the vector collection exists
            await _qdrantService.EnsureCollectionAsync(EmbeddingSize);

            // Embed the user question
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(question);

            // Retrieve relevant chunks (context) from Qdrant
            var topChunks = await _qdrantService.SearchAsync(queryEmbedding, limit: topK);

            // Build a prompt that forces the model to answer ONLY using the retrieved context
            var prompt = BuildPrompt(question, topChunks);

            // Ask the model for an answer
            var answer = await _chatService.GetAnswerAsync(prompt);
             
            return (answer, topChunks);
        }
  
        /// Creates the LLM prompt.
        /// The key idea is to include context + strict instructions so the model stays grounded.
        private static string BuildPrompt(string question, List<string> contextChunks)
        {
            // Combine all chunks into one context block
            var context = string.Join("\n\n", contextChunks).Trim();

            // Prompt format: context, question, and rules for the model.
            return $@"
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
        }
    }
}
