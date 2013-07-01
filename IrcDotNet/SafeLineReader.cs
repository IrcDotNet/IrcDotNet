using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    // Reads lines from text sources safely; unterminated lines are not returned.
    internal class SafeLineReader
    {
        // Reads characters from text source.
        private TextReader textReader;

        // Current incomplete line;
        private string currentLine;

        public SafeLineReader(TextReader textReader)
        {
            this.textReader = textReader;
            this.currentLine = string.Empty;
        }

        public TextReader TextReader
        {
            get { return this.textReader; }
        }

        // Reads line from source, ensuring that line is not returned unless it terminates with line break.
        public string ReadLine()
        {
            var lineBuilder = new StringBuilder();
            int nextChar;

            while (true)
            {
                // Check whether to stop reading characters.
                nextChar = this.textReader.Peek();
                if (nextChar == -1)
                {
                    this.currentLine = lineBuilder.ToString();
                    break;
                }
                else if (nextChar == '\r' || nextChar == '\n')
                {
                    this.textReader.Read();
                    if (this.textReader.Peek() == '\n')
                        this.textReader.Read();

                    var line = this.currentLine + lineBuilder.ToString();
                    this.currentLine = string.Empty;
                    return line;
                }

                // Append next character to line.
                lineBuilder.Append((char)this.textReader.Read());
            }

            return null;
        }
    }
}
