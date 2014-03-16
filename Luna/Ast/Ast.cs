using System;
using System.Collections.Generic;
using System.Linq;

namespace Luna.Ast
{
    struct UniqueBinder
    {
        public readonly object UniqueId;
        public readonly string[] FullName;

        public UniqueBinder(object uniqueId, string[] fullName)
        {
            UniqueId = uniqueId;
            FullName = fullName;
        }

        private bool Equals(UniqueBinder other)
        {
            return Equals(UniqueId, other.UniqueId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is UniqueBinder && Equals((UniqueBinder)obj);
        }

        public override int GetHashCode()
        {
            return (UniqueId != null ? UniqueId.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return string.Join(".", FullName);
        }
    }

    interface IModulePartVisitor<TBinder, in TIn, out TOut>
    {
        TOut Accept(Module<TBinder> value, TIn data);
        TOut Accept(FixityDefinition<TBinder> value, TIn data);
        TOut Accept(TypeDefinition<TBinder> value, TIn data);
        TOut Accept(Definition<TBinder> value, TIn data);
        TOut Accept(ImportDeclaration<TBinder> value, TIn data);
    }

    interface IModulePart<TBinder>
    {
        TOut Visit<TIn, TOut>(IModulePartVisitor<TBinder, TIn, TOut> visitor, TIn data);
    }

    interface IDynAstVisitor<TBinder, in TIn, out TOut>
    {
        TOut Accept(Lambda<TBinder> value, TIn data);
        TOut Accept(Forall<TBinder> value, TIn data);
        TOut Accept(Application<TBinder> value, TIn data);
        TOut Accept(Identifier<TBinder> value, TIn data);
        TOut Accept(Literal<TBinder> value, TIn data);
    }

    interface IDynAst<TBinder>
    {
        TOut Visit<TIn, TOut>(IDynAstVisitor<TBinder, TIn, TOut> visitor, TIn data);
    }

    class Module<TBinder> : IModulePart<TBinder>
    {
        public readonly string[] ModuleName;
        public readonly IModulePart<TBinder>[] Parts;

        public Module(string[] moduleName, IModulePart<TBinder>[] parts)
        {
            ModuleName = moduleName;
            Parts = parts;
        }

        public TOut Visit<TIn, TOut>(IModulePartVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(Module<TBinder> other)
        {
            return ModuleName.SequenceEqual(other.ModuleName) && Parts.SequenceEqual(other.Parts);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Module<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ModuleName != null ? ModuleName.GetHashCode() : 0) * 397) ^ (Parts != null ? Parts.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("module {0}{2}    {1}{2};", string.Join(".", ModuleName), string.Join(Environment.NewLine + "    ", Parts.Select(p => p.ToString())), Environment.NewLine);
        }
    }

    class FixityDefinition<TBinder> : IModulePart<TBinder>
    {
        public readonly string Symbol;
        public readonly bool IsLeft;
        public readonly int Precedence;

        public FixityDefinition(string symbol, bool isLeft, int precedence)
        {
            Symbol = symbol;
            IsLeft = isLeft;
            Precedence = precedence;
        }

        public TOut Visit<TIn, TOut>(IModulePartVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(FixityDefinition<TBinder> other)
        {
            return string.Equals(Symbol, other.Symbol) && IsLeft.Equals(other.IsLeft) && Precedence == other.Precedence;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((FixityDefinition<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Symbol != null ? Symbol.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsLeft.GetHashCode();
                hashCode = (hashCode * 397) ^ Precedence;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", IsLeft ? "infixl" : "infixr", Precedence, Symbol);
        }
    }

    class TypeDefinition<TBinder> : IModulePart<TBinder>
    {
        public readonly Identifier<TBinder> Name;
        public readonly IDynAst<TBinder> Type;

        public TypeDefinition(Identifier<TBinder> name, IDynAst<TBinder> type)
        {
            Name = name;
            Type = type;
        }

        public TOut Visit<TIn, TOut>(IModulePartVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(TypeDefinition<TBinder> other)
        {
            return Equals(Name, other.Name) && Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TypeDefinition<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("{0} :: {1};", Name.Value, Type);
        }
    }

    class Definition<TBinder> : IModulePart<TBinder>
    {
        public readonly Identifier<TBinder> Name;
        public readonly IDynAst<TBinder> Expression;

        public Definition(Identifier<TBinder> name, IDynAst<TBinder> expression)
        {
            Name = name;
            Expression = expression;
        }

        public TOut Visit<TIn, TOut>(IModulePartVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(Definition<TBinder> other)
        {
            return Equals(Name, other.Name) && Equals(Expression, other.Expression);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Definition<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("{0} = {1};", Name.Value, Expression);
        }
    }

    class ImportDeclaration<TBinder> : IModulePart<TBinder>
    {
        public readonly string Import;

        public ImportDeclaration(string import)
        {
            Import = import;
        }

        public TOut Visit<TIn, TOut>(IModulePartVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(ImportDeclaration<TBinder> other)
        {
            return string.Equals(Import, other.Import);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ImportDeclaration<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            return (Import != null ? Import.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return string.Format("import {0}", Import);
        }
    }

    class Forall<TBinder> : IDynAst<TBinder>
    {
        // Required to be a type identifier
        public readonly Literal<TBinder> Name;
        public readonly IDynAst<TBinder> Body;

        private Forall(Literal<TBinder> name, IDynAst<TBinder> body)
        {
            Name = name;
            Body = body;
        }

        public static Forall<TBinder> Create<TTypeBinder>(IDynAst<TTypeBinder> name, IDynAst<TBinder> value)
        {
            return new Forall<TBinder>(new Literal<TBinder>(name), value);
        }

        public TOut Visit<TIn, TOut>(IDynAstVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        public override string ToString()
        {
            return string.Format("/\\{0} -> {1}", Name.Value, Body);
        }
    }

    class Lambda<TBinder> : IDynAst<TBinder>
    {
        public readonly TBinder Name;
        public readonly IDynAst<TBinder> Body;

        public Lambda(TBinder name, IDynAst<TBinder> body)
        {
            Name = name;
            Body = body;
        }

        public TOut Visit<TIn, TOut>(IDynAstVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(Lambda<TBinder> other)
        {
            return Name.Equals(other.Name) && Equals(Body, other.Body);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Lambda<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<TBinder>.Default.GetHashCode(Name) * 397) ^ (Body != null ? Body.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("(\\{0} -> {1})", Name, Body);
        }
    }

    class Application<TBinder> : IDynAst<TBinder>
    {
        public readonly IDynAst<TBinder> Function;
        public readonly IDynAst<TBinder> Value;

        public Application(IDynAst<TBinder> function, IDynAst<TBinder> value)
        {
            Function = function;
            Value = value;
        }

        public TOut Visit<TIn, TOut>(IDynAstVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(Application<TBinder> other)
        {
            return Equals(Function, other.Function) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Application<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Function != null ? Function.GetHashCode() : 0) * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            var funcIsApp = Function as Application<TBinder>;
            if (funcIsApp != null)
            {
                var blah = funcIsApp.Function as Identifier<TBinder>;
                if (blah != null && char.IsLetter(blah.Value.ToString(), 0) == false)
                    return string.Format("({0}) {1} ({2})", funcIsApp.Value, blah.Value, Value);
            }
            return string.Format("({0}) ({1})", Function, Value);
        }
    }

    class Identifier<TBinder> : IDynAst<TBinder>
    {
        public readonly TBinder Value;

        public Identifier(TBinder value)
        {
            Value = value;
        }

        public TOut Visit<TIn, TOut>(IDynAstVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(Identifier<TBinder> other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Identifier<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<TBinder>.Default.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    class Literal<TBinder> : IDynAst<TBinder>
    {
        /// <summary>
        /// Note: Can be of type IDynAst&lt;TTypeBinder&gt;, use AsTypeLit
        /// </summary>
        public readonly object Value;

        public Literal(object value)
        {
            Value = value;
        }

        public IDynAst<TTypeBinder> AsTypeLit<TTypeBinder>()
        {
            return Value as IDynAst<TTypeBinder>;
        }

        public TOut Visit<TIn, TOut>(IDynAstVisitor<TBinder, TIn, TOut> visitor, TIn data)
        {
            return visitor.Accept(this, data);
        }

        private bool Equals(Literal<TBinder> other)
        {
            return Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Literal<TBinder>)obj);
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
