using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Adamantite.BindingGenerator
{
    public class CppHeaderParser
    {
        // C/C++ keywords that should never appear as function/method names
        private static readonly HashSet<string> CppKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "if", "else", "for", "while", "do", "switch", "case", "break", "continue",
            "return", "goto", "try", "catch", "throw", "new", "delete",
            "class", "struct", "union", "enum", "namespace", "template", "typename",
            "public", "private", "protected", "virtual", "override", "final",
            "static", "inline", "const", "constexpr", "volatile", "mutable",
            "explicit", "extern", "auto", "register", "typedef", "using",
            "sizeof", "alignof", "decltype", "static_cast", "dynamic_cast",
            "reinterpret_cast", "const_cast", "nullptr", "true", "false",
            "and", "or", "not", "xor", "bitand", "bitor", "compl",
            // common standard-library function names that appear in method bodies
            "lock", "guard", "emplace", "push_back", "erase", "find", "begin", "end",
            "max", "min", "abs", "size", "empty", "clear", "count", "reserve",
            "substr", "replace", "remove", "sort", "assert"
        };

        // Strip leading/trailing C++ qualifiers from a type string
        private static string StripQualifiers(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return type;
            type = type.Trim();
            string[] leading = { "constexpr ", "inline ", "virtual ", "explicit ", "override ", "final ", "static ", "volatile ", "const " };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var q in leading)
                {
                    if (type.StartsWith(q)) { type = type.Substring(q.Length).TrimStart(); changed = true; }
                }
            }
            // strip trailing const
            while (type.EndsWith(" const")) type = type.Substring(0, type.Length - 6).TrimEnd();
            return type.Trim();
        }

        // Removes inline method bodies ({...}) from a class body string so that
        // statements inside bodies are not mistaken for member declarations.
        // Each removed body is replaced with a ';' to preserve declaration splitting.
        private static string StripInlineMethodBodies(string body)
        {
            var result = new System.Text.StringBuilder();
            int depth = 0;
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == '{')
                {
                    depth++;
                    if (depth == 1) result.Append(';'); // end the declaration before its body
                }
                else if (body[i] == '}')
                {
                    depth--;
                    // don't emit the closing brace
                }
                else if (depth == 0)
                {
                    result.Append(body[i]);
                }
            }
            return result.ToString();
        }

        public List<CppFunction> ParseFunctions(string headerFilePath)
        {
            var functions = new List<CppFunction>();
            var content = File.ReadAllText(headerFilePath);

            // Remove block comments and line comments
            content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            content = Regex.Replace(content, @"//.*?$", string.Empty, RegexOptions.Multiline);

            // Collapse newlines to simplify multi-line declarations
            content = Regex.Replace(content, @"\s+", " ");

            // Basic function prototype matcher (return_type name(params));
            var functionPattern = new Regex(@"([\w\:\<\>\s\*&]+?)\s+([A-Za-z_]\w*)\s*\(([^\)]*)\)\s*[;{]", RegexOptions.Compiled);
            var matches = functionPattern.Matches(content);
            foreach (Match match in matches)
            {
                try
                {
                    var returnType = StripQualifiers(match.Groups[1].Value.Trim());
                    var name = match.Groups[2].Value.Trim();
                    var rawParams = match.Groups[3].Value.Trim();

                    var parameters = ParseParameters(rawParams);

                    // Skip operators, templates and likely macros
                    if (name.StartsWith("operator"))
                        continue;

                    // Skip C/C++ keywords used as names (parser artefacts from inline bodies)
                    if (CppKeywords.Contains(name))
                        continue;

                    functions.Add(new CppFunction
                    {
                        ReturnType = returnType,
                        Name = name,
                        Parameters = parameters
                    });
                }
                catch
                {
                    // swallow individual parse errors and continue
                }
            }

            return functions;
        }

        public List<CppClass> ParseClasses(string headerFilePath)
        {
            var classes = new List<CppClass>();
            var raw = File.ReadAllText(headerFilePath);
            // remove comments
            var content = Regex.Replace(raw, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            content = Regex.Replace(content, @"//.*?$", string.Empty, RegexOptions.Multiline);

            int i = 0;
            // debug preview
            try
            {
                var preview = content.Length > 200 ? content.Substring(0, 200) : content;
                Console.WriteLine($"[ParseClasses] file preview: {preview.Replace('\n',' ').Replace('\r',' ')}");
            }
            catch { }
            while (i < content.Length)
            {
                // find 'class' or 'struct' keyword
                var remaining = content.Substring(i);
                var m = Regex.Match(remaining, "\\b(class|struct)\\b");
                if (m.Success) Console.WriteLine($"[ParseClasses] found keyword '{m.Groups[1].Value}' at idx {i + m.Index}");
                if (!m.Success) break;
                var kind = m.Groups[1].Value;
                i += m.Index + m.Length;

                // skip whitespace
                try { Console.WriteLine($"[ParseClasses] post-keyword at {i}: '{content.Substring(i, Math.Min(40, content.Length - i)).Replace('\n',' ').Replace('\r',' ')}'"); } catch { }

                // skip whitespace and read name
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
                var nameStart = i;
                while (i < content.Length && (char.IsLetterOrDigit(content[i]) || content[i] == '_' || content[i] == ':')) i++;
                var name = content.Substring(nameStart, i - nameStart).Trim();
                Console.WriteLine($"[ParseClasses] extracted name: '{name}' (start={nameStart}, len={i-nameStart})");

                // Skip whitespace after name to detect forward declarations
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;

                // If the very next non-whitespace is ';', this is a forward declaration — skip it
                if (i < content.Length && content[i] == ';')
                {
                    i++;
                    continue;
                }

                // find opening brace; if we hit ';' first it's also a forward declaration
                while (i < content.Length && content[i] != '{' && content[i] != ';') i++;
                if (i >= content.Length || content[i] == ';')
                {
                    if (i < content.Length) i++; // skip ';'
                    continue;
                }
                i++; // past '{'
                var bodyStart = i;
                int depth = 1;
                while (i < content.Length && depth > 0)
                {
                    if (content[i] == '{') depth++;
                    else if (content[i] == '}') depth--;
                    i++;
                }
                var bodyEnd = i - 1; // position of matching '}'
                if (depth != 0) break; // unbalanced

                var body = content.Substring(bodyStart, bodyEnd - bodyStart);

                var cppClass = new CppClass { Name = name, IsStruct = kind == "struct" };

                // Remove inline method bodies so their statements aren't parsed as member declarations
                var cleanBody = StripInlineMethodBodies(body);

                // split members by ';' and parse each
                var members = cleanBody.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var functionPattern = new Regex(@"([\w\:\<\>\s\*&]+?)\s+([A-Za-z_]\w*)\s*\(([^\)]*)\)", RegexOptions.Compiled);
                foreach (var mem in members)
                {
                    var mstr = mem.Trim();
                    if (string.IsNullOrWhiteSpace(mstr)) continue;

                    // Strip C++ access specifiers that may be fused with the next declaration
                    // (e.g. "public:\n    void Foo()" after splitting by ';')
                    mstr = Regex.Replace(mstr, @"^(?:public|private|protected)\s*:\s*", string.Empty, RegexOptions.Singleline).TrimStart();
                    if (string.IsNullOrWhiteSpace(mstr)) continue;

                    // Strip constructor initializer list: "Foo() : X(0), Y(0)..." → "Foo()"
                    mstr = Regex.Replace(mstr, @"\)\s*:(?!:).*$", ")", RegexOptions.Singleline);

                    var fmatch = functionPattern.Match(mstr);
                    if (fmatch.Success)
                    {
                        var returnTypeRaw = fmatch.Groups[1].Value.Trim();
                        var isStaticMethod = returnTypeRaw.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries).Contains("static")
                                          || mstr.TrimStart().StartsWith("static ");
                        var returnType = StripQualifiers(returnTypeRaw);
                        var fname = fmatch.Groups[2].Value.Trim();
                        var rawParams = fmatch.Groups[3].Value.Trim();

                        // Skip C/C++ keywords parsed as names from inline method bodies
                        if (CppKeywords.Contains(fname))
                            continue;
                        if (fname.StartsWith("operator"))
                            continue;

                        var parameters = ParseParameters(rawParams);
                        cppClass.Methods.Add(new CppFunction { ReturnType = returnType, Name = fname, Parameters = parameters, IsStatic = isStaticMethod });
                    }
                    else
                    {
                        // skip static members or methods with bodies
                        if (mstr.StartsWith("static") || mstr.Contains("=") && !mstr.Contains("("))
                        {
                            // try to treat static const fields as fields (e.g. static const Color White)
                            if (mstr.StartsWith("static") && mstr.Contains("const") && mstr.Contains(" "))
                            {
                                // attempt to extract last token as name
                                var toks = mstr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                var nameToken = toks[toks.Length - 1];
                                var typeToken = string.Join(" ", toks, 0, toks.Length - 1);
                                cppClass.Fields.Add(new CppParameter { Type = typeToken.Trim(), Name = nameToken.Trim() });
                            }
                            continue;
                        }

                        // try parse field: support "type name1, name2" patterns
                        var fieldPattern = new Regex(@"^([\w\:\<\>\s\*&]+?)\s+(.+)$", RegexOptions.Compiled);
                        var f = fieldPattern.Match(mstr);
                        if (f.Success)
                        {
                            var typeToken = f.Groups[1].Value.Trim();
                            var namesPart = f.Groups[2].Value.Trim();
                            var names = namesPart.Split(',');
                            foreach (var nm in names)
                            {
                                var clean = nm.Trim();
                                // remove default initializers
                                var eq = clean.IndexOf('='); if (eq >= 0) clean = clean.Substring(0, eq).Trim();
                                // remove array brackets
                                clean = clean.Replace("[]", "");
                                // if name still contains spaces, take last token
                                var nameTok = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (nameTok.Length == 0) continue;
                                var fieldName = nameTok[nameTok.Length - 1].Trim();
                                if (!string.IsNullOrEmpty(fieldName))
                                    cppClass.Fields.Add(new CppParameter { Type = typeToken, Name = fieldName });
                            }
                        }
                    }
                }

                classes.Add(cppClass);
            }

            return classes;
        }

        private List<CppParameter> ParseParameters(string rawParams)
        {
            var result = new List<CppParameter>();
            if (string.IsNullOrWhiteSpace(rawParams) || rawParams.Trim() == "void")
                return result;

            // Split parameters by commas not inside <> (templates)
            var parts = Regex.Split(rawParams, ",(?![^<>]*>)");
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                // Remove default values
                var equalIndex = p.IndexOf('=');
                if (equalIndex >= 0) p = p.Substring(0, equalIndex).Trim();

                // Try to separate type and name (name may be absent)
                var tokens = p.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 1)
                {
                    result.Add(new CppParameter { Type = StripQualifiers(tokens[0].Trim()), Name = string.Empty });
                }
                else
                {
                    // last token is the parameter name; rest is the type
                    var name = tokens[tokens.Length - 1];
                    // strip pointer/ref from name if accidentally captured there
                    name = name.TrimStart('*', '&');
                    var type = StripQualifiers(string.Join(" ", tokens, 0, tokens.Length - 1).Trim());
                    // if name is a keyword or empty, treat whole thing as unnamed type
                    if (string.IsNullOrEmpty(name))
                        result.Add(new CppParameter { Type = type, Name = string.Empty });
                    else
                        result.Add(new CppParameter { Type = type, Name = name.Trim() });
                }
            }

            return result;
        }
    }

    public class CppFunction
    {
        public string ReturnType { get; set; }
        public string Name { get; set; }
        public List<CppParameter> Parameters { get; set; } = new List<CppParameter>();
        public bool IsStatic { get; set; }
    }

    public class CppParameter
    {
        public string Type { get; set; }
        public string Name { get; set; }
    }

    public class CppClass
    {
        public string Name { get; set; }
        public bool IsStruct { get; set; }
        public List<CppParameter> Fields { get; set; } = new List<CppParameter>();
        public List<CppFunction> Methods { get; set; } = new List<CppFunction>();
    }
}
