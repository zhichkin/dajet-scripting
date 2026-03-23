using System.Text;

namespace DaJet.Scripting
{
    public sealed class Lexer
    {
        private StringReader _reader;
        private List<Lexeme> _tokens;
        private int _line = 1;
        private int _start = 0;
        private int _position = 0;
        private char _char = char.MinValue;
        private StringBuilder _lexeme;
        public bool TryScan(in string script, out List<Lexeme> tokens, out string error)
        {
            error = string.Empty;

            _reader = new StringReader(script);
            _lexeme = new StringBuilder(256);
            _tokens = new List<Lexeme>(256);

            try
            {
                Scan();
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            tokens = _tokens;

            return string.IsNullOrEmpty(error);
        }
        private string GetErrorText(string reason)
        {
            return $"{reason}. [{_char}] {{{_line}:{_position}}}";
        }

        private void Scan()
        {
            while (Consume())
            {
                if (_char == '\n')
                {
                    _line++;
                }
                else if (_char == ' ' || _char == '\r' || _char == '\t')
                {
                    // ignore
                }
                else if (_char == '+')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.Plus);
                }
                else if (_char == '-')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('-'))
                    {
                        SingleLineComment();
                    }
                    else
                    {
                        AddToken(Token.Minus);
                    }
                }
                else if (_char == '*') // Multiply | SELECT *
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.Star);
                }
                else if (_char == '/')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('*'))
                    {
                        MultiLineComment();
                    }
                    else
                    {
                        AddToken(Token.Divide);
                    }
                }
                else if (_char == '%')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.Modulo);
                }
                else if (_char == '=')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.Equals);
                }
                else if (_char == '!')
                {
                    _start = _position;
                    _lexeme.Append(_char);

                    if (Consume('='))
                    {
                        _lexeme.Append('=');
                        AddToken(Token.NotEquals);
                    }
                    else
                    {
                        _lexeme.Clear();
                    }
                }
                else if (_char == '>')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('='))
                    {
                        _lexeme.Append('=');
                        AddToken(Token.GreaterOrEquals);
                    }
                    else
                    {
                        AddToken(Token.Greater);
                    }
                }
                else if (_char == '<')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('='))
                    {
                        _lexeme.Append('=');
                        AddToken(Token.LessOrEquals);
                    }
                    else if (Consume('>'))
                    {
                        _lexeme.Append('>');
                        AddToken(Token.NotEquals);
                    }
                    else
                    {
                        AddToken(Token.Less);
                    }
                }
                else if (_char == ',')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.Comma);
                }
                else if (_char == ';')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.EndOfStatement);
                }
                else if (_char == '[')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.OpenSquareBracket);
                }
                else if (_char == ']')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.CloseSquareBracket);
                }
                else if (_char == '(')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.OpenRoundBracket);
                }
                else if (_char == ')')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(Token.CloseRoundBracket);
                }
                else if (_char == '{')
                {
                    Entity();
                }
                else if (_char == '\'')
                {
                    SingleQuotedString();
                }
                else if (_char == '"')
                {
                    DoubleQuotedString();
                }
                else if (LexerHelper.IsNumeric(_char))
                {
                    if (_char == '0' && CheckNext('x'))
                    {
                        Binary();
                    }
                    else
                    {
                        Number();
                    }
                }
                else if (_char == '@')
                {
                    Variable();
                }
                else if (LexerHelper.IsAlphaNumeric(_char))
                {
                    Identifier();
                }
                else
                {
                    throw new Exception(GetErrorText("Unexpected character"));
                }
            }
        }
        private bool Consume()
        {
            int consumed = _reader.Read();

            if (consumed == -1)
            {
                return false;
            }

            _position++;

            _char = (char)consumed;
            
            return true;
        }
        private bool Consume(char expected)
        {
            char next = PeekNext();

            if (next == char.MinValue)
            {
                return false;
            }

            if (next == expected)
            {
                return Consume();
            }

            return false;
        }
        private char PeekNext()
        {
            int next = _reader.Peek();

            if (next == -1)
            {
                return char.MinValue;
            }

            return (char)next;
        }
        private bool CheckNext(char expected)
        {
            return (PeekNext() == expected);
        }
        private void AddToken(Token token)
        {
            _tokens.Add(new Lexeme(token)
            {
                Line = _line,
                Offset = _start,
                Length = _position - _start + 1,
                Value = _lexeme.ToString()
            });

            _lexeme.Clear();
        }
        
        private void SingleLineComment()
        {
            //_start = _position;
            _lexeme.Append(_char);

            while (PeekNext() != '\n' && Consume())
            {
                _lexeme.Append(_char); // read comment to the end of line
            }

            AddToken(Token.Comment);
        }
        private void MultiLineComment()
        {
            //_start = _position;
            _lexeme.Append(_char);

            while (PeekNext() != '*' && Consume())
            {
                // read comment until * is met

                if (_char == '\n')
                {
                    _line++; // process new line
                }
                else
                {
                    _lexeme.Append(_char);
                }
            }

            // the * is met
            _ = Consume(); // consume *

            _lexeme.Append(_char);

            if (_char != '*') // end of script
            {
                throw new Exception(GetErrorText("Unterminated comment"));
            }

            if (!Consume('/'))
            {
                throw new Exception(GetErrorText("Unexpected character"));
            }
            else
            {
                _lexeme.Append(_char);
            }

            AddToken(Token.Comment);
        }
        private void SingleQuotedString()
        {
            _start = _position;
            //_lexeme.Append(_char); do not include quotation mark

            while (PeekNext() != '\'' && Consume())
            {
                // read string literal until ' is met

                if (_char == '\n')
                {
                    _line++; // process new line
                }
                else
                {
                    _lexeme.Append(_char);
                }
            }

            // the ' is met
            if (Consume())
            {
                //_lexeme.Append(_char); do not include quotation mark
            }

            if (_char != '\'') // end of script
            {
                throw new Exception(GetErrorText("Unterminated string"));
            }

            AddToken(Token.String);
        }
        private void DoubleQuotedString()
        {
            _start = _position;
            //_lexeme.Append(_char); do not include quotation mark

            while (PeekNext() != '\"' && Consume())
            {
                // read string literal until " is met

                if (_char == '\n')
                {
                    // process new line
                    _line++;
                }
                else
                {
                    _lexeme.Append(_char);
                }
            }

            // the " is met
            if (Consume())
            {
                //_lexeme.Append(_char); do not include quotation mark
            }

            if (_char != '\"') // end of script
            {
                throw new Exception(GetErrorText("Unterminated string"));
            }

            AddToken(Token.String);
        }
        private void Number()
        {
            _start = _position;
            _lexeme.Append(_char);

            while (LexerHelper.IsNumeric(PeekNext()) && Consume())
            {
                _lexeme.Append(_char); // read number literal
            }

            if (Consume('.'))
            {
                if (!LexerHelper.IsNumeric(PeekNext()))
                {
                    throw new Exception(GetErrorText("Unexpected character"));
                }

                _lexeme.Append(_char);

                while (LexerHelper.IsNumeric(PeekNext()) && Consume())
                {
                    _lexeme.Append(_char); // consume digits - fractional part
                }
            }

            AddToken(Token.Number);
        }
        private void Binary()
        {
            _start = _position;
            _lexeme.Append(_char);

            if (!Consume('x'))
            {
                throw new Exception(GetErrorText("Unexpected character"));
            }

            _lexeme.Append(_char);

            while (LexerHelper.IsHexadecimal(PeekNext()) && Consume())
            {
                _lexeme.Append(_char); // read hex literal
            }

            AddToken(Token.Binary);
        }
        private void Entity()
        {
            _start = _position;
            _lexeme.Append(_char);

            while (PeekNext() != '}' && Consume())
            {
                _lexeme.Append(_char);  // read Entity literal until } is met
            }

            // the } is met
            if (Consume())
            {
                _lexeme.Append(_char);
            }

            if (_char != '}') // end of script
            {
                throw new Exception(GetErrorText("Unterminated Entity literal"));
            }

            AddToken(Token.Entity);
        }
        private void Identifier()
        {
            _start = _position;
            _lexeme.Append(_char);

            char next = PeekNext();

            while (LexerHelper.IsAlphaNumeric(next) && Consume())
            {
                // read identifier
                _lexeme.Append(_char);
                next = PeekNext();
            }

            string test = _lexeme.ToString();

            if (LexerHelper.IsNullLiteral(test))
            {
                AddToken(Token.NULL);
            }
            else if (LexerHelper.IsBooleanLiteral(test))
            {
                AddToken(Token.Boolean);
            }
            else if (LexerHelper.IsKeyword(test, out Token token))
            {
                AddToken(token);
            }
            else
            {
                AddToken(Token.Identifier);
            }
        }
        private void Variable()
        {
            _start = _position;
            _lexeme.Append(_char);

            if (Consume('@')) // double @@
            {
                _lexeme.Append(_char);
            }

            char next = PeekNext();

            while (LexerHelper.IsAlphaNumeric(next) && Consume())
            {
                _lexeme.Append(_char); // read identifier

                next = PeekNext();
            }

            AddToken(Token.Variable);
        }
    }
}