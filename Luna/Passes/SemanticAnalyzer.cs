using System;
using System.Collections.Generic;
using System.Linq;
using Luna.Ast;
using Luna.Parser;

namespace Luna.Passes
{
    static class SemanticUtils
    {
        public static string QualifyStr(this Identifier<Terminal> terminal, string[] moduleName)
        {
            return string.Join(".", terminal.Qualify(moduleName));
        }

        public static string QualifyStr(this Terminal terminal, string[] moduleName)
        {
            return string.Join(".", terminal.Qualify(moduleName));
        }

        public static string[] Qualify(this Identifier<Terminal> terminal, string[] moduleName)
        {
            return terminal.Value.Qualify(moduleName);
        }

        public static string[] Qualify(this Terminal terminal, string[] moduleName)
        {
            return moduleName.Append(terminal.Value);
        }
    }

    struct ModuleDefinitionBinderState
    {
        public readonly Dictionary<string, UniqueBinder> Scope;
        public readonly string[] CurrentModule;

        public ModuleDefinitionBinderState(Dictionary<string, UniqueBinder> scope, string[] currentModule)
            : this()
        {
            Scope = scope;
            CurrentModule = currentModule;
        }
    }

    class ModuleDefinitionBinder
        : IModulePartVisitor<Terminal, ModuleDefinitionBinderState, object>
    {
        private static readonly ModuleDefinitionBinder Fetch = new ModuleDefinitionBinder();

        private ModuleDefinitionBinder()
        {
        }

        public static Dictionary<string, UniqueBinder> Run(IModulePart<Terminal> module)
        {
            var mdbs = new ModuleDefinitionBinderState(new Dictionary<string, UniqueBinder>(), new string[0]);
            module.Visit(Fetch, mdbs);
            return mdbs.Scope;
        }

        public object Accept(Module<Terminal> value, ModuleDefinitionBinderState data)
        {
            var modName = data.CurrentModule.Concat(value.ModuleName).ToArray();
            var newData = new ModuleDefinitionBinderState(data.Scope, modName);
            foreach (var modulePart in value.Parts)
                modulePart.Visit(Fetch, newData);
            return null;
        }

        public object Accept(FixityDefinition<Terminal> value, ModuleDefinitionBinderState data)
        {
            return null;
        }

        public object Accept(TypeDefinition<Terminal> value, ModuleDefinitionBinderState data)
        {
            return null;
        }

        public object Accept(Definition<Terminal> value, ModuleDefinitionBinderState data)
        {
            data.Scope[value.Name.QualifyStr(data.CurrentModule)] = new UniqueBinder(new object(), value.Name.Qualify(data.CurrentModule));
            return null;
        }

        public object Accept(ImportDeclaration<Terminal> value, ModuleDefinitionBinderState data)
        {
            // TODO: Find all files with possible name and add their contents to scope
            return null;
        }
    }

    struct SemanticAnalyzerState
    {
        //private static readonly string[] BuiltinBinders = {"->", "Int"};
        public readonly Dictionary<string, UniqueBinder> Bindings;
        public readonly string[] CurrentModule;
        public readonly List<string[]> Imports;

        public SemanticAnalyzerState(Dictionary<string, UniqueBinder> bindings, string[] currentModule, List<string[]> imports)
        {
            Bindings = bindings;
            CurrentModule = currentModule;
            Imports = imports;
        }

        public IDynAst<UniqueBinder> Lookup(Terminal terminal)
        {
            var localBindings = Bindings;
            var possibilities = Imports.Select(import => terminal.QualifyStr(import)).Where(localBindings.ContainsKey).Select(s => localBindings[s]).ToArray();
            if (possibilities.Length == 1)
                return new Identifier<UniqueBinder>(possibilities[0]);
            if (possibilities.Length == 0)
            {
                var builtinBinder = SemanticAnalyzer.GetBuiltinBinder(terminal.Value);
                if (builtinBinder.UniqueId != null) // from Default value
                    return new Literal<UniqueBinder>(new Identifier<UniqueBinder>(builtinBinder));
                throw new Exception("Unidentified terminal " + terminal);
            }
            throw new Exception("Ambiguous identifier: possibilities are " + string.Join(", ", possibilities.Select(p => string.Join(".", p))));
        }
    }

    class SemanticAnalyzer :
        IDynAstVisitor<Terminal, SemanticAnalyzerState, IDynAst<UniqueBinder>>,
        IModulePartVisitor<Terminal, SemanticAnalyzerState, IModulePart<UniqueBinder>>
    {
        public static readonly List<UniqueBinder> BuiltinBinders = new List<UniqueBinder>
        {
            new UniqueBinder(new object(), new[]{"->"}),
            new UniqueBinder(new object(), new[]{"Int"}),
        };

        public static UniqueBinder GetBuiltinBinder(string name)
        {
            return BuiltinBinders.FirstOrDefault(x => string.Join(".", x.FullName) == name);
        }

        private static readonly SemanticAnalyzer Fetch = new SemanticAnalyzer();

        private SemanticAnalyzer()
        {
        }

        public static IModulePart<UniqueBinder> Run(IModulePart<Terminal> ast)
        {
            var bindings = ModuleDefinitionBinder.Run(ast);
            return ast.Visit(Fetch, new SemanticAnalyzerState(bindings, new string[0], new List<string[]>()));
        }

        public IModulePart<UniqueBinder> Accept(Module<Terminal> value, SemanticAnalyzerState data)
        {
            var modName = data.CurrentModule.Concat(value.ModuleName).ToArray();
            var newData = new SemanticAnalyzerState(data.Bindings, modName, new List<string[]> { new[] { "_" }, modName }); // TODO: Add prelude
            var parts = value.Parts.Select(part => part.Visit(this, newData)).ToArray();
            return new Module<UniqueBinder>(modName, parts);
        }

        public IModulePart<UniqueBinder> Accept(FixityDefinition<Terminal> value, SemanticAnalyzerState data)
        {
            return new FixityDefinition<UniqueBinder>(value.Symbol, value.IsLeft, value.Precedence);
        }

        public IModulePart<UniqueBinder> Accept(TypeDefinition<Terminal> value, SemanticAnalyzerState data)
        {
            var name = data.Bindings[value.Name.QualifyStr(data.CurrentModule)];
            var expr = value.Type.Visit(this, data);
            return new TypeDefinition<UniqueBinder>(new Identifier<UniqueBinder>(name), expr);
        }

        public IModulePart<UniqueBinder> Accept(Definition<Terminal> value, SemanticAnalyzerState data)
        {
            // should already be in because of ModuleDefinitionBinder
            var name = data.Bindings[value.Name.QualifyStr(data.CurrentModule)];
            var expr = value.Expression.Visit(this, data);
            return new Definition<UniqueBinder>(new Identifier<UniqueBinder>(name), expr);
        }

        public IModulePart<UniqueBinder> Accept(ImportDeclaration<Terminal> value, SemanticAnalyzerState data)
        {
            return new ImportDeclaration<UniqueBinder>(value.Import);
        }

        public IDynAst<UniqueBinder> Accept(Lambda<Terminal> value, SemanticAnalyzerState data)
        {
            var module = new[] { "_" };
            var fullName = value.Name.Qualify(module);
            var fullNameStr = value.Name.QualifyStr(module);
            var paramName = new UniqueBinder(new object(), fullName);
            var newBinding = new Dictionary<string, UniqueBinder>(data.Bindings) { { fullNameStr, paramName } };
            var newdata = new SemanticAnalyzerState(newBinding, data.CurrentModule, data.Imports);
            var newBody = value.Body.Visit(this, newdata);
            return new Lambda<UniqueBinder>(paramName, newBody);
        }

        public IDynAst<UniqueBinder> Accept(Forall<Terminal> value, SemanticAnalyzerState data)
        {
            throw new Exception("Illegal forall in pre-typechecked expression");
        }

        public IDynAst<UniqueBinder> Accept(Application<Terminal> value, SemanticAnalyzerState data)
        {
            return new Application<UniqueBinder>(value.Function.Visit(this, data), value.Value.Visit(this, data));
        }

        public IDynAst<UniqueBinder> Accept(Identifier<Terminal> value, SemanticAnalyzerState data)
        {
            return data.Lookup(value.Value);
        }

        public IDynAst<UniqueBinder> Accept(Literal<Terminal> value, SemanticAnalyzerState data)
        {
            var type = value.AsTypeLit<Terminal>();
            return type == null ? new Literal<UniqueBinder>(value.Value) : new Literal<UniqueBinder>(type.Visit(this, data));
        }
    }
}