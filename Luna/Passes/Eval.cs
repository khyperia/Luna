using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Luna.Ast;
using Luna.Parser;

namespace Luna.Passes
{
    static class EvalTerminal
    {
        public static object Run(IModulePart<Terminal> moduleParts, string main)
        {
            return Eval<Terminal>.Run(moduleParts, (_, x) => x, terminal => terminal.Value == main, PrimopBuilder<Terminal>.Get(s => new Terminal(TokenType.Identifier, s)));
        }
    }

    static class EvalUniqueBinder
    {
        public static object Run(IModulePart<UniqueBinder> moduleParts, string main)
        {
            return Eval<UniqueBinder>.Run(moduleParts, (_, x) => x, terminal => string.Join(".", terminal.FullName) == main,
                PrimopBuilder<UniqueBinder>.Get(s => new UniqueBinder(new object(), new[] { "Prelude", s })));
        }
    }

    static class PrimopBuilder<TBinder>
    {
        private static Dictionary<TBinder, object> _primops;

        public static Dictionary<TBinder, object> Get(Func<string, TBinder> lifter)
        {
            if (_primops != null)
                return _primops;

            _primops = new Dictionary<TBinder, object>();
            foreach (var method in typeof(Primops).GetMethods(BindingFlags.Public | BindingFlags.Static).GroupBy(m => m.Name))
            {
                var paramSize = -1;
                foreach (var methodInfo in method)
                {
                    if (paramSize == -1)
                        paramSize = methodInfo.GetParameters().Length;
                    else if (methodInfo.GetParameters().Length != paramSize)
                        throw new Exception("Primop " + methodInfo.Name + " has differing parameter arguments");
                }
                var methodName = method.Key;
                Func<object[], Lazy<object>> obj = args => new Lazy<object>(() => typeof(Primops)
                    .GetMethod(methodName, args.Select(o => o.GetType()).ToArray()).Invoke(null, args));
                obj = Enumerable.Range(0, paramSize).Aggregate(obj, (current, parameterInfo) =>
                    (args => new Lazy<object>(() => new Func<Lazy<object>, Lazy<object>>(nextArg => current(args.Append(nextArg.Value))))));
                _primops.Add(lifter(methodName), obj(new object[0]));
            }

            return _primops;
        }
    }

    static class Primops
    {
        public static double Add(double left, double right)
        {
            return left + right;
        }
        public static long Add(long left, long right)
        {
            return left + right;
        }
    }

    static class Eval<TBinder>
    {
        public static object Run(IModulePart<TBinder> moduleParts, Func<string[], TBinder, TBinder> nameConcatenator, Func<TBinder, bool> mainSelector, IReadOnlyDictionary<TBinder, object> builtins)
        {
            var environment = EvalCollect<TBinder>.Run(moduleParts, nameConcatenator);
            var main = environment.First(env => mainSelector(env.Key)).Value;
            var memoizedRuns = new Dictionary<TBinder, Lazy<object>>();
            return EvalExec<TBinder>.Run(main, LookupBinder(memoizedRuns, environment, builtins)).Value;
        }

        private static Func<TBinder, Lazy<object>> LookupBinder(IDictionary<TBinder, Lazy<object>> memoizedRuns, IReadOnlyDictionary<TBinder, IDynAst<TBinder>> environment, IReadOnlyDictionary<TBinder, object> builtins)
        {
            return binder =>
            {
                if (memoizedRuns.ContainsKey(binder))
                {
                    return memoizedRuns[binder];
                }
                Lazy<object> run;
                if (environment.ContainsKey(binder) == false)
                {
                    if (builtins.ContainsKey(binder) == false)
                        throw new Exception("Symbol " + binder + " not found");
                    run = new Lazy<object>(() => builtins[binder]);
                }
                else
                    run = new Lazy<object>(() => EvalExec<TBinder>.Run(environment[binder], LookupBinder(memoizedRuns, environment, builtins)).Value);
                memoizedRuns.Add(binder, run);
                return run;
            };
        }
    }

    class EvalCollect<TBinder> : IModulePartVisitor<TBinder, Func<string[], TBinder, TBinder>, Dictionary<TBinder, IDynAst<TBinder>>>
    {
        private static readonly EvalCollect<TBinder> Fetch = new EvalCollect<TBinder>();

        private EvalCollect()
        {
        }

        public static Dictionary<TBinder, IDynAst<TBinder>> Run(IModulePart<TBinder> ast, Func<string[], TBinder, TBinder> nameConcatenator)
        {
            return ast.Visit(Fetch, nameConcatenator);
        }

        public Dictionary<TBinder, IDynAst<TBinder>> Accept(Module<TBinder> value, Func<string[], TBinder, TBinder> data)
        {
            return value.Parts
                .SelectMany(part => part.Visit(this, data))
                .ToDictionary(kvp => data(value.ModuleName, kvp.Key), kvp => kvp.Value);
        }

        public Dictionary<TBinder, IDynAst<TBinder>> Accept(FixityDefinition<TBinder> value, Func<string[], TBinder, TBinder> data)
        {
            return new Dictionary<TBinder, IDynAst<TBinder>>();
        }

        public Dictionary<TBinder, IDynAst<TBinder>> Accept(TypeDefinition<TBinder> value, Func<string[], TBinder, TBinder> data)
        {
            return new Dictionary<TBinder, IDynAst<TBinder>>();
        }

        public Dictionary<TBinder, IDynAst<TBinder>> Accept(Definition<TBinder> value, Func<string[], TBinder, TBinder> data)
        {
            return new Dictionary<TBinder, IDynAst<TBinder>> { { value.Name.Value, value.Expression } };
        }

        public Dictionary<TBinder, IDynAst<TBinder>> Accept(ImportDeclaration<TBinder> value, Func<string[], TBinder, TBinder> data)
        {
            return new Dictionary<TBinder, IDynAst<TBinder>>();
        }
    }

    class EvalExec<TBinder> : IDynAstVisitor<TBinder, Func<TBinder, Lazy<object>>, Lazy<object>>
    {
        private static readonly EvalExec<TBinder> Fetch = new EvalExec<TBinder>();

        private EvalExec()
        {
        }

        public static Lazy<object> Run(IDynAst<TBinder> ast, Func<TBinder, Lazy<object>> environment)
        {
            return ast.Visit(Fetch, environment);
        }

        public Lazy<object> Accept(Lambda<TBinder> value, Func<TBinder, Lazy<object>> data)
        {
            var name = value.Name;
            Func<Lazy<object>, Lazy<object>> func = param => value.Body.Visit(this, lookup => lookup.Equals(name) ? param : data(lookup));
            return new Lazy<object>(() => func);
        }

        public Lazy<object> Accept(Forall<TBinder> value, Func<TBinder, Lazy<object>> data)
        {
            throw new Exception("Illegal forall in expression");
        }

        public Lazy<object> Accept(Application<TBinder> value, Func<TBinder, Lazy<object>> data)
        {
            var function = value.Function.Visit(this, data);
            var argument = value.Value.Visit(this, data);
            return ((Func<Lazy<object>, Lazy<object>>)function.Value)(argument);
        }

        public Lazy<object> Accept(Identifier<TBinder> value, Func<TBinder, Lazy<object>> data)
        {
            return data(value.Value);
        }

        public Lazy<object> Accept(Literal<TBinder> value, Func<TBinder, Lazy<object>> data)
        {
            // TODO: Check for AsTypeLit
            return new Lazy<object>(() => value.Value);
        }
    }
}
