namespace FileExplorer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class GeneratorTask : Task
    {
        [Required]
        public string OutputFile { get; set; } = "";

        [Required]
        public ITaskItem[] SourceFiles { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string TypeName { get; set; } = "";

        public override bool Execute()
        {
            if (string.IsNullOrWhiteSpace(OutputFile))
            {
                Log.LogError($"{nameof(OutputFile)} is not set");
                return false;
            }

            if (string.IsNullOrWhiteSpace(TypeName))
            {
                Log.LogError($"{nameof(TypeName)} is not set");
                return false;
            }

            try
            {
                var files = SourceFiles
                    .Select(item => item.ItemSpec)
                    .Distinct()
                    .ToArray();

                var code = GenerateCode(files);

                var target = new FileInfo(OutputFile);

                if (target.Exists)
                {
                    // Only try writing if the contents are different. Don't cause a rebuild
                    var contents = File.ReadAllText(target.FullName, Encoding.UTF8);
                    if (string.Equals(contents, code, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                using var file = File.Open(target.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
                using var sw = new StreamWriter(file, Encoding.UTF8);

                sw.Write(code);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return false;
            }

            return true;
        }

        // Super simple codegen, see my other answer for something more sophisticated.
        string GenerateCode(IEnumerable<string> files)
        {
            var (namespaceName, typeName) = SplitLast(TypeName, '.');

            var code = $@"
// Generated code, do not edit.
namespace {namespaceName ?? "FileExplorer"}
{{
    public static class {typeName}
    {{
        {string.Join($"{Environment.NewLine}\t\t", files.Select(GenerateProperty))}
    }}
}}";

            static string GenerateProperty(string file)
            {
                var name = file
                    .ToCharArray()
                    .Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_')
                    .ToArray();

                return $"public static readonly string {new string(name)} = \"{file.Replace("\\", "\\\\")}\";";
            }

            static (string?, string) SplitLast(string text, char delimiter)
            {
                var index = text.LastIndexOf(delimiter);

                return index == -1
                    ? (null, text)
                    : (text.Substring(0, index), text.Substring(index + 1));
            }

            return code;
        }
    }
}