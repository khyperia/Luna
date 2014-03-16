using System;
using System.IO;
using Luna.Parser;
using Luna.Passes;

namespace Luna
{
    static class Program
    {
        static void Main()
        {
            var source = File.ReadAllText("TestProgram2.luna");
            Console.WriteLine("--- Source:");
            Console.WriteLine(source);
            Console.WriteLine("--- Token stream:");
            var ast = RecursiveDescentParser.Parse(source);
            Console.WriteLine("--- Syntax tree:");
            Console.WriteLine(ast);
            Console.WriteLine("--- Semantic tree:");
            var semantic = SemanticAnalyzer.Run(ast);
            Console.WriteLine(semantic);
            Console.WriteLine("--- Typechecked tree:");
            var typechecked = TypecheckerUniqueBinder.Run(semantic);
            Console.WriteLine(typechecked);
            Console.WriteLine("--- Syntax tree evaluation of main");
            Console.WriteLine(EvalTerminal.Run(ast, "main"));
            Console.WriteLine("--- Semantic tree evaluation of Main.main");
            Console.WriteLine(EvalUniqueBinder.Run(semantic, "Main.main"));
            Console.ReadKey(true);
        }
    }
}
