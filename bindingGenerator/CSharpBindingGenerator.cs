using System;
using System.Collections.Generic;
using System.IO;

namespace Adamantite.BindingGenerator
{
    public class CSharpBindingGenerator
    {
        public void GenerateCSharpBindings(List<CppFunction> functions, List<CppClass> classes, string outputPath, string headerContent, string libraryName)
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
            // Emit global native functions
            lines.Add("public static class NativeBindings");
            lines.Add("{");
            int suffix = 1;
            foreach (var func in functions)
            {
                var csReturn = MapCppTypeToCSharp(func.ReturnType);
                var csParams = MapParameters(func.Parameters);
                var safeName = MakeSafeIdentifier(func.Name);
                var dllName = string.IsNullOrWhiteSpace(libraryName) ? "NativeLib" : libraryName;
                if (lines.Exists(l => l.Contains($" {safeName}(") ))
                {
                    safeName = $"{safeName}_{suffix++}";
                }
                if (IsCSharpKeyword(safeName))
                {
                    safeName = safeName + "_" + suffix++;
                }
                lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                lines.Add($"    public static extern {csReturn} {safeName}({csParams});");
            }
            lines.Add("}");

            // Emit structs and classes
            foreach (var cls in classes)
            {
                if (cls.IsStruct)
                {
                    lines.Add("");
                    lines.Add("[StructLayout(LayoutKind.Sequential)]");
                    lines.Add($"public struct {cls.Name}");
                    lines.Add("{");
                    foreach (var field in cls.Fields)
                    {
                        var ftype = MapCppTypeToCSharp(field.Type);
                        var fname = MakeSafeIdentifier(field.Name.Replace("[]", "Array"));
                        lines.Add($"    public {ftype} {fname};");
                    }
                    lines.Add("}");
                }
                else
                {
                    // generate native externs for methods (assume instance pointer as first param)
                    var dllName = string.IsNullOrWhiteSpace(libraryName) ? "NativeLib" : libraryName;
                    lines.Add("");
                    lines.Add($"public class {cls.Name}");
                    lines.Add("{");
                    lines.Add("    private IntPtr _native;\n");
                    // constructors: methods with same name
                    var ctors = cls.Methods.FindAll(m => m.Name == cls.Name);
                    if (ctors.Count > 0)
                    {
                        // declare extern create functions for each ctor overload
                        int ci = 0;
                        foreach (var ctor in ctors)
                        {
                            var pname = $"{cls.Name}_Create" + (ci == 0 ? string.Empty : "_" + ci);
                            var pparams = MapParameters(ctor.Parameters);
                            lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                            lines.Add($"    private static extern IntPtr {pname}({pparams});");
                            ci++;
                        }
                        // generate simple managed constructors that call create
                        lines.Add($"    public {cls.Name}() {{ _native = {cls.Name}_Create(); }}");
                    }

                    // methods: generate extern + managed wrapper
                    foreach (var method in cls.Methods)
                    {
                        if (method.Name == cls.Name) continue; // skip ctor already handled
                        var methodName = MakeSafeIdentifier(method.Name);
                        var externName = $"{cls.Name}_{methodName}";
                        // parameters: instance pointer + method params
                        var paramList = new List<string>();
                        paramList.Add("IntPtr instance");
                        foreach (var p in method.Parameters)
                            paramList.Add($"{MapCppTypeToCSharp(p.Type)} {MakeSafeIdentifier(p.Name)}");
                        var externParams = string.Join(", ", paramList);
                        var ret = MapCppTypeToCSharp(method.ReturnType);
                        lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                        lines.Add($"    private static extern {ret} {externName}({externParams});");

                        // managed wrapper
                        var managedParams = string.Join(", ", method.Parameters.ConvertAll(p => $"{MapCppTypeToCSharp(p.Type)} {MakeSafeIdentifier(p.Name)}"));
                        lines.Add($"    public {ret} {methodName}({managedParams})");
                        lines.Add("    {");
                        var callArgs = new List<string>();
                        callArgs.Add("_native");
                        foreach (var p in method.Parameters) callArgs.Add(MakeSafeIdentifier(p.Name));
                        var call = $"{externName}({string.Join(", ", callArgs)})";
                        if (ret == "void") lines.Add($"        {call};");
                        else lines.Add($"        return {call};");
                        lines.Add("    }");
                    }

                    lines.Add("}");
                }
            }

            File.WriteAllLines(outputPath, lines);
        }
        // IsCSharpKeyword checks if a name is a C# keyword that would require escaping
        private static bool IsCSharpKeyword(string name)
        {
            var keywords = new HashSet<string> {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
                "checked", "class", "const", "continue", "decimal", "default", "delegate",
                "do", "double", "else", "enum", "event", "explicit", "extern", "false",
                "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
                "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
                "new", "null", "object", "operator", "out", "override", "params",
                "private", "protected", "public", "readonly", "ref", "return",
                "sbyte", "sealed", "short", "sizeof","stackalloc","static","string",
                "struct","switch","this","throw","true","try","typeof","uint",
                "ulong","unchecked","unsafe","ushort","using","virtual","void",
                "volatile","while"
            };
            return keywords.Contains(name);
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
