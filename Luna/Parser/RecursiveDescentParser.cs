using System;
using System.Collections.Generic;
using System.Linq;
using Luna.Ast;

namespace Luna.Parser
{
    struct Precedence
    {
        public readonly int Level;
        public readonly bool IsLeft;

        public Precedence(int level, bool isLeft)
        {
            Level = level;
            IsLeft = isLeft;
        }

        public Precedence Passalong
        {
            get { return new Precedence(IsLeft ? Level : Level - 1, IsLeft); }
        }

        public static readonly Precedence NullPrecedence = new Precedence(-1, true);
    }

    class ParseState
    {
        public readonly Dictionary<string, Precedence> Precedence;
        public bool Remaining;

        public ParseState(Dictionary<string, Precedence> precedence)
        {
            Precedence = precedence;
            Remaining = true;
        }
    }

    static class RecursiveDescentParser
    {
        private static readonly string[] ReservedSymbols = { ";", "=", "::" };

        private static IEnumerable<T> Print<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.Select(t =>
            {
                Console.WriteLine(t);
                return t;
            });
        }

        public static Module<Terminal> Parse(string contents)
        {
            var stream = new TokenStream(contents).Print().GetEnumerator();
            var state = new ParseState(new Dictionary<string, Precedence>());
            state.Remaining = stream.MoveNext();

            if (state.Remaining == false || stream.Current.Type != TokenType.Identifier || stream.CurrentMoveNext(state).Value != "module")
                throw new Exception("Expected `module`");

            var parts = ParseModule(stream, state);
            if (state.Remaining)
                throw new Exception("Unexpected token " + stream.Current);
            return parts;
        }

        private static Module<Terminal> ParseModule(IEnumerator<Terminal> stream, ParseState state)
        {
            if (state.Remaining == false)
                throw new Exception("Expected module name");
            var moduleName = stream.CurrentMoveNext(state);
            var moduleDefs = ParseModuleParts(stream, state);
            return new Module<Terminal>(new[] { moduleName.Value }, moduleDefs);
        }

        private static IModulePart<Terminal>[] ParseModuleParts(IEnumerator<Terminal> stream, ParseState state)
        {
            var moduleDefs = new List<IModulePart<Terminal>>();
            while (state.Remaining)
            {
                if (stream.Current.Type == TokenType.Symbol && stream.Current.Value == ";")
                {
                    stream.CurrentMoveNext(state);
                    return moduleDefs.ToArray();
                }
                if (stream.Current.Type == TokenType.Identifier)
                {
                    var iden = stream.Current;
                    var success = true;
                    switch (iden.Value)
                    {
                        case "infixl":
                            stream.CurrentMoveNext(state);
                            moduleDefs.Add(ParseFixity(stream, state, true));
                            break;
                        case "infixr":
                            stream.CurrentMoveNext(state);
                            moduleDefs.Add(ParseFixity(stream, state, false));
                            break;
                        case "module":
                            stream.CurrentMoveNext(state);
                            moduleDefs.Add(ParseModule(stream, state));
                            break;
                        case "import":
                            stream.CurrentMoveNext(state);
                            moduleDefs.Add(ParseImport(stream, state));
                            break;
                        default:
                            success = false;
                            break;
                    }
                    if (success)
                        continue;
                }
                moduleDefs.Add(ParseDefinition(stream, state));
            }
            throw new Exception("Expected semicolon");
        }

        private static ImportDeclaration<Terminal> ParseImport(IEnumerator<Terminal> stream, ParseState state)
        {
            if (state.Remaining == false)
                throw new Exception("Expected import name");
            var token = stream.CurrentMoveNext(state);
            if (token.Type != TokenType.Identifier)
                throw new Exception("Expected identifier");
            return new ImportDeclaration<Terminal>(token.Value);
        }

        private static FixityDefinition<Terminal> ParseFixity(IEnumerator<Terminal> stream, ParseState state, bool isLeft)
        {
            if (state.Remaining == false)
                throw new Exception("Expected number");
            var precedence = stream.CurrentMoveNext(state);
            int realPrec;
            if (precedence.Type != TokenType.Number || int.TryParse(precedence.Value, out realPrec) == false)
                throw new Exception("Expected number");
            if (state.Remaining == false)
                throw new Exception("Expected operator");
            var op = stream.CurrentMoveNext(state);
            if (op.Type != TokenType.Symbol)
                throw new Exception("Expected operator");
            state.Precedence[op.Value] = new Precedence(realPrec, isLeft);
            return new FixityDefinition<Terminal>(op.Value, isLeft, realPrec);
        }

        private static IModulePart<Terminal> ParseDefinition(IEnumerator<Terminal> stream, ParseState state)
        {
            if (state.Remaining == false)
                throw new Exception("Expected assignment operator");
            var arguments = ParseExpression(stream, state);
            var assignmentTerminal = stream.CurrentMoveNext(state);
            if (assignmentTerminal.Type != TokenType.Symbol)
                throw new Exception("Expected assignment operator");
            var expr = ParseExpression(stream, state);
            if (expr == null)
                throw new Exception("Expected expression");
            if (stream.Current.Type != TokenType.Symbol)
                throw new Exception("Expected semicolon");
            if (stream.CurrentMoveNext(state).Value != ";")
                throw new Exception("Expected semicolon");
            var tuple = PatternmatchGenenerator.Run(arguments, expr);
            switch (assignmentTerminal.Value)
            {
                case "=":
                    return new Definition<Terminal>(tuple.Item1, tuple.Item2);
                case "::":
                    return new TypeDefinition<Terminal>(tuple.Item1, tuple.Item2);
                default:
                    throw new Exception("Expected assignment operator");
            }
        }

        private static IDynAst<Terminal> ParseExpression(IEnumerator<Terminal> stream, ParseState state)
        {
            return ParseOperator(stream, state, Precedence.NullPrecedence);
        }

        // Pratt parser
        private static IDynAst<Terminal> ParseOperator(IEnumerator<Terminal> stream, ParseState state, Precedence minPrecedence)
        {
            if (state.Remaining == false)
                return null;
            var current = ParsePrimary(stream, state);
            if (current == null)
                return null;
            while (state.Remaining && stream.Current.Type == TokenType.Symbol && ReservedSymbols.Contains(stream.Current.Value) == false)
            {
                var op = stream.CurrentMoveNext(state);
                if (state.Precedence.ContainsKey(op.Value) == false)
                    throw new Exception("Unknown operator " + op);
                var precedence = state.Precedence[op.Value];
                if (minPrecedence.Level >= precedence.Level)
                    break;
                current = MakeBinaryOperator(op, current, ParseOperator(stream, state, precedence.Passalong));
            }
            return current;
        }

        private static IDynAst<Terminal> MakeBinaryOperator(Terminal op, IDynAst<Terminal> left, IDynAst<Terminal> right)
        {
            return new Application<Terminal>(new Application<Terminal>(MakeIden(op), left), right);
        }

        private static IDynAst<Terminal> ParsePrimary(IEnumerator<Terminal> stream, ParseState state)
        {
            var current = ParsePrimaryNoAp(stream, state);
            if (current == null)
                return null;
            while (true)
            {
                var next = ParsePrimaryNoAp(stream, state);
                if (next == null)
                    break;
                current = new Application<Terminal>(current, next);
            }
            return current;
        }

        private static IDynAst<Terminal> ParsePrimaryNoAp(IEnumerator<Terminal> stream, ParseState state)
        {
            if (state.Remaining == false)
                return null;
            var current = stream.Current;
            switch (current.Type)
            {
                case TokenType.GroupSymbol:
                    if (current.Value == "(")
                    {
                        stream.CurrentMoveNext(state);
                        return ParseParens(stream, state);
                    }
                    return null;
                case TokenType.Symbol:
                    if (current.Value == "\\")
                    {
                        stream.CurrentMoveNext(state);
                        if (state.Remaining == false)
                            throw new Exception("Expected lambda identifier");
                        var idens = new List<Terminal>();
                        while (stream.Current.Type == TokenType.Identifier)
                            idens.Add(stream.CurrentMoveNext(state));
                        if (stream.Current.Type != TokenType.Symbol || stream.CurrentMoveNext(state).Value != "->")
                            throw new Exception("Expected arrow");
                        var body = ParseExpression(stream, state);
                        idens.Reverse();
                        return idens.Aggregate(body, (expr, param) => new Lambda<Terminal>(param, expr));
                    }
                    return null;
                case TokenType.Identifier:
                    return MakeIden(stream.CurrentMoveNext(state));
                case TokenType.Number:
                    long lval;
                    double dval;
                    var litval = stream.CurrentMoveNext(state).Value;
                    if (long.TryParse(litval, out lval))
                        return new Literal<Terminal>(lval);
                    if (double.TryParse(litval, out dval))
                        return new Literal<Terminal>(dval);
                    throw new Exception("Could not parse number");
                case TokenType.String:
                    return new Literal<Terminal>(stream.CurrentMoveNext(state).Value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static IDynAst<Terminal> ParseParens(IEnumerator<Terminal> stream, ParseState state)
        {
            if (state.Remaining == false)
            {
                throw new Exception("Expected value");
            }
            IDynAst<Terminal> value;
            if (stream.Current.Type == TokenType.Symbol && ReservedSymbols.Contains(stream.Current.Value) == false)
            {
                var symbol = stream.CurrentMoveNext(state);
                if (stream.Current.Type == TokenType.GroupSymbol && stream.Current.Value == ")")
                {
                    stream.CurrentMoveNext(state);
                    return MakeIden(symbol); // (+) = (+)
                }

                value = ParseExpression(stream, state);

                if (stream.Current.Type == TokenType.Symbol && ReservedSymbols.Contains(stream.Current.Value) == false)
                    throw new Exception("Double operator sections are not allowed"); // (+ foo +)

                if (state.Remaining == false || stream.CurrentMoveNext(state).Value != ")")
                    throw new Exception("Expected closing parenthases");

                var sectionArg = new Terminal(TokenType.Identifier, "opSectionArg#");
                value = new Lambda<Terminal>(sectionArg,
                    new Application<Terminal>(
                        new Application<Terminal>(MakeIden(symbol), MakeIden(sectionArg)),
                        value)); // (+ x) = \y -> (+) y x

                return value;
            }
            value = ParseExpression(stream, state);

            if (stream.Current.Type == TokenType.Symbol && ReservedSymbols.Contains(stream.Current.Value) == false)
                value = new Application<Terminal>(MakeIden(stream.CurrentMoveNext(state)), value); // (x +) = (+) x

            if (state.Remaining == false || stream.CurrentMoveNext(state).Value != ")")
                throw new Exception("Expected closing parenthases");
            return value;
        }

        private static IDynAst<Terminal> MakeIden(Terminal terminal)
        {
            //if (char.IsUpper(terminal.Value[0]) || terminal.Value[0] == ':' || terminal.Value == "->")
            //    return new Literal<Terminal>(new Identifier<Terminal>(terminal));
            return new Identifier<Terminal>(terminal);
        }

        private static Terminal CurrentMoveNext(this IEnumerator<Terminal> stream, ParseState state)
        {
            var prev = stream.Current;
            state.Remaining = stream.MoveNext();
            return prev;
        }

        class PatternmatchGenenerator : IDynAstVisitor<Terminal, IDynAst<Terminal>, Tuple<Identifier<Terminal>, IDynAst<Terminal>>>
        {
            private static readonly PatternmatchGenenerator Fetch = new PatternmatchGenenerator();

            private PatternmatchGenenerator()
            {
            }

            public static Tuple<Identifier<Terminal>, IDynAst<Terminal>> Run(IDynAst<Terminal> ast, IDynAst<Terminal> innerExpression)
            {
                return ast.Visit(Fetch, innerExpression);
            }

            public Tuple<Identifier<Terminal>, IDynAst<Terminal>> Accept(Lambda<Terminal> value, IDynAst<Terminal> data)
            {
                throw new Exception("Cannot have lambda in a pattern match");
            }

            public Tuple<Identifier<Terminal>, IDynAst<Terminal>> Accept(Forall<Terminal> value, IDynAst<Terminal> data)
            {
                throw new Exception("Illegal forall in pattern match");
            }

            public Tuple<Identifier<Terminal>, IDynAst<Terminal>> Accept(Application<Terminal> value, IDynAst<Terminal> data)
            {
                var identifier = value.Value as Identifier<Terminal>;
                if (identifier == null)
                    throw new Exception("Argument to function must be an identifier");
                data = new Lambda<Terminal>(identifier.Value, data);
                return value.Function.Visit(this, data);
            }

            public Tuple<Identifier<Terminal>, IDynAst<Terminal>> Accept(Identifier<Terminal> value, IDynAst<Terminal> data)
            {
                return Tuple.Create(value, data);
            }

            public Tuple<Identifier<Terminal>, IDynAst<Terminal>> Accept(Literal<Terminal> value, IDynAst<Terminal> data)
            {
                throw new Exception("Value-based pattern matching is not supported");
            }
        }
    }
}
