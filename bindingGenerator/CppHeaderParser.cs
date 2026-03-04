using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Adamantite.BindingGenerator
{
    public class CppHeaderParser
    {
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
                    var returnType = match.Groups[1].Value.Trim();
                    var name = match.Groups[2].Value.Trim();
                    var rawParams = match.Groups[3].Value.Trim();

                    var parameters = ParseParameters(rawParams);

                    // Skip operators and templates and likely macros
                    if (name.StartsWith("operator"))
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

                // find opening brace
                while (i < content.Length && content[i] != '{') i++;
                if (i >= content.Length) break;
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

                // split members by ';' and parse each
                var members = body.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var functionPattern = new Regex(@"([\w\:\<\>\s\*&]+?)\s+([A-Za-z_]\w*)\s*\(([^\)]*)\)", RegexOptions.Compiled);
                foreach (var mem in members)
                {
                    var mstr = mem.Trim();
                    if (string.IsNullOrWhiteSpace(mstr)) continue;

                    var fmatch = functionPattern.Match(mstr);
                    if (fmatch.Success)
                    {
                        var returnType = fmatch.Groups[1].Value.Trim();
                        var fname = fmatch.Groups[2].Value.Trim();
                        var rawParams = fmatch.Groups[3].Value.Trim();
                        var parameters = ParseParameters(rawParams);
                        cppClass.Methods.Add(new CppFunction { ReturnType = returnType, Name = fname, Parameters = parameters });
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
                    result.Add(new CppParameter { Type = tokens[0].Trim(), Name = string.Empty });
                }
                else
                {
                    var name = tokens[tokens.Length - 1];
                    var type = string.Join(" ", tokens, 0, tokens.Length - 1);
                    result.Add(new CppParameter { Type = type.Trim(), Name = name.Trim() });
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
