using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Luna.Parser
{
    struct Terminal
    {
        public readonly TokenType Type;
        public readonly string Value;

        public Terminal(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return string.Format("{0}: \"{1}\"", Type, Value);
        }

        public bool Equals(Terminal other)
        {
            return Type == other.Type && string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Terminal && Equals((Terminal)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Type * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }
    }

    enum TokenType
    {
        Errtok,
        GroupSymbol,
        Symbol,
        Identifier,
        Number,
        String
    }

    class TokenStream : IEnumerable<Terminal>
    {
        private const string AllowedOperators = "`~!@#$%^&*_-+=|\\:;\"'<,>.?/";
        private const string GroupOperators = "(){}[]";

        private static readonly Dictionary<char, char> StringEscapes = new Dictionary<char, char>
        {
            {'n', '\n'},
            {'r', '\r'},
            {'t', '\t'},
            {'"', '"'},
            {'\'', '\''},
            {'\\', '\\'}
        };

        private readonly string _source;

        public TokenStream(string source)
        {
            _source = source;
        }

        private IEnumerable<Terminal> All()
        {
            var index = 0;
            TakeWhitespace(ref index);
            while (index < _source.Length)
            {
                yield return Advance(ref index);
                TakeWhitespace(ref index);
            }
        }

        public IEnumerator<Terminal> GetEnumerator()
        {
            return All().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private Terminal Advance(ref int index)
        {
            var start = index;
            TokenType type;
            if (char.IsDigit(_source, index))
            {
                while (index < _source.Length && char.IsDigit(_source, index))
                    index++;
                if (index < _source.Length && _source[index] == '.')
                {
                    index++;
                    while (index < _source.Length && char.IsDigit(_source, index))
                        index++;
                }
                if (index < _source.Length && (_source[index] == 'e' || _source[index] == 'E'))
                {
                    index++;
                    while (index < _source.Length && char.IsDigit(_source, index))
                        index++;
                }
                type = TokenType.Number;
            }
            else if (char.IsLetter(_source, index))
            {
                while (index < _source.Length && char.IsLetterOrDigit(_source, index))
                    index++;
                type = TokenType.Identifier;
            }
            else if (_source[index] == '"')
            {
                index++;
                while (index < _source.Length && "\"\r\n".IndexOf(_source[index]) == -1)
                {
                    if (_source[index] == '\\')
                    {
                        index++;
                        if (index >= _source.Length)
                            break;
                        if (StringEscapes.ContainsKey(_source[index]) == false)
                            throw new Exception("Unrecognized escape sequence '\\" + _source[index] + "'");
                    }
                    index++;
                }
                if (index < _source.Length && _source[index] == '"')
                    index++;
                else
                    throw new Exception("Unterminated string");
                type = TokenType.String;
            }
            else if (GroupOperators.IndexOf(_source[index]) != -1)
            {
                index++;
                type = TokenType.GroupSymbol;
            }
            else if (AllowedOperators.IndexOf(_source[index]) != -1)
            {
                while (index < _source.Length && AllowedOperators.IndexOf(_source[index]) != -1)
                    index++;
                type = TokenType.Symbol;
            }
            else
                throw new Exception("Unexpected character '" + _source[index] + "'");
            var value = _source.Substring(start, index - start);
            if (type == TokenType.String)
                value = StringEscapes.Aggregate(value.Substring(1, value.Length - 2), (val, kvp) => val.Replace("\\" + kvp.Key, char.ToString(kvp.Value)));
            var terminal = new Terminal(type, value);
            return terminal;
        }

        private void TakeWhitespace(ref int index)
        {
            while (index < _source.Length && char.IsWhiteSpace(_source, index))
                index++;
        }
    }
}
