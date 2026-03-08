using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Adamantite.BindingGenerator
{
    public class CSharpBindingGenerator
    {
        // C++ type name → C# friendly type and the marshaling expression to convert to/from the raw IntPtr/blittable type.
        // ManagedType:   the type shown in the public API  (e.g. "string", "Surface")
        // ToNative:      C# expression turning a managed value into the raw extern value  ({0} = the parameter name)
        // FromNative:    C# expression turning the raw return value into the managed type  ({0} = the raw expression)
        private record TypeMarshal(string ManagedType, string ToNative, string FromNative);

        // Well-known C++ → C# marshal rules that don't depend on the parsed class list
        private static readonly Dictionary<string, TypeMarshal> BuiltinMarshalRules = new()
        {
            // std::string / const std::string& → string (marshal via UTF-8 pointer)
            ["std::string"]       = new("string", "MarshalString({0})", "MarshalPtrToString({0})"),
            ["string"]            = new("string", "MarshalString({0})", "MarshalPtrToString({0})"),
            // const char* → string
            ["const char *"]      = new("string", "MarshalString({0})", "MarshalPtrToString({0})"),
            ["char *"]            = new("string", "MarshalString({0})", "MarshalPtrToString({0})"),
            ["const char"]        = new("string", "MarshalString({0})", "MarshalPtrToString({0})"),
            ["char const"]        = new("string", "MarshalString({0})", "MarshalPtrToString({0})"),
        };

        // Emits the static helper methods that every generated class file needs
        private static readonly string MarshalHelpers = @"
    // ── Marshal helpers ────────────────────────────────────────────────────────
    private static System.IntPtr MarshalString(string? s)
    {
        if (s is null) return System.IntPtr.Zero;
        return System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8(s);
    }
    private static void FreeNative(System.IntPtr p)
    {
        if (p != System.IntPtr.Zero)
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(p);
    }
    private static string MarshalPtrToString(System.IntPtr p)
    {
        if (p == System.IntPtr.Zero) return string.Empty;
        return System.Runtime.InteropServices.Marshal.PtrToStringUTF8(p) ?? string.Empty;
    }
    private static byte[] MarshalPtrToByteArray(System.IntPtr ptr, System.UIntPtr size)
    {
        if (ptr == System.IntPtr.Zero || (ulong)size == 0UL) return System.Array.Empty<byte>();
        var _res = new byte[(int)(ulong)size];
        System.Runtime.InteropServices.Marshal.Copy(ptr, _res, 0, _res.Length);
        return _res;
    }
    // ── End helpers ────────────────────────────────────────────────────────────
";

        public void GenerateCSharpBindings(List<CppFunction> functions, List<CppClass> classes, string outputPath, string headerContent, string libraryName, ISet<string>? globalKnownClasses = null)
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
            // Build known wrapper-class names for pointer marshaling (covers both global functions and class methods)
            var knownClassesGlobal = new HashSet<string>(classes.Where(c => !c.IsStruct).Select(c => c.Name));
            // Emit global native functions
            lines.Add($"public static class {nativeBindingsName}");
            lines.Add("{");
            // Inject marshal helpers if any global function needs pointer marshaling
            var needsGlobalMarshalHelpers = functions.Any(f =>
                IsByteVectorType(f.ReturnType) ||
                f.Parameters.Any(p => IsByteVectorType(p.Type)) ||
                GetMarshal(f.ReturnType, knownClassesGlobal, globalKnownClasses) != null ||
                f.Parameters.Any(p => GetMarshal(p.Type, knownClassesGlobal, globalKnownClasses) != null));
            if (needsGlobalMarshalHelpers)
                foreach (var helperLine in MarshalHelpers.Split('\n'))
                    lines.Add(helperLine.TrimEnd());
            int suffix = 1;
            foreach (var func in functions)
            {
                var rawReturn = MapCppTypeToCSharp(func.ReturnType);
                var dllName = string.IsNullOrWhiteSpace(libraryName) ? "NativeLib" : libraryName;
                var safeName = MakeSafeIdentifier(func.Name);
                if (lines.Exists(l => l.Contains($" {safeName}(") ))
                    safeName = $"{safeName}_{suffix++}";

                var retIsByteVec = IsByteVectorType(func.ReturnType);
                var retMarshal = retIsByteVec ? null : GetMarshal(func.ReturnType, knownClassesGlobal, globalKnownClasses);
                var managedRet = retIsByteVec ? "byte[]" : (retMarshal?.ManagedType ?? rawReturn);

                // Determine per-parameter marshal rules
                bool needsWrapper = retIsByteVec || retMarshal != null;
                var paramMarshalList = new List<TypeMarshal?>();
                var paramIsByteVec = new List<bool>();
                for (int pi = 0; pi < func.Parameters.Count; pi++)
                {
                    var isBV = IsByteVectorType(func.Parameters[pi].Type);
                    paramIsByteVec.Add(isBV);
                    var pm = isBV ? null : GetMarshal(func.Parameters[pi].Type, knownClassesGlobal, globalKnownClasses);
                    paramMarshalList.Add(pm);
                    if (isBV || pm != null) needsWrapper = true;
                }

                // Build raw extern parameter list (byte-vectors expand to ptr+len pairs)
                var externParamList = new List<string>();
                for (int pi = 0; pi < func.Parameters.Count; pi++)
                {
                    var ep = SafeParamName(func.Parameters[pi], pi);
                    if (paramIsByteVec[pi])
                    {
                        externParamList.Add($"IntPtr {ep}");
                        externParamList.Add($"UIntPtr {ep}Len");
                    }
                    else
                    {
                        externParamList.Add($"{MapCppTypeToCSharp(func.Parameters[pi].Type)} {ep}");
                    }
                }
                // byte-vector return needs an out-size param appended to the extern
                if (retIsByteVec) externParamList.Add("out UIntPtr _outSize");

                if (needsWrapper)
                {
                    // Emit private extern aliased to the real native entry point
                    var externAlias = $"{safeName}_Extern";
                    lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl, EntryPoint = \"{func.Name}\")]");
                    lines.Add($"    private static extern {rawReturn} {externAlias}({string.Join(", ", externParamList)});");

                    // Build managed wrapper with friendly types
                    var managedParams = new List<string>();
                    var preamble = new List<string>();
                    var postamble = new List<string>();
                    var callArgs = new List<string>();
                    for (int pi = 0; pi < func.Parameters.Count; pi++)
                    {
                        var p = func.Parameters[pi];
                        var pname = SafeParamName(p, pi);
                        if (paramIsByteVec[pi])
                        {
                            managedParams.Add($"byte[] {pname}");
                            var gcName = $"_gc_{pname}";
                            preamble.Add($"var {gcName} = GCHandle.Alloc({pname}, GCHandleType.Pinned);");
                            postamble.Add($"{gcName}.Free();");
                            callArgs.Add($"{gcName}.AddrOfPinnedObject()");
                            callArgs.Add($"(UIntPtr){pname}.Length");
                        }
                        else
                        {
                            var pm = paramMarshalList[pi];
                            if (pm != null)
                            {
                                managedParams.Add($"{pm.ManagedType} {pname}");
                                var rawName = $"_raw_{pname}";
                                preamble.Add($"var {rawName} = {string.Format(pm.ToNative, pname)};");
                                if (pm.ToNative.Contains("MarshalString"))
                                    postamble.Add($"FreeNative({rawName});");
                                callArgs.Add(rawName);
                            }
                            else
                            {
                                managedParams.Add($"{MapCppTypeToCSharp(p.Type)} {pname}");
                                callArgs.Add(pname);
                            }
                        }
                    }
                    if (retIsByteVec) callArgs.Add("out var _outSize");

                    lines.Add($"    public static {managedRet} {safeName}({string.Join(", ", managedParams)})");
                    lines.Add("    {");
                    foreach (var pre in preamble) lines.Add($"        {pre}");
                    var rawCall = $"{externAlias}({string.Join(", ", callArgs)})";
                    if (retIsByteVec)
                    {
                        lines.Add($"        var _ptr = {rawCall};");
                        foreach (var post in postamble) lines.Add($"        {post}");
                        lines.Add($"        return MarshalPtrToByteArray(_ptr, _outSize);");
                    }
                    else if (rawReturn == "void")
                    {
                        lines.Add($"        {rawCall};");
                        foreach (var post in postamble) lines.Add($"        {post}");
                    }
                    else if (retMarshal != null)
                    {
                        lines.Add($"        var _ret = {rawCall};");
                        foreach (var post in postamble) lines.Add($"        {post}");
                        lines.Add($"        return {string.Format(retMarshal.FromNative, "_ret")};");
                    }
                    else
                    {
                        if (postamble.Count > 0)
                        {
                            lines.Add($"        var _ret = {rawCall};");
                            foreach (var post in postamble) lines.Add($"        {post}");
                            lines.Add($"        return _ret;");
                        }
                        else
                        {
                            lines.Add($"        return {rawCall};");
                        }
                    }
                    lines.Add("    }");
                }
                else
                {
                    // No marshaling needed — emit a plain public extern
                    var csParams = MapParameters(func.Parameters);
                    lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                    lines.Add($"    public static extern {rawReturn} {safeName}({csParams});");
                }
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
                    // Reuse the known wrapper-class names built earlier
                    var knownClasses = knownClassesGlobal;
                    // Detect all-static classes: all non-constructor methods are static and there are no instance fields
                    var nonCtorMethods = cls.Methods.Where(m => m.Name != cls.Name).ToList();
                    var isAllStatic = nonCtorMethods.Count > 0
                                   && nonCtorMethods.All(m => m.IsStatic)
                                   && cls.Fields.Count == 0;
                    lines.Add("");
                    lines.Add(isAllStatic ? $"public static class {cls.Name}" : $"public class {cls.Name}");
                    lines.Add("{");
                    if (!isAllStatic)
                    {
                        lines.Add("    private IntPtr _native;");
                        lines.Add("    /// <summary>Exposes the raw native handle for interop use.</summary>");
                        lines.Add("    public IntPtr _Handle => _native;");
                    }
                    // Inject marshal helpers once per class
                    foreach (var helperLine in MarshalHelpers.Split('\n'))
                        lines.Add(helperLine.TrimEnd());
                    lines.Add("");
                    // constructors: methods with same name (skipped for all-static classes)
                    var ctors = cls.Methods.FindAll(m => m.Name == cls.Name);
                    if (!isAllStatic && ctors.Count > 0)
                    {
                        // Deduplicate constructor overloads by their mapped C# parameter type signature
                        var emittedCtorSigs = new HashSet<string>();
                        int ci = 0;
                        foreach (var ctor in ctors)
                        {
                            var ctorTypeKey = string.Join(",", ctor.Parameters.Select(p => MapCppTypeToCSharp(p.Type)));
                            if (!emittedCtorSigs.Add(ctorTypeKey)) continue;

                            var pname = $"{cls.Name}_Create" + (ci == 0 ? string.Empty : "_" + ci);

                            // Extern declaration always uses raw types
                            var externParamList = new List<string>();
                            for (int pi = 0; pi < ctor.Parameters.Count; pi++)
                                externParamList.Add($"{MapCppTypeToCSharp(ctor.Parameters[pi].Type)} {SafeParamName(ctor.Parameters[pi], pi)}");
                            lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                            lines.Add($"    private static extern IntPtr {pname}({string.Join(", ", externParamList)});");

                            // Managed constructor uses friendly types
                            var managedCtorParams = new List<string>();
                            var ctorCallArgs = new List<string>();
                            var ctorPreamble = new List<string>();
                            var ctorPostamble = new List<string>();
                            for (int pi = 0; pi < ctor.Parameters.Count; pi++)
                            {
                                var p = ctor.Parameters[pi];
                                var pname2 = SafeParamName(p, pi);
                                var marshal = GetMarshal(p.Type, knownClasses, globalKnownClasses);
                                if (marshal != null)
                                {
                                    managedCtorParams.Add($"{marshal.ManagedType} {pname2}");
                                    var rawName = $"_raw_{pname2}";
                                    ctorPreamble.Add($"var {rawName} = {string.Format(marshal.ToNative, pname2)};");
                                    // free pinned strings after call
                                    if (marshal.ToNative.Contains("MarshalString"))
                                        ctorPostamble.Add($"FreeNative({rawName});");
                                    ctorCallArgs.Add(rawName);
                                }
                                else
                                {
                                    managedCtorParams.Add($"{MapCppTypeToCSharp(p.Type)} {pname2}");
                                    ctorCallArgs.Add(pname2);
                                }
                            }
                            if (ctorPreamble.Count == 0 && ctorPostamble.Count == 0)
                            {
                                lines.Add($"    public {cls.Name}({string.Join(", ", managedCtorParams)}) {{ _native = {pname}({string.Join(", ", ctorCallArgs)}); }}");
                            }
                            else
                            {
                                lines.Add($"    public {cls.Name}({string.Join(", ", managedCtorParams)})");
                                lines.Add("    {");
                                foreach (var pre in ctorPreamble) lines.Add($"        {pre}");
                                lines.Add($"        _native = {pname}({string.Join(", ", ctorCallArgs)});");
                                foreach (var post in ctorPostamble) lines.Add($"        {post}");
                                lines.Add("    }");
                            }
                            ci++;
                        }
                        // Emit the raw-handle constructor only if no emitted ctor already has a single IntPtr signature
                        if (!emittedCtorSigs.Contains("IntPtr"))
                        {
                            lines.Add($"    /// <summary>Wraps an existing native pointer. Does not take ownership.</summary>");
                            lines.Add($"    public {cls.Name}(IntPtr nativeHandle) {{ _native = nativeHandle; }}");
                        }
                    }
                    else if (!isAllStatic)
                    {
                        // No C++ ctor found — emit a native Create factory + default + raw-handle constructors
                        lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                        lines.Add($"    private static extern IntPtr {cls.Name}_Create();");
                        lines.Add($"    /// <summary>Creates a new native instance via the default constructor.</summary>");
                        lines.Add($"    public {cls.Name}() {{ _native = {cls.Name}_Create(); }}");
                        lines.Add($"    /// <summary>Wraps an existing native pointer. Does not take ownership.</summary>");
                        lines.Add($"    public {cls.Name}(IntPtr nativeHandle) {{ _native = nativeHandle; }}");
                    }

                    // methods: generate extern + managed wrapper
                    var emittedMethodSignatures = new HashSet<string>();
                    foreach (var method in cls.Methods)
                    {
                        if (method.Name == cls.Name) continue; // skip ctor already handled
                        var methodName = MakeSafeIdentifier(method.Name);
                        var externName = $"{cls.Name}_{methodName}";
                        // Dedup by extern name + raw param types
                        var typeKey = string.Join(",", method.Parameters.Select(p => MapCppTypeToCSharp(p.Type)));
                        var sigKey = $"{externName}({typeKey})";
                        if (emittedMethodSignatures.Contains(sigKey)) continue;
                        emittedMethodSignatures.Add(sigKey);

                        // ── extern declaration (always raw types) ──
                        // Static methods have no instance pointer parameter
                        var isStaticMethod = method.IsStatic || isAllStatic;
                        var externParamList2 = isStaticMethod
                            ? new List<string>()
                            : new List<string> { "IntPtr instance" };
                        var methodParamIsByteVec = new List<bool>();
                        for (int pi = 0; pi < method.Parameters.Count; pi++)
                        {
                            var isBV = IsByteVectorType(method.Parameters[pi].Type);
                            methodParamIsByteVec.Add(isBV);
                            var ep = SafeParamName(method.Parameters[pi], pi);
                            if (isBV)
                            {
                                externParamList2.Add($"IntPtr {ep}");
                                externParamList2.Add($"UIntPtr {ep}Len");
                            }
                            else
                            {
                                externParamList2.Add($"{MapCppTypeToCSharp(method.Parameters[pi].Type)} {ep}");
                            }
                        }
                        var rawRet = MapCppTypeToCSharp(method.ReturnType);
                        var retIsByteVec2 = IsByteVectorType(method.ReturnType);
                        if (retIsByteVec2) externParamList2.Add("out UIntPtr _outSize");
                        lines.Add($"    [DllImport(\"{dllName}\", CallingConvention = CallingConvention.Cdecl)]");
                        lines.Add($"    private static extern {rawRet} {externName}({string.Join(", ", externParamList2)});");

                        // ── managed wrapper (friendly types) ──
                        var retMarshal = retIsByteVec2 ? null : GetMarshal(method.ReturnType, knownClasses, globalKnownClasses);
                        var managedRet = retIsByteVec2 ? "byte[]" : (retMarshal?.ManagedType ?? rawRet);

                        var managedParamList2 = new List<string>();
                        var preamble = new List<string>();
                        var postamble = new List<string>();
                        var callArgs2 = isStaticMethod ? new List<string>() : new List<string> { "_native" };
                        for (int pi = 0; pi < method.Parameters.Count; pi++)
                        {
                            var p = method.Parameters[pi];
                            var pname2 = SafeParamName(p, pi);
                            if (methodParamIsByteVec[pi])
                            {
                                managedParamList2.Add($"byte[] {pname2}");
                                var gcName = $"_gc_{pname2}";
                                preamble.Add($"var {gcName} = GCHandle.Alloc({pname2}, GCHandleType.Pinned);");
                                postamble.Add($"{gcName}.Free();");
                                callArgs2.Add($"{gcName}.AddrOfPinnedObject()");
                                callArgs2.Add($"(UIntPtr){pname2}.Length");
                            }
                            else
                            {
                                var pm = GetMarshal(p.Type, knownClasses, globalKnownClasses);
                                if (pm != null)
                                {
                                    managedParamList2.Add($"{pm.ManagedType} {pname2}");
                                    var rawName = $"_raw_{pname2}";
                                    preamble.Add($"var {rawName} = {string.Format(pm.ToNative, pname2)};");
                                    if (pm.ToNative.Contains("MarshalString"))
                                        postamble.Add($"FreeNative({rawName});");
                                    callArgs2.Add(rawName);
                                }
                                else
                                {
                                    managedParamList2.Add($"{MapCppTypeToCSharp(p.Type)} {pname2}");
                                    callArgs2.Add(pname2);
                                }
                            }
                        }
                        if (retIsByteVec2) callArgs2.Add("out var _outSize");

                        var managedMethodModifier = isStaticMethod ? "static " : "";
                        lines.Add($"    public {managedMethodModifier}{managedRet} {methodName}({string.Join(", ", managedParamList2)})");
                        lines.Add("    {");
                        foreach (var pre in preamble) lines.Add($"        {pre}");
                        var rawCall = $"{externName}({string.Join(", ", callArgs2)})";
                        if (retIsByteVec2)
                        {
                            lines.Add($"        var _ptr = {rawCall};");
                            foreach (var post in postamble) lines.Add($"        {post}");
                            lines.Add($"        return MarshalPtrToByteArray(_ptr, _outSize);");
                        }
                        else if (rawRet == "void")
                        {
                            lines.Add($"        {rawCall};");
                            foreach (var post in postamble) lines.Add($"        {post}");
                        }
                        else if (retMarshal != null)
                        {
                            lines.Add($"        var _ret = {rawCall};");
                            foreach (var post in postamble) lines.Add($"        {post}");
                            lines.Add($"        return {string.Format(retMarshal.FromNative, "_ret")};");
                        }
                        else
                        {
                            if (postamble.Count > 0)
                            {
                                lines.Add($"        var _ret = {rawCall};");
                                foreach (var post in postamble) lines.Add($"        {post}");
                                lines.Add($"        return _ret;");
                            }
                            else
                            {
                                lines.Add($"        return {rawCall};");
                            }
                        }
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

        // Returns the TypeMarshal rule for a C++ type, accounting for known wrapper classes.
        // knownClasses: names of C++ classes in the current file; globalKnownClasses covers all files in the directory.
        private TypeMarshal? GetMarshal(string cppType, ISet<string> knownClasses, ISet<string>? globalKnownClasses = null)
        {
            if (string.IsNullOrWhiteSpace(cppType)) return null;
            var stripped = StripForMarshal(cppType);

            // Built-in rules first (strings etc.)
            if (BuiltinMarshalRules.TryGetValue(stripped, out var rule)) return rule;
            // Also match original before stripping
            if (BuiltinMarshalRules.TryGetValue(cppType.Trim(), out rule)) return rule;

            // If the stripped type is a known wrapper class in this file, marshal it as a handle
            if (!string.IsNullOrEmpty(stripped) && knownClasses.Contains(stripped))
                return new TypeMarshal(stripped, "{0}._Handle", $"new {stripped}({{0}})");

            // Fall back to global class registry for cross-file type marshaling
            if (globalKnownClasses != null && !string.IsNullOrEmpty(stripped) && globalKnownClasses.Contains(stripped))
                return new TypeMarshal(stripped, "{0}._Handle", $"new {stripped}({{0}})");

            return null;
        }

        // Returns true when the C++ type is a byte-array vector (std::vector<uint8_t> etc.)
        // These get special marshaling: expanded to ptr+len on the extern, byte[] on the managed side.
        private static bool IsByteVectorType(string cppType)
        {
            if (string.IsNullOrWhiteSpace(cppType)) return false;
            var t = StripForMarshal(cppType);
            return t is "std::vector<uint8_t>" or "std::vector<unsigned char>"
                      or "std::vector<char>"   or "std::vector<byte>"
                      or "vector<uint8_t>"     or "vector<unsigned char>";
        }

        // Strips qualifiers, pointers and refs to get the base type name for marshal lookup.
        private static string StripForMarshal(string cppType)
        {
            var t = cppType.Trim();
            foreach (var q in new[] { "constexpr ", "inline ", "virtual ", "explicit ", "override ", "final ", "static ", "volatile ", "const " })
                while (t.StartsWith(q)) t = t.Substring(q.Length).TrimStart();
            while (t.EndsWith(" const") || t.EndsWith(" &") || t.EndsWith("&") || t.EndsWith("*"))
            {
                if (t.EndsWith(" const")) t = t.Substring(0, t.Length - 6).TrimEnd();
                else if (t.EndsWith(" &"))  t = t.Substring(0, t.Length - 2).TrimEnd();
                else                          t = t.Substring(0, t.Length - 1).TrimEnd();
            }
            return t.Trim();
        }

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
                return "Adamantite";
            var parts = dir.Split('/').Select(p => System.Text.RegularExpressions.Regex.Replace(p, "[^A-Za-z0-9_]", "_"));
            return "AdamantiteBindings." + string.Join(".", parts);
        }
    }
}
