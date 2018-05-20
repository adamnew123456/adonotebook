using System;
using System.IO;
using System.Text;

namespace ADONotebook
{
    /// <summary>
    ///   Tracks what the lexer was processing after the previous feeding.
    /// </summary>
    public enum LexerState
    {
        NORMAL,
        LINE_COMMENT, BLOCK_COMMENT,
        STRING,
        ANSI_QUOTE, MYSQL_QUOTE, TSQL_QUOTE,
        COMPLETE,
        ERROR
    }

    /// <summary>
    ///   A basic SQL lexer that understands the basics of SQL's commenting
    ///   and quoting rules.
    /// </summary>
    public class SqlLexer
    {
        /// <summary>
        ///   What the lexer was last processing.
        /// </summary>
        public LexerState State { get; private set; }

        private int ParenDepth = 0;

        public SqlLexer()
        {
            State = LexerState.NORMAL;
        }

        /// <summary>
        ///   Handles a single character from the input, or possibly more in
        ///   certain double-quoting cases.
        /// </summary>
        private void DispatchCharacter(MemoryStream memory, char character)
        {
            switch (character)
            {
                case '\'':
                    if (State == LexerState.STRING)
                    {
                        var nextRawCharacter = memory.ReadByte();
                        if (nextRawCharacter == -1)
                        {
                            State = LexerState.NORMAL;
                            return;
                        }

                        var nextCharacter = (char)nextRawCharacter;
                        if (nextCharacter != '\'')
                        {
                            State = LexerState.NORMAL;
                            DispatchCharacter(memory, nextCharacter);
                        }
                    }
                    else if (State == LexerState.NORMAL)
                    {
                        State = LexerState.STRING;
                    }

                    break;

                case '-':
                    if (State == LexerState.NORMAL)
                    {
                        var nextRawCharacter = memory.ReadByte();
                        if (nextRawCharacter == -1)
                        {
                            State = LexerState.NORMAL;
                            return;
                        }

                        var nextCharacter = (char)nextRawCharacter;
                        if (nextCharacter == '-')
                        {
                            State = LexerState.LINE_COMMENT;
                        }
                        else
                        {
                            DispatchCharacter(memory, nextCharacter);
                        }
                    }
                    break;

                case '/':
                    if (State == LexerState.NORMAL)
                    {
                        var nextRawCharacter = memory.ReadByte();
                        if (nextRawCharacter == -1)
                        {
                            State = LexerState.NORMAL;
                            return;
                        }

                        var nextCharacter = (char)nextRawCharacter;
                        if (nextCharacter == '*')
                        {
                            State = LexerState.BLOCK_COMMENT;
                        }
                        else if (nextCharacter == '/')
                        {
                            State = LexerState.LINE_COMMENT;
                        }
                        else
                        {
                            DispatchCharacter(memory, nextCharacter);
                        }
                    }
                    break;

                case '*':
                    if (State == LexerState.BLOCK_COMMENT)
                    {
                        var nextRawCharacter = memory.ReadByte();
                        if (nextRawCharacter == -1)
                        {
                            State = LexerState.NORMAL;
                            return;
                        }

                        var nextCharacter = (char)nextRawCharacter;
                        if (nextCharacter == '/')
                        {
                            State = LexerState.NORMAL;
                        }
                        else
                        {
                            DispatchCharacter(memory, nextCharacter);
                        }
                    }
                    break;

                case '"':
                    if (State == LexerState.NORMAL)
                    {
                        State = LexerState.ANSI_QUOTE;
                    }
                    else if (State == LexerState.ANSI_QUOTE)
                    {
                        State = LexerState.NORMAL;
                    }
                    break;

                case '`':
                    if (State == LexerState.NORMAL)
                    {
                        State = LexerState.MYSQL_QUOTE;
                    }
                    else if (State == LexerState.MYSQL_QUOTE)
                    {
                        State = LexerState.NORMAL;
                    }
                    break;

                case '[':
                    if (State == LexerState.NORMAL)
                    {
                        State = LexerState.TSQL_QUOTE;
                    }
                    break;

                case ']':
                    if (State == LexerState.TSQL_QUOTE)
                    {
                        State = LexerState.NORMAL;
                    }
                    else
                    {
                        State = LexerState.ERROR;
                    }
                    break;

                case '(':
                    if (State == LexerState.NORMAL)
                    {
                        ParenDepth++;
                    }
                    break;

                case ')':
                    if (State == LexerState.NORMAL)
                    {
                        ParenDepth--;
                        if (ParenDepth < 0)
                        {
                            State = LexerState.ERROR;
                        }
                    }
                    break;

                case ';':
                    if (State == LexerState.NORMAL)
                    {
                        if (ParenDepth == 0)
                        {
                            State = LexerState.COMPLETE;
                        }
                        else
                        {
                            State = LexerState.ERROR;
                        }
                    }
                    break;

                case '\n':
                case '\r':
                    if (State == LexerState.LINE_COMMENT)
                    {
                        State = LexerState.NORMAL;
                    }
                    else if (State == LexerState.STRING ||
                             State == LexerState.ANSI_QUOTE ||
                             State == LexerState.MYSQL_QUOTE ||
                             State == LexerState.TSQL_QUOTE)
                    {
                        State = LexerState.ERROR;
                    }
                    break;
            }
        }

        /// <summary>
        ///   Consumes the given string, updating the lexer state based upon
        ///   its contents.
        /// </summary>
        public void Feed(string fragment)
        {
            var memory = new MemoryStream(Encoding.UTF8.GetBytes(fragment));
            while (State != LexerState.COMPLETE &&
                   State != LexerState.ERROR)
            {
                var rawCharacter = memory.ReadByte();
                if (rawCharacter == -1)
                {
                    break;
                }

                var character = (char)rawCharacter;
                DispatchCharacter(memory, character);
            }
        }
    }
}
