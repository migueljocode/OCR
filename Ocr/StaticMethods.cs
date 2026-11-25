namespace Ocr;

internal enum InputKind { Pdf, Image, Directory }
internal enum OutputMode { SingleFile, PerItem }
internal enum SortBy { Name, Number, Date, Size }

internal sealed record RunOptions(
    string InputPath,
    string OutputDir,
    InputKind Kind,
    string Language,
    OutputMode OutputMode,
    SortBy SortBy,
    int Width,
    int Height,
    float Scale,
    bool Interactive);

// ───────────────────────────────────────────────
//  AppRunner  —  top-level exception boundary
// ───────────────────────────────────────────────
internal static class AppRunner
{
    internal static int Execute(Action action)
    {
        try
        {
            action();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n✗ Error: {ex.InnerException?.Message ?? ex.Message}");
            return 1;
        }
    }
}

// ───────────────────────────────────────────────
//  ConsoleHelper  —  reusable console I/O
// ───────────────────────────────────────────────
internal static class ConsoleHelper
{
    internal static string Ask(string prompt, string? defaultValue = null)
    {
        string suffix = defaultValue is null ? ": " : $" [{defaultValue}]: ";
        Console.Write($"{prompt}{suffix}");
        string? value = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            if (defaultValue is not null)
                return defaultValue;
            throw new ArgumentException($"'{prompt}' cannot be empty.");
        }

        return value;
    }

    internal static int AskInt(string prompt, int defaultValue)
    {
        string raw = Ask(prompt, defaultValue.ToString());
        if (!int.TryParse(raw, out int result) || result <= 0)
            throw new ArgumentException($"'{prompt}' must be a positive integer.");
        return result;
    }

    internal static float AskFloat(string prompt, float defaultValue)
    {
        string raw = Ask(prompt, defaultValue.ToString());
        if (!float.TryParse(raw, out float result) || result <= 0)
            throw new ArgumentException($"'{prompt}' must be a positive number.");
        return result;
    }

    internal static T AskChoice<T>(string title, (string Label, T Value)[] choices)
    {
        Console.WriteLine(title);
        for (int i = 0; i < choices.Length; i++)
            Console.WriteLine($"  {i + 1}. {choices[i].Label}");

        Console.Write($"Choice [1-{choices.Length}]: ");
        string? raw = Console.ReadLine()?.Trim();

        if (!int.TryParse(raw, out int index) || index < 1 || index > choices.Length)
            throw new ArgumentException($"Invalid choice '{raw}'.");

        return choices[index - 1].Value;
    }

    internal static void Done(string message, bool waitForKey)
    {
        Console.WriteLine($"\n✓ {message}");
        if (waitForKey)
        {
            Console.Write("Press any key to exit...");
            Console.Read();
        }
    }
}

// ───────────────────────────────────────────────
//  CliOptions  —  CLI parsing, -h help, prompts
// ───────────────────────────────────────────────
internal static class CliOptions
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp", ".gif"
    };

    internal static bool IsSupportedImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path));

    internal static bool TryParse(string[] args, out RunOptions options)
    {
        if (args.Any(a => a is "-h" or "--help" or "-?"))
        {
            PrintHelp();
            options = null!;
            return false;
        }

        if (args.Length == 0)
        {
            options = PromptInteractive();
            return true;
        }

        options = ParseArgs(args);
        return true;
    }

    private static RunOptions PromptInteractive()
    {
        string inputPath = ConsoleHelper.Ask("Input path (PDF, image file, or image directory)");
        InputKind kind   = DetectInputKind(inputPath);

        string defaultOut = kind == InputKind.Directory
            ? Path.Combine(inputPath, "ocr_output")
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputPath))!, "ocr_output");

        string outputDir = ConsoleHelper.Ask("Output directory", defaultOut);

        string language = ConsoleHelper.AskChoice("Select OCR language:",
        [
            ("Persian (fas)",     "fas"),
            ("English (eng)",     "eng"),
            ("Both    (fas+eng)", "fas+eng")
        ]);

        OutputMode mode = kind switch
        {
            InputKind.Image => OutputMode.PerItem,
            InputKind.Pdf => ConsoleHelper.AskChoice("PDF output mode:",
            [
                ("One text file for whole PDF", OutputMode.SingleFile),
                ("One text file per page",      OutputMode.PerItem)
            ]),
            InputKind.Directory => ConsoleHelper.AskChoice("Directory output mode:",
            [
                ("One combined text file for all images", OutputMode.SingleFile),
                ("One text file per image",               OutputMode.PerItem)
            ]),
            _ => throw new InvalidOperationException()
        };

        SortBy sortBy = SortBy.Number;
        if (kind == InputKind.Directory && mode == OutputMode.SingleFile)
        {
            sortBy = ConsoleHelper.AskChoice("Sort images by:",
            [
                ("Number in filename", SortBy.Number),
                ("File name (A-Z)",    SortBy.Name),
                ("Date modified",      SortBy.Date),
                ("File size",          SortBy.Size)
            ]);
        }

        int width = 1920, height = 1080;
        float scale = 1f;
        if (kind == InputKind.Pdf)
        {
            width  = ConsoleHelper.AskInt("PDF render width", 1920);
            height = ConsoleHelper.AskInt("PDF render height", 1080);
            scale  = ConsoleHelper.AskFloat("PDF render scale factor", 1f);
        }

        return new RunOptions(inputPath, outputDir, kind, language, mode, sortBy, width, height, scale, true);
    }

    private static RunOptions ParseArgs(string[] args)
    {
        string? input = null, output = null;
        string lang   = "fas+eng";
        var mode      = OutputMode.SingleFile;
        var sort      = SortBy.Number;
        int width     = 1920, height = 1080;
        float scale   = 1f;

        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i];
            string NextValue() => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"Missing value for '{flag}'.");

            switch (flag)
            {
                case "-i" or "--input":
                    input = NextValue();
                    break;
                case "-o" or "--output":
                    output = NextValue();
                    break;
                case "-l" or "--lang":
                    lang = NextValue().ToLowerInvariant() switch
                    {
                        "fas" or "eng" or "fas+eng" or "eng+fas" => args[i].ToLowerInvariant(),
                        _ => throw new ArgumentException("Language must be 'fas', 'eng', or 'fas+eng'.")
                    };
                    break;
                case "-m" or "--mode":
                    mode = NextValue().ToLowerInvariant() switch
                    {
                        "single" => OutputMode.SingleFile,
                        "per-page" or "per-file" or "each" => OutputMode.PerItem,
                        _ => throw new ArgumentException("Mode must be 'single' or 'each'.")
                    };
                    break;
                case "-s" or "--sort":
                    sort = NextValue().ToLowerInvariant() switch
                    {
                        "name"   => SortBy.Name,
                        "number" => SortBy.Number,
                        "date"   => SortBy.Date,
                        "size"   => SortBy.Size,
                        _ => throw new ArgumentException("Sort must be 'number', 'name', 'date', or 'size'.")
                    };
                    break;
                case "--width":
                    if (!int.TryParse(NextValue(), out width) || width <= 0)
                        throw new ArgumentException("--width must be a positive integer.");
                    break;
                case "--height":
                    if (!int.TryParse(NextValue(), out height) || height <= 0)
                        throw new ArgumentException("--height must be a positive integer.");
                    break;
                case "--scale":
                    if (!float.TryParse(NextValue(), out scale) || scale <= 0)
                        throw new ArgumentException("--scale must be a positive number.");
                    break;
                default:
                    if (!flag.StartsWith('-') && input is null)
                        input = flag;
                    else
                        throw new ArgumentException($"Unknown option '{flag}'. Use -h for help.");
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input path is required. Use -h for help.");

        InputKind kind = DetectInputKind(input);
        if (kind == InputKind.Image)
            mode = OutputMode.PerItem;

        output ??= kind == InputKind.Directory
            ? Path.Combine(input, "ocr_output")
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!, "ocr_output");

        return new RunOptions(input, output, kind, lang, mode, sort, width, height, scale, false);
    }

    private static InputKind DetectInputKind(string path)
    {
        if (Directory.Exists(path))
            return InputKind.Directory;

        if (!File.Exists(path))
            throw new FileNotFoundException($"Input path not found: '{path}'");

        if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return InputKind.Pdf;

        if (IsSupportedImage(path))
            return InputKind.Image;

        throw new NotSupportedException(
            $"Unsupported file type '{Path.GetExtension(path)}'. Expected .pdf or an image ({string.Join(", ", ImageExtensions)}).");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Ocr — Unified PDF, Image, and Directory OCR Tool

            Usage:
              Ocr                              Run in interactive mode (prompts for options)
              Ocr -i <path> [options]          Run with command-line arguments
              Ocr -h | --help                  Show this help message

            Options:
              -i, --input <path>               Input PDF file, image file, or directory of images
              -o, --output <dir>               Output directory (default: <input_dir>/ocr_output)
              -l, --lang <lang>                OCR language: fas | eng | fas+eng (default: fas+eng)
              -m, --mode <mode>                Output mode for PDF or directory:
                                                 single  One combined .txt file (default)
                                                 each    One .txt file per page/image
              -s, --sort <by>                  Sort order for combined directory output:
                                                 number  By number in filename (default)
                                                 name    Alphabetically by filename
                                                 date    By last modified time
                                                 size    By file size
                  --width <px>                 PDF render width  (default: 1920)
                  --height <px>                PDF render height (default: 1080)
                  --scale <factor>             PDF render scale  (default: 1)
              -h, --help                       Show this help

            Supported Inputs:
              • PDF (.pdf)
              • Images (.png, .jpg, .jpeg, .bmp, .tif, .tiff, .webp, .gif)
              • Directory containing any supported image types

            Examples:
              Ocr -i document.pdf -o ./out -l fas+eng -m single
              Ocr -i ./scans -o ./out -l fas -m single -s number
              Ocr -i photo.jpg -o ./out -l eng
            """);
    }
}

// ───────────────────────────────────────────────
//  TesseractSetup  —  OS paths & traineddata
// ───────────────────────────────────────────────
internal static class TesseractSetup
{
    private const string BaseUrl =
        "https://github.com/tesseract-ocr/tessdata_best/raw/main/";

    internal static string GetTessDataPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string[] candidates =
            [
                "/usr/share/tesseract-ocr/5/tessdata",
                "/usr/share/tesseract-ocr/4/tessdata",
                "/usr/share/tessdata"
            ];
            foreach (string dir in candidates)
                if (Directory.Exists(dir))
                    return dir;

            Directory.CreateDirectory(candidates[0]);
            return candidates[0];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string[] candidates =
            [
                @"C:\Program Files\Tesseract-OCR\tessdata",
                @"C:\Program Files (x86)\Tesseract-OCR\tessdata",
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "Tesseract-OCR", "tessdata")
            ];
            foreach (string dir in candidates)
                if (Directory.Exists(dir))
                    return dir;

            Directory.CreateDirectory(candidates[^1]);
            return candidates[^1];
        }

        throw new PlatformNotSupportedException("Only Linux and Windows are supported.");
    }

    internal static string GetTesseractExecutable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string[] candidates =
            [
                @"C:\Program Files\Tesseract-OCR\tesseract.exe",
                @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe"
            ];
            foreach (string exe in candidates)
                if (File.Exists(exe))
                    return exe;
        }

        return "tesseract";
    }

    internal static void EnsureTrainedData(params string[] languages)
    {
        string tessDataDir = GetTessDataPath();
        Console.WriteLine($"Tessdata directory: {tessDataDir}");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        foreach (string lang in languages)
        {
            string filePath = Path.Combine(tessDataDir, $"{lang}.traineddata");
            if (File.Exists(filePath))
            {
                Console.WriteLine($"  ✓ {lang}.traineddata already present.");
                continue;
            }

            string url = $"{BaseUrl}{lang}.traineddata";
            Console.Write($"  ↓ Downloading {lang}.traineddata … ");

            try
            {
                byte[] data = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(filePath, data);
                Console.WriteLine($"OK ({data.Length:N0} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED");
                throw new InvalidOperationException(
                    $"Could not download {lang}.traineddata from {url}. " +
                    $"Error: {ex.Message}\nPlease download it manually to: {tessDataDir}");
            }
        }
    }
}

// ───────────────────────────────────────────────
//  PdfRenderer  —  PDF pages to temp PNG files
// ───────────────────────────────────────────────
internal static class PdfRenderer
{
    private static readonly object _consoleLock = new();

    internal static string[] RenderToTempPngs(string pdfPath, string tempDir,
                                              int width, int height, float scale)
    {
        Directory.CreateDirectory(tempDir);

        int pageCount;
        using (var stream = File.OpenRead(pdfPath))
        using (var probe  = new PdfToImageConverter())
        {
            probe.Load(stream);
            pageCount = probe.PageCount;
        }

        if (pageCount == 0)
            throw new InvalidOperationException("PDF contains zero pages.");

        Console.WriteLine($"Rendering {pageCount} PDF page(s)...");
        string[] results = new string[pageCount];

        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        Parallel.For(0, pageCount, options, i =>
        {
            using var stream    = File.OpenRead(pdfPath);
            using var converter = new PdfToImageConverter();
            converter.Load(stream);
            converter.ScaleFactor = scale;

            var size   = new Syncfusion.Drawing.SizeF(width, height);
            var images = converter.Convert(i, i, size, true, false, true);

            if (images[0] is not { Length: > 0 } imgStream)
                throw new InvalidOperationException($"PDF page {i + 1} produced an empty image.");

            string outPath = Path.Combine(tempDir, $"Page_{i + 1:D5}.png");
            imgStream.Position = 0;
            using (var fs = File.Create(outPath))
                imgStream.CopyTo(fs);
            imgStream.Dispose();

            results[i] = outPath;
            lock (_consoleLock) { Console.WriteLine($"  • Rendered page {i + 1}/{pageCount}"); }
        });

        return results;
    }
}

// ───────────────────────────────────────────────
//  OcrEngine  —  parallel tesseract CLI runner
// ───────────────────────────────────────────────
internal static class OcrEngine
{
    private static readonly object _consoleLock = new();

    internal static string[] RecognizeAll(string[] imageFiles, string tessDataDir, string language)
    {
        Console.WriteLine($"Running OCR on {imageFiles.Length} image(s) [{language}]...");
        string[] texts = new string[imageFiles.Length];

        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        Parallel.For(0, imageFiles.Length, options, i =>
        {
            string file = imageFiles[i];
            string text = RecognizeFile(file, tessDataDir, language);
            texts[i] = text;

            string name = Path.GetFileName(file);
            lock (_consoleLock)
            {
                Console.WriteLine(string.IsNullOrWhiteSpace(text)
                    ? $"  ⚠ No text in '{name}'"
                    : $"  ✓ {name} → {text.Length:N0} chars");
            }
        });

        return texts;
    }

    internal static string RecognizeFile(string imagePath, string tessDataDir, string language)
    {
        var psi = new ProcessStartInfo
        {
            FileName = TesseractSetup.GetTesseractExecutable(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["OMP_THREAD_LIMIT"] = "1";

        psi.ArgumentList.Add(imagePath);
        psi.ArgumentList.Add("stdout");
        psi.ArgumentList.Add("--tessdata-dir");
        psi.ArgumentList.Add(tessDataDir);
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add(language);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start tesseract process.");

        var errorTask = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        string error  = errorTask.GetAwaiter().GetResult();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"tesseract exited with code {process.ExitCode}: {error.Trim()}");

        return output.Trim();
    }
}

// ───────────────────────────────────────────────
//  OcrPipeline  —  orchestrates input → OCR → out
// ───────────────────────────────────────────────
internal static class OcrPipeline
{
    private static readonly Regex NumberPattern = new(@"\d+", RegexOptions.Compiled);

    internal static void Run(RunOptions opts)
    {
        Directory.CreateDirectory(opts.OutputDir);
        string tessDataDir = TesseractSetup.GetTessDataPath();

        switch (opts.Kind)
        {
            case InputKind.Image:
                RunSingleImage(opts, tessDataDir);
                break;
            case InputKind.Pdf:
                RunPdf(opts, tessDataDir);
                break;
            case InputKind.Directory:
                RunDirectory(opts, tessDataDir);
                break;
        }
    }

    private static void RunSingleImage(RunOptions opts, string tessDataDir)
    {
        string text    = OcrEngine.RecognizeFile(opts.InputPath, tessDataDir, opts.Language);
        string outPath = Path.Combine(opts.OutputDir, Path.GetFileNameWithoutExtension(opts.InputPath) + ".txt");

        File.WriteAllText(outPath, text, Encoding.UTF8);
        Console.WriteLine($"  ✓ Saved: {outPath} ({text.Length:N0} chars)");
    }

    private static void RunPdf(RunOptions opts, string tessDataDir)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}");
        try
        {
            string[] pageImages = PdfRenderer.RenderToTempPngs(
                opts.InputPath, tempDir, opts.Width, opts.Height, opts.Scale);

            string[] texts = OcrEngine.RecognizeAll(pageImages, tessDataDir, opts.Language);

            if (opts.OutputMode == OutputMode.SingleFile)
            {
                string outFile = Path.Combine(opts.OutputDir,
                    Path.GetFileNameWithoutExtension(opts.InputPath) + ".txt");
                WriteCombined(outFile, pageImages, texts);
            }
            else
            {
                WritePerItem(opts.OutputDir, pageImages, texts);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void RunDirectory(RunOptions opts, string tessDataDir)
    {
        string[] files = Directory.GetFiles(opts.InputPath)
            .Where(CliOptions.IsSupportedImage)
            .ToArray();

        if (files.Length == 0)
            throw new FileNotFoundException($"No supported image files found in '{opts.InputPath}'.");

        files = SortFiles(files, opts.SortBy);
        string[] texts = OcrEngine.RecognizeAll(files, tessDataDir, opts.Language);

        if (opts.OutputMode == OutputMode.SingleFile)
        {
            string outFile = Path.Combine(opts.OutputDir, "combined_text.txt");
            WriteCombined(outFile, files, texts);
        }
        else
        {
            WritePerItem(opts.OutputDir, files, texts);
        }
    }

    private static string[] SortFiles(string[] files, SortBy sortBy) => sortBy switch
    {
        SortBy.Name => files.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToArray(),
        SortBy.Number => files.OrderBy(f => ExtractNumber(Path.GetFileName(f)))
                              .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                              .ToArray(),
        SortBy.Date => files.OrderBy(File.GetLastWriteTimeUtc).ToArray(),
        SortBy.Size => files.OrderBy(f => new FileInfo(f).Length).ToArray(),
        _ => files
    };

    private static int ExtractNumber(string name)
    {
        var match = NumberPattern.Match(name);
        return match.Success && int.TryParse(match.Value, out int n) ? n : 0;
    }

    private static void WriteCombined(string outFile, string[] sourceFiles, string[] texts)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(texts[i]))
                continue;

            sb.AppendLine($"=== {Path.GetFileName(sourceFiles[i])} ===");
            sb.AppendLine(texts[i]);
            sb.AppendLine();
        }

        File.WriteAllText(outFile, sb.ToString().Trim(), Encoding.UTF8);
        Console.WriteLine($"Combined output saved to: {outFile}");
    }

    private static void WritePerItem(string outputDir, string[] sourceFiles, string[] texts)
    {
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string outFile = Path.Combine(outputDir,
                Path.GetFileNameWithoutExtension(sourceFiles[i]) + ".txt");
            File.WriteAllText(outFile, texts[i], Encoding.UTF8);
        }

        Console.WriteLine($"Saved {sourceFiles.Length} text file(s) to: {outputDir}");
    }
}
