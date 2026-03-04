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
}
