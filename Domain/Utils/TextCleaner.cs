using System.Text.RegularExpressions;

namespace RagBackend.Domain.Utils
{
    // Utility for cleaning and normalising raw text before chunking and retrieval
    public static class TextCleaner
    {
        // Normalises input text by fixing line breaks, spacing, and formatting issues
        public static string Clean(string text)
        {
            // Guard: return empty string if input text is empty
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Normalise line endings
            text = text.Replace("\r\n", "\n");

            // Join headings or sentences split across lines
            text = Regex.Replace(text, "([a-zA-Z])\\s*\\n\\s*([A-Z])", "$1. $2");

            // Join words split mid-word across line breaks
            text = Regex.Replace(text, "([a-zA-Z])\\s*\\n\\s*([a-z])", "$1$2");

            // Replace remaining line breaks with spaces
            text = Regex.Replace(text, "\\n+", " ");

            // Collapse multiple spaces into a single space
            text = Regex.Replace(text, "\\s{2,}", " ");

            return text.Trim();
        }
    }
}