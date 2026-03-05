using System;
using System.IO;

namespace Adamantite.BindingGenerator
{
    // Simple factory that returns a CppSharp-backed generator when available.
    public class CppSharpBindingGeneratorFactory : IBindingGeneratorFactory
    {
        public IBindingGenerator Create(string inputDirectory, string outputDirectory, string headerContent)
        {
            return new CppSharpBindingGenerator(inputDirectory, outputDirectory, headerContent);
        }
    }

    public class CppSharpBindingGenerator : IBindingGenerator
    {
        private readonly string _inputDirectory;
        private readonly string _outputDirectory;
        private readonly string _headerContent;

        public CppSharpBindingGenerator(string inputDirectory, string outputDirectory, string headerContent)
        {
            _inputDirectory = inputDirectory;
            _outputDirectory = outputDirectory;
            _headerContent = headerContent;
        }

        public void GenerateBindings()
        {
            Console.WriteLine("CppSharp integration not enabled in this build.");
            Console.WriteLine("To enable CppSharp-based generation:");
            Console.WriteLine("  1) Add the CppSharp NuGet packages to the bindingGenerator project:");
            Console.WriteLine("     dotnet add package CppSharp --version 0.22.0");
            Console.WriteLine("     dotnet add package CppSharp.Generators --version 0.22.0");
            Console.WriteLine("  2) Rebuild the project. Optionally define a CPPSHARP symbol if you plan to gate code.");
            Console.WriteLine("  3) Re-run with --cppsharp to use the CppSharp integration.");
            Console.WriteLine();
            Console.WriteLine("Falling back to the heuristic parser/generator.");

            // Fallback to existing generator to maintain behavior without CppSharp installed.
            var fallback = new BindingGenerator(_inputDirectory, _outputDirectory, _headerContent);
            fallback.GenerateBindings();
        }
    }
}
