using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            // File-scoped namespace to prevent type name collisions across generated files
            var fileNamespace = GetNamespaceFromOutputPath(outputPath);
            lines.Add($"namespace {fileNamespace};");
            lines.Add("");
            // Use a per-file unique name based on the output file to avoid duplicate class errors
            var fileBaseName = Path.GetFileNameWithoutExtension(outputPath);
            var nativeBindingsName = $"NativeBindings_{fileBaseName}";
            // Emit global native functions
            lines.Add($"public static class {nativeBindingsName}");
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
                    var emittedFieldNames = new HashSet<string>();
                    foreach (var field in cls.Fields)
                    {
                        var ftype = MapCppTypeToCSharp(field.Type);
                        var fname = MakeSafeIdentifier(field.Name.Replace("[]", "Array"));
                        // Skip duplicate field names
                        if (!emittedFieldNames.Add(fname)) continue;
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
                        // Deduplicate constructor overloads by their mapped C# parameter type signature
                        var emittedCtorSigs = new HashSet<string>();
                        int ci = 0;
                        foreach (var ctor in ctors)
                        {
                            var ctorTypeKey = string.Join(",", ctor.Parameters.Select(p => MapCppTypeToCSharp(p.Type)));
                            if (!emittedCtorSigs.Add(ctorTypeKey)) continue;

                            var pname = $"{cls.Name}_Create" + (ci == 0 ? string.Empty : "_" + ci);

                            // Build extern declaration params
                            var externParamList = new List<string>();
                            for (int pi = 0; pi < ctor.Parameters.Count; pi++)
                                externParamList.Add($"{MapCppTypeToCSharp(ctor.Parameters[pi].Type)} {SafeParamName(ctor.Parameters[pi], pi)}");
                            lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                            lines.Add($"    private static extern IntPtr {pname}({string.Join(", ", externParamList)});");

                            // Build managed constructor with matching parameters
                            var managedCtorParamList = new List<string>();
                            for (int pi = 0; pi < ctor.Parameters.Count; pi++)
                                managedCtorParamList.Add($"{MapCppTypeToCSharp(ctor.Parameters[pi].Type)} {SafeParamName(ctor.Parameters[pi], pi)}");
                            var callArgList = string.Join(", ", ctor.Parameters.Select((p, pi) => SafeParamName(p, pi)));
                            lines.Add($"    public {cls.Name}({string.Join(", ", managedCtorParamList)}) {{ _native = {pname}({callArgList}); }}");
                            ci++;
                        }
                    }

                    // methods: generate extern + managed wrapper
                    // Track emitted method signatures to skip overloads that map to the same C# signature
                    var emittedMethodSignatures = new HashSet<string>();
                    foreach (var method in cls.Methods)
                    {
                        if (method.Name == cls.Name) continue; // skip ctor already handled
                        var methodName = MakeSafeIdentifier(method.Name);
                        var externName = $"{cls.Name}_{methodName}";
                        // Dedup: skip if the same extern name + param types already emitted
                        var typeKey = string.Join(",", method.Parameters.Select(p => MapCppTypeToCSharp(p.Type)));
                        var sigKey = $"{externName}({typeKey})";
                        if (emittedMethodSignatures.Contains(sigKey)) continue;
                        emittedMethodSignatures.Add(sigKey);

                        // parameters: instance pointer + method params
                        // Use indexed names for unnamed params to avoid duplicate identifier errors
                        var paramList = new List<string>();
                        paramList.Add("IntPtr instance");
                        for (int pi = 0; pi < method.Parameters.Count; pi++)
                            paramList.Add($"{MapCppTypeToCSharp(method.Parameters[pi].Type)} {SafeParamName(method.Parameters[pi], pi)}");
                        var externParams = string.Join(", ", paramList);
                        var ret = MapCppTypeToCSharp(method.ReturnType);
                        lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                        lines.Add($"    private static extern {ret} {externName}({externParams});");

                        // managed wrapper with indexed names for unnamed params
                        var managedParamList = new List<string>();
                        for (int pi = 0; pi < method.Parameters.Count; pi++)
                            managedParamList.Add($"{MapCppTypeToCSharp(method.Parameters[pi].Type)} {SafeParamName(method.Parameters[pi], pi)}");
                        var managedParams = string.Join(", ", managedParamList);
                        lines.Add($"    public {ret} {methodName}({managedParams})");
                        lines.Add("    {");
                        var callArgs = new List<string> { "_native" };
                        for (int pi = 0; pi < method.Parameters.Count; pi++) callArgs.Add(SafeParamName(method.Parameters[pi], pi));
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
            // prefix keyword identifiers with underscore
            if (IsCSharpKeyword(safe)) safe = "_" + safe;
            return safe;
        }

        private string MapCppTypeToCSharp(string cppType)
        {
            if (string.IsNullOrWhiteSpace(cppType)) return "IntPtr";
            cppType = cppType.Trim();

            // Strip leading qualifiers: const, volatile, static, inline, constexpr, explicit, virtual, override, final
            foreach (var qualifier in new[] { "constexpr ", "inline ", "virtual ", "explicit ", "override ", "final ", "static ", "volatile ", "const " })
            {
                while (cppType.StartsWith(qualifier))
                    cppType = cppType.Substring(qualifier.Length).TrimStart();
            }
            // Strip trailing const/& qualifiers
            foreach (var qualifier in new[] { " const", " &", "&" })
            {
                while (cppType.EndsWith(qualifier))
                    cppType = cppType.Substring(0, cppType.Length - qualifier.Length).TrimEnd();
            }

            // handle pointers
            if (cppType.EndsWith("*") || cppType.EndsWith("&")) return "IntPtr";

            switch (cppType)
            {
                case "int": return "int";
                case "unsigned int": return "uint";
                case "short":
                case "short int":
                case "signed short":
                case "signed short int": return "short";
                case "unsigned short":
                case "unsigned short int": return "ushort";
                case "long":
                case "long int":
                case "signed long":
                case "signed long int": return "long";
                case "unsigned long":
                case "unsigned long int": return "ulong";
                case "long long":
                case "long long int":
                case "signed long long":
                case "int64_t": return "long";
                case "unsigned long long":
                case "uint64_t": return "ulong";
                case "int8_t":
                case "signed char": return "sbyte";
                case "uint8_t":
                case "unsigned char": return "byte";
                case "int16_t": return "short";
                case "uint16_t": return "ushort";
                case "int32_t":
                case "signed int": return "int";
                case "uint32_t": return "uint";
                case "size_t": return "UIntPtr";
                case "ptrdiff_t": return "IntPtr";
                case "float": return "float";
                case "double": return "double";
                case "long double": return "double";
                case "bool": return "bool";
                case "char": return "sbyte";
                case "const char":
                case "char const":
                case "const char *":
                case "char *":
                    return "IntPtr"; // caller should marshal strings manually
                case "void": return "void";
                case "std::string":
                case "string": return "IntPtr"; // marshal via pointer
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

        // Returns a safe parameter name: uses index-based name for unnamed params to avoid duplicate identifiers
        private string SafeParamName(CppParameter p, int idx) =>
            string.IsNullOrWhiteSpace(p.Name) ? $"arg{idx}" : MakeSafeIdentifier(p.Name);

        // Derives a C# namespace from the output file path, using subdirectory relative to src/
        private static string GetNamespaceFromOutputPath(string outputPath)
        {
            var normalized = outputPath.Replace('\\', '/');
            var srcIdx = -1;
            foreach (var marker in new[] { "/src/", "/Src/" })
            {
                var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) { srcIdx = idx + marker.Length; break; }
            }
            var relPath = srcIdx >= 0 ? normalized.Substring(srcIdx) : Path.GetFileName(normalized);
            var dir = (Path.GetDirectoryName(relPath) ?? string.Empty).Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(dir))
                return "AdamantiteBindings";
            var parts = dir.Split('/').Select(p => System.Text.RegularExpressions.Regex.Replace(p, "[^A-Za-z0-9_]", "_"));
            return "AdamantiteBindings." + string.Join(".", parts);
        }
    }
}
