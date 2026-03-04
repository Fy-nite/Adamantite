namespace Adamantite.BindingGenerator
{
    public interface IBindingGenerator
    {
        void GenerateBindings();
    }

    public interface IBindingGeneratorFactory
    {
        IBindingGenerator Create(string inputDirectory, string outputDirectory, string headerContent);
    }
    public class BindingGeneratorFactory : IBindingGeneratorFactory
    {
        public IBindingGenerator Create(string inputDirectory, string outputDirectory, string headerContent)
        {
            return new BindingGenerator(inputDirectory, outputDirectory, headerContent);
        }
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BindingGeneratorAttribute : Attribute
    {
        public string InputDirectory { get; }
        public string OutputDirectory { get; }
        public string HeaderContent { get; }

        public BindingGeneratorAttribute(string inputDirectory, string outputDirectory, string headerContent)
        {
            InputDirectory = inputDirectory;
            OutputDirectory = outputDirectory;
            HeaderContent = headerContent;
        }
    }

    public class BindingGenerator : IBindingGenerator
    {
        private  string _inputDirectory;
        private  string _outputDirectory;
        private string _headerContent;

        public BindingGenerator(string inputDirectory, string outputDirectory, string headerContent)
        {
            _inputDirectory = inputDirectory;
            _outputDirectory = outputDirectory;
            _headerContent = headerContent;
        }

        public void GenerateBindings()
        {
            Console.WriteLine($"Generating bindings from {_inputDirectory} to {_outputDirectory}");
            if (!Directory.Exists(_inputDirectory))
            {
                Console.WriteLine($"Input directory not found: {_inputDirectory}");
                return;
            }

            var headerFiles = Directory.GetFiles(_inputDirectory, "*.h*", SearchOption.AllDirectories);
            var parser = new CppHeaderParser();
            var csGenerator = new CSharpBindingGenerator();
            var libraryName = new DirectoryInfo(_inputDirectory).Name;
            foreach (var headerFile in headerFiles)
            {
                Console.WriteLine($"Processing file: {headerFile}");
                var functions = parser.ParseFunctions(headerFile);
                var relativePath = Path.GetRelativePath(_inputDirectory, headerFile);
                var relativeDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
                var outputFileName = Path.GetFileNameWithoutExtension(headerFile) + ".cs";
                var outputDir = Path.Combine(_outputDirectory, relativeDir);
                var outputPath = Path.Combine(outputDir, outputFileName);
                csGenerator.GenerateCSharpBindings(functions, outputPath, _headerContent, libraryName);
                Console.WriteLine($"Generated: {outputPath}");
            }
        }
    }
}
