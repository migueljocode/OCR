namespace TxtCombiner;

// ───────────────────────────────────────────────
//  ConsoleHelper  —  reusable console I/O
// ───────────────────────────────────────────────
internal static class ConsoleHelper
{
    internal static string Ask(string prompt)
    {
        Console.Write($"{prompt}: ");
        string? value = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"'{prompt}' cannot be empty.");
        return value;
    }

    internal static void Done(string message)
    {
        Console.WriteLine($"\n✓ {message}");
        Console.Write("Press any key to exit...");
        Console.Read();
    }
}

// ───────────────────────────────────────────────
//  TextMerger  —  ordered text file combiner
// ───────────────────────────────────────────────
internal static class TextMerger
{
    private static readonly Regex NumberPattern = new(@"\d+", RegexOptions.Compiled);

    internal static void CombinePageFiles(string inputDir, string outputDir)
    {
        if (!Directory.Exists(inputDir))
            throw new DirectoryNotFoundException($"Input directory not found: {inputDir}");

        Directory.CreateDirectory(outputDir);

        string[] files = Directory.GetFiles(inputDir, "Page_*.txt");
        if (files.Length == 0)
            throw new FileNotFoundException($"No Page_*.txt files found in '{inputDir}'.");

        // Sort by the numeric part of the filename
        Array.Sort(files, (a, b) =>
            ExtractNumber(Path.GetFileName(a))
                .CompareTo(ExtractNumber(Path.GetFileName(b))));

        Console.WriteLine($"Found {files.Length} file(s) to combine.");

        var output   = new StringBuilder();
        string outPath = Path.Combine(outputDir, "combined_text.txt");

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            Console.WriteLine($"  + {name}");

            output.AppendLine($"\n\n\n=== output from file: {name} ===");
            output.AppendLine(File.ReadAllText(file));
        }

        File.WriteAllText(outPath, output.ToString().Trim(), Encoding.UTF8);
        Console.WriteLine($"Output saved to: {outPath}");
    }

    private static int ExtractNumber(string input)
    {
        var match = NumberPattern.Match(input);
        return match.Success ? int.Parse(match.Value) : 0;
    }
}
