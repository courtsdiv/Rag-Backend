
using RagBackend.Application.Interfaces;
using RagBackend.Domain.Utils;

namespace RagBackend.Application.Services
{
    /// <summary>
    /// This service contains the core Retrieval‑Augmented Generation (RAG) workflow.
    /// 
    /// Its responsibility is to:
    /// - Index documents into the vector store
    /// - Search for relevant document chunks
    /// - Build a grounded prompt using retrieved context
    /// - Ask the LLM to generate an answer based only on that context
    /// 
    /// This class does not handle HTTP requests or UI behaviour.
    /// It represents the main "business logic" of the RAG system.
    /// </summary>
    public class RagService
    {
        /// <summary>
        /// The expected size of each embedding vector.
        /// 
        /// This value must match the embedding model used by the LLM provider.
        /// If this value is wrong, the vector store will reject stored vectors.
        /// </summary>
        private const int EmbeddingSize = 1536;

        /// <summary>
        /// Service used to convert text into numeric embedding vectors.
        /// These vectors are used for semantic search.
        /// </summary>
        private readonly IEmbeddingService _embeddingService;

        /// <summary>
        /// Service used to store and search vectors in the vector database.
        /// </summary>
        private readonly IVectorStore _vectorStore;

        /// <summary>
        /// Service used to send prompts to the LLM and receive generated answers.
        /// </summary>
        private readonly IChatCompletionService _chatService;

        /// <summary>
        /// Logger used to record warnings and errors without stopping execution.
        /// </summary>
        private readonly ILogger<RagService> _logger;

        /// <summary>
        /// Constructor.
        /// 
        /// All dependencies are injected using dependency injection.
        /// This keeps the service loosely coupled and easy to test.
        /// </summary>
        public RagService(
            IEmbeddingService embeddingService,
            IVectorStore vectorStore,
            IChatCompletionService chatService,
            ILogger<RagService> logger)
        {
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
            _chatService = chatService;
            _logger = logger;
        }

        /// <summary>
        /// Indexes raw text so it can be retrieved later using semantic search.
        /// 
        /// This method:
        /// 1. Cleans the input text
        /// 2. Splits it into smaller chunks
        /// 3. Converts each chunk into an embedding vector
        /// 4. Stores the vector and text in the vector store
        /// </summary>
        public async Task IndexTextAsync(string text)
        {
            // Do nothing if the input text is empty
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Clean the text (remove formatting issues, extra whitespace, etc.)
            var cleanedText = TextCleaner.Clean(text);

            // Split the text into smaller chunks for better retrieval quality
            var chunks = TextChunker.ChunkText(cleanedText);

            // Ensure the vector collection exists before inserting data
            await _vectorStore.EnsureCollectionAsync(EmbeddingSize);

            foreach (var chunk in chunks)
            {
                // Skip empty chunks as a safety check
                if (string.IsNullOrWhiteSpace(chunk))
                    continue;

                try
                {
                    // Convert the text chunk into an embedding vector
                    var embedding = await _embeddingService.GetEmbeddingAsync(chunk);

                    // Store the embedding and original text in the vector store
                    await _vectorStore.UpsertAsync(embedding, chunk);
                }
                catch (Exception ex)
                {
                    // Log the error and continue indexing the remaining chunks
                    // This prevents one bad chunk from stopping the entire indexing process
                    _logger.LogWarning(ex, "Failed to index a text chunk. Skipping.");
                }
            }
        }

        /// <summary>
        /// Searches the vector store for text chunks that are semantically
        /// similar to the user's query.
        /// </summary>
        public async Task<List<string>> SearchAsync(string query, int topK)
        {
            // If the query is empty, return no results
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            // Ensure the vector collection exists before searching
            await _vectorStore.EnsureCollectionAsync(EmbeddingSize);

            // Convert the query into an embedding vector
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

            // Retrieve the most similar chunks from the vector store
            var results =
                await _vectorStore.SearchAsync(queryEmbedding, limit: topK);

            return results;
        }

        /// <summary>
        /// Main method used to answer a user question.
        /// 
        /// This method:
        /// 1. Embeds the user's question
        /// 2. Retrieves relevant context from the vector store
        /// 3. Builds a grounded prompt using that context
        /// 4. Sends the prompt to the LLM
        /// 5. Returns the answer and the context used
        /// </summary>
        public async Task<(string Answer, List<string> Context)> GetAnswerAsync(
            string question,
            int topK)
        {
            // Do not attempt to answer empty questions
            if (string.IsNullOrWhiteSpace(question))
                return (string.Empty, new List<string>());

            // Ensure the vector collection exists
            await _vectorStore.EnsureCollectionAsync(EmbeddingSize);

            // Convert the question into an embedding vector
            var questionEmbedding =
                await _embeddingService.GetEmbeddingAsync(question);

            // Retrieve relevant document chunks as context
            var contextChunks =
                await _vectorStore.SearchAsync(questionEmbedding, limit: topK);

            // Build a prompt that restricts the LLM to the retrieved context only
            var prompt =
                BuildPrompt(question, contextChunks);

            // Ask the LLM to generate an answer
            var answer =
                await _chatService.GetAnswerAsync(prompt);

            return (answer, contextChunks);
        }

        /// <summary>
        /// Builds the prompt sent to the LLM.
        /// 
        /// The prompt explicitly:
        /// - Provides retrieved context
        /// - Includes the user's question
        /// - Instructs the model to avoid guessing
        /// </summary>
        private static string BuildPrompt(
            string question,
            List<string> contextChunks)
        {
            // Combine all retrieved chunks into a single context block
            var context =
                string.Join("\n\n", contextChunks).Trim();

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