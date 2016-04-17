using System.IO;
using System.Text;

namespace IrcDotNet
{
    // Reads lines from text sources safely; non-terminated lines are not returned.
    internal class SafeLineReader
    {
        // Current incomplete line;
        private string currentLine;
        // Reads characters from text source.

        public SafeLineReader(TextReader textReader)
        {
            TextReader = textReader;
            currentLine = string.Empty;
        }

        public TextReader TextReader { get; }

        // Reads line from source, ensuring that line is not returned unless it terminates with line break.
        public string ReadLine()
        {
            var lineBuilder = new StringBuilder();
            int nextChar;

            while (true)
            {
                // Check whether to stop reading characters.
                nextChar = TextReader.Peek();
                if (nextChar == -1)
                {
                    currentLine = lineBuilder.ToString();
                    break;
                }
                if (nextChar == '\r' || nextChar == '\n')
                {
                    TextReader.Read();
                    if (TextReader.Peek() == '\n')
                        TextReader.Read();

                    var line = currentLine + lineBuilder;
                    currentLine = string.Empty;
                    return line;
                }

                // Append next character to line.
                lineBuilder.Append((char) TextReader.Read());
            }

            return null;
        }
    }
}