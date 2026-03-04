using System;
using System.Collections.Generic;
using System.IO;

namespace Adamantite.BindingGenerator
{
    public class CSharpBindingGenerator
    {
        public void GenerateCSharpBindings(List<CppFunction> functions, string outputPath, string headerContent)
        {
            // Ensure output directory exists
            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            var lines = new List<string>();
            // Replace simple placeholders in headerContent
            headerContent = headerContent?.Replace("${RepoUrl}", "https://github.com/fy-nite/Adamantite").Replace("${Version}", "0.1.0") ?? string.Empty;
            lines.Add(headerContent.Trim());
            lines.Add("using System;");
            lines.Add("using System.Runtime.InteropServices;");
            lines.Add("");
            lines.Add("public static class NativeBindings");
            lines.Add("{");
            foreach (var func in functions)
            {
                var csReturn = MapCppTypeToCSharp(func.ReturnType);
                var csParams = MapParameters(func.Parameters);
                var safeName = MakeSafeIdentifier(func.Name);
                lines.Add($"    [DllImport(\"NativeLib.dll\", CallingConvention = CallingConvention.Cdecl)]");
                lines.Add($"    public static extern {csReturn} {safeName}({csParams});");
            }
            lines.Add("}");

            File.WriteAllLines(outputPath, lines);
        }

        private string MakeSafeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";
            // replace invalid chars
            var safe = System.Text.RegularExpressions.Regex.Replace(name, "[^A-Za-z0-9_]", "_");
            if (char.IsDigit(safe[0])) safe = "_" + safe;
            return safe;
        }

        private string MapCppTypeToCSharp(string cppType)
        {
            if (string.IsNullOrWhiteSpace(cppType)) return "IntPtr";
            cppType = cppType.Trim();
            // handle pointers
            if (cppType.EndsWith("*") || cppType.EndsWith("&")) return "IntPtr";

            switch (cppType)
            {
                case "int": return "int";
                case "unsigned int": return "uint";
                case "short": return "short";
                case "long": return "long";
                case "float": return "float";
                case "double": return "double";
                case "bool": return "bool";
                case "char": return "sbyte";
                case "const char":
                case "char const":
                case "const char *":
                case "char *":
                    return "IntPtr"; // caller should marshal strings manually
                case "void": return "void";
                default: return "IntPtr";
            }
        }

        private string MapParameters(List<CppParameter> parameters)
        {
            if (parameters == null || parameters.Count == 0) return string.Empty;
            var mapped = new List<string>();
            foreach (var p in parameters)
            {
                var csType = MapCppTypeToCSharp(p.Type ?? string.Empty);
                var name = string.IsNullOrWhiteSpace(p.Name) ? "arg" + mapped.Count : MakeSafeIdentifier(p.Name);
                mapped.Add($"{csType} {name}");
            }
            return string.Join(", ", mapped);
        }
    }
}
