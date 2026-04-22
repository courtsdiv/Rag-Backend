using System.Text.RegularExpressions;

namespace RagBackend.Domain.Utils
{
    /// <summary>
    /// Utility class responsible for cleaning and normalising raw text
    /// before it is split into chunks and stored in the vector store.
    /// 
    /// Text cleaning is important because documents often contain:
    /// - inconsistent line breaks
    /// - formatting artefacts
    /// - broken sentences or words
    /// 
    /// Cleaning the text helps improve embedding quality
    /// and retrieval accuracy.
    /// </summary>
    public static class TextCleaner
    {
        /// <summary>
        /// Cleans and normalises raw text by fixing common formatting issues.
        /// 
        /// This method:
        /// - normalises line endings
        /// - joins sentences split across lines
        /// - fixes words broken by line breaks
        /// - removes extra whitespace
        /// </summary>
        /// <param name="text">
        /// The raw text to clean.
        /// </param>
        /// <returns>
        /// A cleaned version of the text, ready for chunking and embedding.
        /// </returns>
        public static string Clean(string text)
        {
            // If the input text is empty, return an empty string
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Normalise Windows-style line endings to Unix-style
            // This makes further processing more predictable
            text = text.Replace("\r\n", "\n");

            // Join sentences or headings that were split across lines.
            // Example:
            // "This is a sentence\nThat continues" → "This is a sentence. That continues"
            text = Regex.Replace(
                text,
                "([a-zA-Z])\\s*\\n\\s*([A-Z])",
                "$1. $2");

            // Join words that were broken across line breaks.
            // Example:
            // "infor\nmation" → "information"
            text = Regex.Replace(
                text,
                "([a-zA-Z])\\s*\\n\\s*([a-z])",
                "$1$2");

            // Replace any remaining line breaks with a single space
            text = Regex.Replace(text, "\\n+", " ");

            // Collapse multiple spaces into a single space
            text = Regex.Replace(text, "\\s{2,}", " ");

            // Remove leading and trailing whitespace
            return text.Trim();
        }
    }
}