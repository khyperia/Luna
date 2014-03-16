using System;
using System.Collections.Generic;
using System.Linq;
using Luna.Ast;

namespace Luna.Passes
{
    struct TypedBinder<TBinder>
    {
        public readonly TBinder Name;
        public readonly IDynAst<TBinder> Type;

        public TypedBinder(TBinder name, IDynAst<TBinder> type)
            : this()
        {
            Name = name;
            Type = type;
        }

        public bool Equals(TypedBinder<TBinder> other)
        {
            return EqualityComparer<TBinder>.Default.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TypedBinder<TBinder> && Equals((TypedBinder<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<TBinder>.Default.GetHashCode(Name);
        }

        public override string ToString()
        {
            return Name + "::" + Type;
        }
    }

    struct TypecheckAssignPassState<TBinder>
    {
        public readonly Dictionary<TBinder, IDynAst<TBinder>> Environment;
        public readonly Func<TBinder> UniqueGenerator;
        public readonly Func<string, TBinder> NamedGenerator;

        public TypecheckAssignPassState(Dictionary<TBinder, IDynAst<TBinder>> environment, Func<TBinder> uniqueGenerator, Func<string, TBinder> namedGenerator)
        {
            Environment = environment;
            UniqueGenerator = uniqueGenerator;
            NamedGenerator = namedGenerator;
        }
    }

    class TypecheckAssignPass<TBinder> :
        IModulePartVisitor<TBinder, TypecheckAssignPassState<TBinder>, IModulePart<TypedBinder<TBinder>>>,
        IDynAstVisitor<TBinder, TypecheckAssignPassState<TBinder>, IDynAst<TypedBinder<TBinder>>>
    {
        private static readonly TypecheckAssignPass<TBinder> Fetch = new TypecheckAssignPass<TBinder>();

        private TypecheckAssignPass()
        {
        }

        public static IModulePart<TypedBinder<TBinder>> Run(IModulePart<TBinder> module, Func<TBinder> uniqueGenerator, Func<string, TBinder> namedGenerator)
        {
            return module.Visit(Fetch, new TypecheckAssignPassState<TBinder>(new Dictionary<TBinder, IDynAst<TBinder>>(), uniqueGenerator, namedGenerator));
        }

        public IModulePart<TypedBinder<TBinder>> Accept(Module<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            return new Module<TypedBinder<TBinder>>(value.ModuleName, value.Parts.Select(p => p.Visit(this, data)).Where(p => p != null).ToArray());
        }

        public IModulePart<TypedBinder<TBinder>> Accept(FixityDefinition<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            return new FixityDefinition<TypedBinder<TBinder>>(value.Symbol, value.IsLeft, value.Precedence);
        }

        public IModulePart<TypedBinder<TBinder>> Accept(TypeDefinition<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            var type = value.Type;
            data.Environment[value.Name.Value] = type;
            return null;
        }

        public IModulePart<TypedBinder<TBinder>> Accept(Definition<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            if (data.Environment.ContainsKey(value.Name.Value) == false)
                throw new Exception(value.Name.Value + " did not contain a type definition");
            var name = new Identifier<TypedBinder<TBinder>>(new TypedBinder<TBinder>(value.Name.Value, data.Environment[value.Name.Value]));
            return new Definition<TypedBinder<TBinder>>(name, value.Expression.Visit(this, data));
        }

        public IModulePart<TypedBinder<TBinder>> Accept(ImportDeclaration<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            return new ImportDeclaration<TypedBinder<TBinder>>(value.Import);
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Lambda<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            var type = new Identifier<TBinder>(data.UniqueGenerator());
            var newdata = new TypecheckAssignPassState<TBinder>(new Dictionary<TBinder, IDynAst<TBinder>>(data.Environment) { { value.Name, type } }, data.UniqueGenerator, data.NamedGenerator);
            return new Lambda<TypedBinder<TBinder>>(new TypedBinder<TBinder>(value.Name, type), value.Body.Visit(this, newdata));
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Forall<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            throw new Exception("Illegal forall in pre-typechecked expression");
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Application<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            return new Application<TypedBinder<TBinder>>(value.Function.Visit(this, data), value.Value.Visit(this, data));
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Identifier<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            var type = data.Environment[value.Value];
            var returnValue = (IDynAst<TypedBinder<TBinder>>)new Identifier<TypedBinder<TBinder>>(new TypedBinder<TBinder>(value.Value, type));
            while (true)
            {
                var lam = type as Lambda<TBinder>;
                if (lam == null)
                    break;
                returnValue = new Application<TypedBinder<TBinder>>(returnValue, new Literal<TypedBinder<TBinder>>(new Identifier<TBinder>(data.UniqueGenerator())));
                type = lam.Body;
            }
            return returnValue;
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Literal<TBinder> value, TypecheckAssignPassState<TBinder> data)
        {
            if (value.AsTypeLit<TBinder>() != null)
            {
                throw new Exception("Invalid type identifier in pre-typechecked code");
            }
            return new Literal<TypedBinder<TBinder>>(value.Value);
        }
    }

    class SubstApplier<TBinder> :
        IDynAstVisitor<TBinder, Dictionary<TBinder, IDynAst<TBinder>>, IDynAst<TBinder>>
    {
        private static readonly SubstApplier<TBinder> Fetch = new SubstApplier<TBinder>();

        private SubstApplier()
        {
        }

        /// <summary>
        /// This class is intended to be applied on types
        /// </summary>
        public static IDynAst<TBinder> Run(IDynAst<TBinder> ast, Dictionary<TBinder, IDynAst<TBinder>> substs)
        {
            return ast.Visit(Fetch, substs);
        }

        public IDynAst<TBinder> Accept(Lambda<TBinder> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            return new Lambda<TBinder>(value.Name, value.Body.Visit(this, data));
        }

        public IDynAst<TBinder> Accept(Forall<TBinder> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            throw new Exception("Illegal forall in pre-typechecked expression");
        }

        public IDynAst<TBinder> Accept(Application<TBinder> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            return new Application<TBinder>(value.Function.Visit(this, data), value.Value.Visit(this, data));
        }

        public IDynAst<TBinder> Accept(Identifier<TBinder> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            return data.ContainsKey(value.Value) ? data[value.Value] : value;
        }

        public IDynAst<TBinder> Accept(Literal<TBinder> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            return value;
        }
    }

    class FreeVarFinder<TBinder> :
        IDynAstVisitor<TBinder, object, List<TBinder>>
    {
        private static readonly FreeVarFinder<TBinder> Fetch = new FreeVarFinder<TBinder>();

        private FreeVarFinder()
        {
        }

        public static List<TBinder> Run(IDynAst<TBinder> ast)
        {
            return ast.Visit(Fetch, null);
        }

        public List<TBinder> Accept(Lambda<TBinder> value, object data)
        {
            var list = value.Body.Visit(this, data);
            list.Remove(value.Name);
            return list;
        }

        public List<TBinder> Accept(Forall<TBinder> value, object data)
        {
            throw new Exception("Illegal forall in pre-typechecked expression");
        }

        public List<TBinder> Accept(Application<TBinder> value, object data)
        {
            var left = value.Function.Visit(this, data);
            var right = value.Value.Visit(this, data);
            foreach (var binder in right.Where(binder => left.Contains(binder) == false))
                left.Add(binder);
            return left;
        }

        public List<TBinder> Accept(Identifier<TBinder> value, object data)
        {
            return new List<TBinder> { value.Value };
        }

        public List<TBinder> Accept(Literal<TBinder> value, object data)
        {
            return new List<TBinder>();
        }
    }

    static class RuleCollectorHelper<TBinder>
    {
        public static IDynAst<TBinder> Quantify(IDynAst<TBinder> toQuantify, ref IDynAst<TypedBinder<TBinder>> expression)
        {
            foreach (var binder in FreeVarFinder<TBinder>.Run(toQuantify))
            {
                expression = Forall<TypedBinder<TBinder>>.Create(new Identifier<TBinder>(binder), expression);
                toQuantify = new Lambda<TBinder>(binder, toQuantify);
            }
            return toQuantify;
        }

        private static void AddSubst(RuleCollectorState<TBinder> state, TBinder binder, IDynAst<TBinder> value)
        {
            var dict = new Dictionary<TBinder, IDynAst<TBinder>> { { binder, value } };
            foreach (var substitution in state.Substitutions.ToList())
                state.Substitutions[substitution.Key] = SubstApplier<TBinder>.Run(substitution.Value, dict);
            if (state.Substitutions.ContainsKey(binder) == false)
                state.Substitutions[binder] = value;
        }

        public static void MostGeneralUnion(IDynAst<TBinder> left, IDynAst<TBinder> right, RuleCollectorState<TBinder> state)
        {
            var leftIden = left as Identifier<TBinder>;
            if (leftIden != null)
            {
                var rightIden = right as Identifier<TBinder>;
                if (rightIden != null && leftIden.Value.Equals(rightIden.Value))
                    return;
                // TODO: Occurs check
                AddSubst(state, leftIden.Value, right);
                return;
            }
            var rightIden2 = right as Identifier<TBinder>;
            if (rightIden2 != null)
            {
                MostGeneralUnion(right, left, state);
                return;
            }
            var leftApp = left as Application<TBinder>;
            var rightApp = right as Application<TBinder>;
            if (leftApp != null && rightApp != null)
            {
                MostGeneralUnion(leftApp.Value, rightApp.Value, state);
                MostGeneralUnion(SubstApplier<TBinder>.Run(leftApp.Function, state.Substitutions), SubstApplier<TBinder>.Run(rightApp.Function, state.Substitutions), state);
                return;
            }
            var leftLam = left as Lambda<TBinder>;
            var rightLam = right as Lambda<TBinder>;
            if (leftLam != null && rightLam != null)
            {
                var name = new Literal<TBinder>(new LambdaMguHelper(leftLam.Name, rightLam.Name));
                var leftBody = SubstApplier<TBinder>.Run(leftLam.Body, new Dictionary<TBinder, IDynAst<TBinder>> { { leftLam.Name, name } });
                var rightBody = SubstApplier<TBinder>.Run(rightLam.Body, new Dictionary<TBinder, IDynAst<TBinder>> { { rightLam.Name, name } });
                MostGeneralUnion(leftBody, rightBody, state);
                return;
            }
            var leftIdenType = left as Literal<TBinder>;
            var rightIdenType = right as Literal<TBinder>;
            if (leftIdenType != null && rightIdenType != null)
            {
                if (leftIdenType.Value.Equals(rightIdenType.Value) == false)
                    throw new Exception("Cannot unify types " + leftIdenType.Value + " and " + rightIdenType.Value);
                return;
            }
            throw new Exception("Cannot unify types " + left.GetType() + " and " + right.GetType());
        }

        class LambdaMguHelper
        {
            private readonly TBinder _left;
            private readonly TBinder _right;

            public LambdaMguHelper(TBinder left, TBinder right)
            {
                _left = left;
                _right = right;
            }

            public override string ToString()
            {
                return string.Format("anonymous type from forall arguments {0} and {1}", _left, _right);
            }
        }

        public static bool LeftIsRight(IDynAst<TBinder> left, IDynAst<TBinder> right)
        {
            if (left is Identifier<TBinder>)
                return true;
            var leftLiteral = left as Literal<TBinder>;
            if (leftLiteral != null)
            {
                var rightLiteral = right as Literal<TBinder>;
                return rightLiteral != null && leftLiteral.Value.Equals(rightLiteral.Value);
            }
            var leftApplication = left as Application<TBinder>;
            if (leftApplication != null)
            {
                var rightApplication = right as Application<TBinder>;
                return rightApplication != null &&
                    LeftIsRight(leftApplication.Value, rightApplication.Value) &&
                    LeftIsRight(leftApplication.Function, leftApplication.Function);
            }
            var leftLambda = left as Lambda<TBinder>;
            if (leftLambda != null)
            {
                var rightLambda = right as Lambda<TBinder>;
                if (rightLambda != null)
                {
                    var newvar = new Literal<TBinder>(new LambdaMguHelper(leftLambda.Name, rightLambda.Name));
                    var leftBody = SubstApplier<TBinder>.Run(leftLambda.Body, new Dictionary<TBinder, IDynAst<TBinder>> { { leftLambda.Name, newvar } });
                    var rightBody = SubstApplier<TBinder>.Run(rightLambda.Body, new Dictionary<TBinder, IDynAst<TBinder>> { { rightLambda.Name, newvar } });
                    return LeftIsRight(leftBody, rightBody);
                }
                return false;
            }
            throw new Exception("Unimplemented case block in Typechecker LeftIsRight");
        }
    }

    struct RuleCollectorState<TBinder>
    {
        public readonly Func<TBinder> UniqueGenerator;
        public readonly Func<string, TBinder> NamedGenerator;
        public readonly Dictionary<TBinder, IDynAst<TBinder>> Substitutions;

        public RuleCollectorState(Func<TBinder> uniqueGenerator, Func<string, TBinder> namedGenerator, Dictionary<TBinder, IDynAst<TBinder>> substitutions)
        {
            UniqueGenerator = uniqueGenerator;
            NamedGenerator = namedGenerator;
            Substitutions = substitutions;
        }
    }

    class HindleyMilner<TBinder> :
        IModulePartVisitor<TypedBinder<TBinder>, Tuple<Func<TBinder>, Func<string, TBinder>>, IModulePart<TypedBinder<TBinder>>>,
        IDynAstVisitor<TypedBinder<TBinder>, RuleCollectorState<TBinder>, IDynAst<TBinder>>
    {
        private static readonly HindleyMilner<TBinder> Fetch = new HindleyMilner<TBinder>();

        private HindleyMilner()
        {
        }

        public static IModulePart<TypedBinder<TBinder>> Run(IModulePart<TypedBinder<TBinder>> modulePart, Func<TBinder> uniqueGenerator, Func<string, TBinder> namedGenerator)
        {
            var state = Tuple.Create(uniqueGenerator, namedGenerator);
            return modulePart.Visit(Fetch, state);
        }

        public IModulePart<TypedBinder<TBinder>> Accept(Module<TypedBinder<TBinder>> value, Tuple<Func<TBinder>, Func<string, TBinder>> data)
        {
            return new Module<TypedBinder<TBinder>>(value.ModuleName, value.Parts.Select(p => p.Visit(this, data)).ToArray());
        }

        public IModulePart<TypedBinder<TBinder>> Accept(FixityDefinition<TypedBinder<TBinder>> value, Tuple<Func<TBinder>, Func<string, TBinder>> data)
        {
            return value;
        }

        public IModulePart<TypedBinder<TBinder>> Accept(TypeDefinition<TypedBinder<TBinder>> value, Tuple<Func<TBinder>, Func<string, TBinder>> data)
        {
            return value;
        }

        public IModulePart<TypedBinder<TBinder>> Accept(Definition<TypedBinder<TBinder>> value, Tuple<Func<TBinder>, Func<string, TBinder>> data)
        {
            var state = new RuleCollectorState<TBinder>(data.Item1, data.Item2, new Dictionary<TBinder, IDynAst<TBinder>>());
            var result = value.Expression.Visit(this, state);
            result = SubstApplier<TBinder>.Run(result, state.Substitutions);
            var newExpr = value.Expression;
            newExpr = SubstAstApplier<TBinder>.Run(newExpr, state.Substitutions);
            result = RuleCollectorHelper<TBinder>.Quantify(result, ref newExpr); // TODO: Add foralls in expression
            if (RuleCollectorHelper<TBinder>.LeftIsRight(result, value.Name.Value.Type) == false) // TODO: Ignore order of arguments of explicit vs implicit (and reorder inserted foralls)
                throw new Exception(string.Format("Declared type on {0} did not match actual type {1}", value.Name.Value, result));
            return new Definition<TypedBinder<TBinder>>(value.Name, newExpr);
        }

        public IModulePart<TypedBinder<TBinder>> Accept(ImportDeclaration<TypedBinder<TBinder>> value, Tuple<Func<TBinder>, Func<string, TBinder>> data)
        {
            return value;
        }

        public IDynAst<TBinder> Accept(Lambda<TypedBinder<TBinder>> value, RuleCollectorState<TBinder> data)
        {
            var expr = value.Body.Visit(this, data);
            var type = new Application<TBinder>(new Application<TBinder>(TypeConstructor("->", data), value.Name.Type), expr);
            return type;
        }

        public IDynAst<TBinder> Accept(Forall<TypedBinder<TBinder>> value, RuleCollectorState<TBinder> data)
        {
            throw new Exception("Illegal forall in pre-typechecked expression");
        }

        public IDynAst<TBinder> Accept(Application<TypedBinder<TBinder>> value, RuleCollectorState<TBinder> data)
        {
            var function = value.Function.Visit(this, data);
            if (function is Lambda<TBinder>)
            {
                var literalValue = value.Value as Literal<TypedBinder<TBinder>>;
                if (literalValue != null)
                {
                    var typeParameter = literalValue.AsTypeLit<TBinder>();
                    if (typeParameter != null)
                    {
                        var forall = value.Function.Visit(this, data) as Lambda<TBinder>;
                        if (forall == null)
                            throw new Exception("Illegal type application to value " + value.Function);
                        return SubstApplier<TBinder>.Run(forall.Body, new Dictionary<TBinder, IDynAst<TBinder>> { { forall.Name, typeParameter } });
                    }
                    throw new Exception();
                }
                throw new Exception("Missing application to forall type from value " + value.Function);
            }
            var valueResult = value.Value.Visit(this, data);
            var returnType = (IDynAst<TBinder>)new Identifier<TBinder>(data.UniqueGenerator()); // TODO: This is a bit strange
            var mguHelperType = new Application<TBinder>(new Application<TBinder>(TypeConstructor("->", data), valueResult), returnType);
            RuleCollectorHelper<TBinder>.MostGeneralUnion(function, mguHelperType, data);
            returnType = SubstApplier<TBinder>.Run(returnType, data.Substitutions);
            return returnType;
        }

        public IDynAst<TBinder> Accept(Identifier<TypedBinder<TBinder>> value, RuleCollectorState<TBinder> data)
        {
            return SubstApplier<TBinder>.Run(value.Value.Type, data.Substitutions); // TODO: Pointless SubstApplier?
        }

        public IDynAst<TBinder> Accept(Literal<TypedBinder<TBinder>> value, RuleCollectorState<TBinder> data)
        {
            var obj = value.Value;
            string type;
            if (obj is long)
                type = "Int";
            else if (obj is double)
                type = "Double";
            else if (obj is string)
                type = "String";
            else if (obj is IDynAst<TBinder>)
                throw new Exception("Invalid type literal in expression");
            else
                throw new Exception("Unsupported literal " + obj.GetType());
            return TypeConstructor(type, data);
        }

        private IDynAst<TBinder> TypeConstructor(string type, RuleCollectorState<TBinder> data)
        {
            return new Literal<TBinder>(new Identifier<TBinder>(data.NamedGenerator(type)));
        }
    }

    class SubstAstApplier<TBinder> :
        IDynAstVisitor<TypedBinder<TBinder>, Dictionary<TBinder, IDynAst<TBinder>>, IDynAst<TypedBinder<TBinder>>>
    {
        private static readonly SubstAstApplier<TBinder> Fetch = new SubstAstApplier<TBinder>();

        private SubstAstApplier()
        {
        }

        /// <summary>
        /// This class is intended to be applied on values
        /// </summary>
        public static IDynAst<TypedBinder<TBinder>> Run(IDynAst<TypedBinder<TBinder>> ast, Dictionary<TBinder, IDynAst<TBinder>> substs)
        {
            return ast.Visit(Fetch, substs);
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Lambda<TypedBinder<TBinder>> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            var subst = SubstApplier<TBinder>.Run(value.Name.Type, data);
            var name = new TypedBinder<TBinder>(value.Name.Name, subst);
            return new Lambda<TypedBinder<TBinder>>(name, value.Body.Visit(this, data));
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Forall<TypedBinder<TBinder>> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            throw new Exception("Illegal forall in pre-typechecked expression");
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Application<TypedBinder<TBinder>> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            return new Application<TypedBinder<TBinder>>(value.Function.Visit(this, data), value.Value.Visit(this, data));
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Identifier<TypedBinder<TBinder>> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            var subst = SubstApplier<TBinder>.Run(value.Value.Type, data);
            var name = new TypedBinder<TBinder>(value.Value.Name, subst);
            return new Identifier<TypedBinder<TBinder>>(name);
        }

        public IDynAst<TypedBinder<TBinder>> Accept(Literal<TypedBinder<TBinder>> value, Dictionary<TBinder, IDynAst<TBinder>> data)
        {
            var type = value.AsTypeLit<TBinder>();
            return type == null ? value : new Literal<TypedBinder<TBinder>>(SubstApplier<TBinder>.Run(type, data));
        }
    }

    static class Typechecker<TBinder>
    {
        public static IModulePart<TypedBinder<TBinder>> Run(IModulePart<TBinder> module, Func<TBinder> uniqueGenerator, Func<string, TBinder> namedGenerator)
        {
            var assign = TypecheckAssignPass<TBinder>.Run(module, uniqueGenerator, namedGenerator);
            return HindleyMilner<TBinder>.Run(assign, uniqueGenerator, namedGenerator);
        }
    }

    static class TypecheckerUniqueBinder
    {
        public static IModulePart<TypedBinder<UniqueBinder>> Run(IModulePart<UniqueBinder> module)
        {
            var i = 1;
            return Typechecker<UniqueBinder>.Run(module, () => new UniqueBinder(new object(), new[] { "ty" + i++ }),
                s =>
                {
                    var builtinBinder = SemanticAnalyzer.GetBuiltinBinder(s);
                    if (builtinBinder.UniqueId != null) // from Default value
                        return builtinBinder;
                    throw new Exception("Compiler-named value " + s + " did not exist");
                });
        }
    }
}
