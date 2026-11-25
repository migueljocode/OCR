namespace Ocr;

internal enum InputKind { Pdf, Image, Directory }
internal enum OutputMode { SingleFile, PerItem, PdfPerFileImagesCombined }
internal enum SortBy { Name, Number, Date, Size }
internal enum PdfProcessingMode { TextExtraction, ImageOcr }
internal enum RenderSize { Hd, FullHd, FourK }
internal enum DirectoryFileSelection { Both, PdfOnly, ImagesOnly }

internal sealed record RunOptions(
    string InputPath,
    string OutputDir,
    InputKind Kind,
    string Language,
    OutputMode OutputMode,
    PdfProcessingMode PdfMode,
    DirectoryFileSelection DirectoryFiles,
    SortBy SortBy,
    RenderSize RenderSize,
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
            ConsoleHelper.WriteError($"\n✗ Error: {ex.InnerException?.Message ?? ex.Message}");
            return 1;
        }
    }
}

// ───────────────────────────────────────────────
//  ConsoleHelper  —  reusable console I/O
// ───────────────────────────────────────────────
internal static class ConsoleHelper
{
    private static bool _wasPrompted;

    internal static bool WasPrompted => _wasPrompted;

    internal static void ResetPromptState() => _wasPrompted = false;

    private static bool UseColor =>
        !Console.IsOutputRedirected &&
        Environment.GetEnvironmentVariable("NO_COLOR") is null;

    internal static void WriteLine(string message, ConsoleColor color)
    {
        WriteColored(message, color, line: true);
    }

    internal static void Write(string message, ConsoleColor color)
    {
        WriteColored(message, color, line: false);
    }

    internal static void WriteError(string message)
    {
        WriteColored(message, ConsoleColor.Red, line: true, error: true);
    }

    private static void WriteColored(string message, ConsoleColor color, bool line, bool error = false)
    {
        TextWriter writer = error ? Console.Error : Console.Out;
        if (!UseColor)
        {
            if (line)
                writer.WriteLine(message);
            else
                writer.Write(message);
            return;
        }

        ConsoleColor previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            if (line)
                writer.WriteLine(message);
            else
                writer.Write(message);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }

    internal static void ClearForProcessingIfUnprompted()
    {
        if (!_wasPrompted &&
            !Console.IsOutputRedirected &&
            !Console.IsInputRedirected &&
            Environment.GetEnvironmentVariable("TERM") != "dumb")
        {
            Console.Clear();
        }
    }

    internal static string Ask(string prompt, string? defaultValue = null)
    {
        _wasPrompted = true;
        string suffix = defaultValue is null ? ": " : $" [{defaultValue}]: ";
        Write($"\n  {prompt}{suffix}", ConsoleColor.Cyan);
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
        _wasPrompted = true;
        WriteLine($"\n  {title}", ConsoleColor.Cyan);
        for (int i = 0; i < choices.Length; i++)
            WriteLine($"    {i + 1}. {choices[i].Label}", ConsoleColor.Yellow);

        Write($"  Choice [1-{choices.Length}]: ", ConsoleColor.Green);
        string? raw = Console.ReadLine()?.Trim();

        if (!int.TryParse(raw, out int index) || index < 1 || index > choices.Length)
            throw new ArgumentException($"Invalid choice '{raw}'.");

        return choices[index - 1].Value;
    }

    internal static void Done(string message, bool waitForKey)
    {
        WriteLine($"\n✓ {message}", ConsoleColor.Green);
        if (waitForKey)
        {
            Write("Press any key to exit...", ConsoleColor.DarkGray);
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

    internal static bool IsPdf(string path) =>
        Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    internal static bool IsSupportedFile(string path) =>
        IsSupportedImage(path) || IsPdf(path);

    internal static bool TryParse(string[] args, out RunOptions options)
    {
        ConsoleHelper.ResetPromptState();
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
        ConsoleHelper.ClearForProcessingIfUnprompted();
        return true;
    }

    private static RunOptions PromptInteractive()
    {
        string inputPath = ConsoleHelper.Ask("Input path (PDF, image file, or directory)");
        InputKind kind   = DetectInputKind(inputPath);

        string defaultOut = kind == InputKind.Directory
            ? Path.Combine(inputPath, "ocr_output")
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputPath))!, "ocr_output");

        string outputDir = ConsoleHelper.Ask("Output directory", defaultOut);

        string[] inputFiles = kind == InputKind.Directory
            ? Directory.GetFiles(inputPath).Where(IsSupportedFile).ToArray()
            : [inputPath];
        bool hasPdf = inputFiles.Any(IsPdf);
        bool hasImage = inputFiles.Any(IsSupportedImage);

        DirectoryFileSelection directoryFiles = DirectoryFileSelection.Both;
        if (kind == InputKind.Directory && hasPdf && hasImage)
        {
            directoryFiles = ConsoleHelper.AskChoice(
                "This folder contains PDFs and images. Select which file types to process:",
            [
                ("Process PDFs and images", DirectoryFileSelection.Both),
                ("Process PDFs only (skip images)", DirectoryFileSelection.PdfOnly),
                ("Process images only (skip PDFs)", DirectoryFileSelection.ImagesOnly)
            ]);
            inputFiles = SelectDirectoryFiles(inputFiles, directoryFiles).ToArray();
        }

        hasPdf = inputFiles.Any(IsPdf);
        hasImage = inputFiles.Any(IsSupportedImage);

        PdfProcessingMode pdfMode = PdfProcessingMode.TextExtraction;
        if (hasPdf)
        {
            pdfMode = ConsoleHelper.AskChoice(
                "PDF processing method (applies to every selected PDF):",
            [
                ("Extract embedded text (for text-based PDFs; does not read scanned pages)", PdfProcessingMode.TextExtraction),
                ("Render pages and OCR them (for scans or pages containing images)", PdfProcessingMode.ImageOcr)
            ]);
        }

        string language = "fas+eng";
        if (hasImage || (hasPdf && pdfMode == PdfProcessingMode.ImageOcr))
        {
            string languageScope = hasImage && hasPdf
                ? "for selected images and OCR-processed PDF pages"
                : hasImage ? "for selected images" : "for OCR-processed PDF pages";
            language = ConsoleHelper.AskChoice($"OCR language {languageScope}:",
            [
                ("Persian (fas)",     "fas"),
                ("English (eng)",     "eng"),
                ("Both    (fas+eng)", "fas+eng")
            ]);
        }

        OutputMode mode = kind switch
        {
            InputKind.Image => OutputMode.PerItem,
            InputKind.Pdf => ConsoleHelper.AskChoice("PDF output mode:",
            [
                ("One text file for whole PDF", OutputMode.SingleFile),
                ("One text file per page",      OutputMode.PerItem)
            ]),
            InputKind.Directory when hasPdf && hasImage => ConsoleHelper.AskChoice(
                "Output layout for the selected PDFs and images:",
            [
                ("One combined text file for all selected PDFs and images", OutputMode.SingleFile),
                ("One text file per input file (one per PDF and one per image)", OutputMode.PerItem),
                ("One text file per PDF, plus one combined text file for all images", OutputMode.PdfPerFileImagesCombined)
            ]),
            InputKind.Directory when hasPdf => ConsoleHelper.AskChoice("Output layout for the selected PDFs:",
            [
                ("One combined text file for all selected PDFs", OutputMode.SingleFile),
                ("One text file per PDF (each file contains all its pages)", OutputMode.PerItem)
            ]),
            InputKind.Directory => ConsoleHelper.AskChoice("Output layout for the selected images:",
            [
                ("One combined text file for all selected images", OutputMode.SingleFile),
                ("One text file per image", OutputMode.PerItem)
            ]),
            _ => throw new InvalidOperationException()
        };

        SortBy sortBy = SortBy.Number;
        if (kind == InputKind.Directory && mode is OutputMode.SingleFile or OutputMode.PdfPerFileImagesCombined)
        {
            string sortPrompt = mode == OutputMode.PdfPerFileImagesCombined
                ? "Sort selected PDFs and images (PDF order and image order in the combined image file):"
                : "Sort selected directory files by:";
            sortBy = ConsoleHelper.AskChoice(sortPrompt,
            [
                ("Number in filename", SortBy.Number),
                ("File name (A-Z)",    SortBy.Name),
                ("Date modified",      SortBy.Date),
                ("File size",          SortBy.Size)
            ]);
        }

        RenderSize renderSize = RenderSize.FullHd;
        if (hasPdf && pdfMode == PdfProcessingMode.ImageOcr)
        {
            renderSize = ConsoleHelper.AskChoice("PDF render size (page aspect ratio is preserved):",
            [
                ("4K (up to 3840 × 2160)", RenderSize.FourK),
                ("Full HD (up to 1920 × 1080)", RenderSize.FullHd),
                ("HD (up to 1280 × 720)", RenderSize.Hd)
            ]);
        }

        return new RunOptions(inputPath, outputDir, kind, language, mode, pdfMode,
            directoryFiles, sortBy, renderSize, true);
    }

    private static RunOptions ParseArgs(string[] args)
    {
        string? input = null, output = null;
        string lang   = "fas+eng";
        var mode      = OutputMode.SingleFile;
        var pdfMode   = PdfProcessingMode.ImageOcr;
        var directoryFiles = DirectoryFileSelection.Both;
        var sort      = SortBy.Number;
        var renderSize = RenderSize.FullHd;
        bool languageSpecified = false;
        bool modeSpecified = false;
        bool pdfModeSpecified = false;
        bool directoryFilesSpecified = false;
        bool sortSpecified = false;
        bool renderSizeSpecified = false;

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
                case "-l" or "--lang" or "--language":
                    languageSpecified = true;
                    lang = NextValue().ToLowerInvariant() switch
                    {
                        "fas" or "eng" or "fas+eng" or "eng+fas" => args[i].ToLowerInvariant(),
                        _ => throw new ArgumentException("Language must be 'fas', 'eng', or 'fas+eng'.")
                    };
                    break;
                case "-m" or "--mode" or "--output-mode":
                    modeSpecified = true;
                    mode = NextValue().ToLowerInvariant() switch
                    {
                        "single" => OutputMode.SingleFile,
                        "per-page" or "per-file" or "each" => OutputMode.PerItem,
                        "split" or "pdf-each-images-combined" => OutputMode.PdfPerFileImagesCombined,
                        _ => throw new ArgumentException(
                            "Mode must be 'single', 'each', or 'split'.")
                    };
                    break;
                case "--pdf-mode":
                    pdfModeSpecified = true;
                    pdfMode = NextValue().ToLowerInvariant() switch
                    {
                        "extract" => PdfProcessingMode.TextExtraction,
                        "ocr" => PdfProcessingMode.ImageOcr,
                        _ => throw new ArgumentException("--pdf-mode must be 'extract' or 'ocr'.")
                    };
                    break;
                case "--files" or "--directory-files":
                    directoryFilesSpecified = true;
                    directoryFiles = NextValue().ToLowerInvariant() switch
                    {
                        "both" => DirectoryFileSelection.Both,
                        "pdf" => DirectoryFileSelection.PdfOnly,
                        "images" => DirectoryFileSelection.ImagesOnly,
                        _ => throw new ArgumentException("--files must be 'both', 'pdf', or 'images'.")
                    };
                    break;
                case "--render-size" or "--pdf-render-size":
                    renderSizeSpecified = true;
                    renderSize = NextValue().ToLowerInvariant() switch
                    {
                        "hd" => RenderSize.Hd,
                        "fullhd" => RenderSize.FullHd,
                        "4k" => RenderSize.FourK,
                        _ => throw new ArgumentException("--render-size must be 'hd', 'fullhd', or '4k'.")
                    };
                    break;
                case "-s" or "--sort" or "--sort-by":
                    sortSpecified = true;
                    sort = NextValue().ToLowerInvariant() switch
                    {
                        "name"   => SortBy.Name,
                        "number" => SortBy.Number,
                        "date"   => SortBy.Date,
                        "size"   => SortBy.Size,
                        _ => throw new ArgumentException("Sort must be 'number', 'name', 'date', or 'size'.")
                    };
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

        string[] inputFiles = kind == InputKind.Directory
            ? Directory.GetFiles(input).Where(IsSupportedFile).ToArray()
            : [input];
        bool hasPdf = inputFiles.Any(IsPdf);
        bool hasImage = inputFiles.Any(IsSupportedImage);

        if (kind == InputKind.Directory && hasPdf && hasImage && !directoryFilesSpecified)
        {
            directoryFiles = ConsoleHelper.AskChoice(
                "This folder contains PDFs and images. Select which file types to process:",
            [
                ("Process PDFs and images", DirectoryFileSelection.Both),
                ("Process PDFs only (skip images)", DirectoryFileSelection.PdfOnly),
                ("Process images only (skip PDFs)", DirectoryFileSelection.ImagesOnly)
            ]);
            inputFiles = SelectDirectoryFiles(inputFiles, directoryFiles).ToArray();
        }

        hasPdf = inputFiles.Any(IsPdf);
        hasImage = inputFiles.Any(IsSupportedImage);

        if (hasPdf && !pdfModeSpecified)
        {
            pdfMode = ConsoleHelper.AskChoice(
                "PDF processing method (applies to every selected PDF):",
            [
                ("Extract embedded text (for text-based PDFs; does not read scanned pages)", PdfProcessingMode.TextExtraction),
                ("Render pages and OCR them (for scans or pages containing images)", PdfProcessingMode.ImageOcr)
            ]);
        }

        bool needsOcr = hasImage || (hasPdf && pdfMode == PdfProcessingMode.ImageOcr);
        if (needsOcr && !languageSpecified)
        {
            lang = ConsoleHelper.AskChoice("Select OCR language:",
            [
                ("Persian (fas)",     "fas"),
                ("English (eng)",     "eng"),
                ("Both    (fas+eng)", "fas+eng")
            ]);
        }

        if (kind != InputKind.Image && !modeSpecified)
        {
            mode = kind switch
            {
                InputKind.Pdf => ConsoleHelper.AskChoice("PDF output mode:",
                [
                    ("One text file for whole PDF", OutputMode.SingleFile),
                    ("One text file per page", OutputMode.PerItem)
                ]),
                InputKind.Directory when hasPdf && hasImage => ConsoleHelper.AskChoice(
                    "Output layout for the selected PDFs and images:",
                [
                    ("One combined text file for all selected PDFs and images", OutputMode.SingleFile),
                    ("One text file per input file (one per PDF and one per image)", OutputMode.PerItem),
                    ("One text file per PDF, plus one combined text file for all images", OutputMode.PdfPerFileImagesCombined)
                ]),
                InputKind.Directory when hasPdf => ConsoleHelper.AskChoice("Output layout for the selected PDFs:",
                [
                    ("One combined text file for all selected PDFs", OutputMode.SingleFile),
                    ("One text file per PDF (each file contains all its pages)", OutputMode.PerItem)
                ]),
                InputKind.Directory => ConsoleHelper.AskChoice("Output layout for the selected images:",
                [
                    ("One combined text file for all selected images", OutputMode.SingleFile),
                    ("One text file per image", OutputMode.PerItem)
                ]),
                _ => throw new InvalidOperationException()
            };
        }

        if (kind == InputKind.Directory &&
            mode is OutputMode.SingleFile or OutputMode.PdfPerFileImagesCombined &&
            !sortSpecified)
        {
            sort = ConsoleHelper.AskChoice("Sort selected directory files by:",
            [
                ("Number in filename", SortBy.Number),
                ("File name (A-Z)", SortBy.Name),
                ("Date modified", SortBy.Date),
                ("File size", SortBy.Size)
            ]);
        }

        if (hasPdf && pdfMode == PdfProcessingMode.ImageOcr && !renderSizeSpecified)
        {
            renderSize = ConsoleHelper.AskChoice("PDF render size (page aspect ratio is preserved):",
            [
                ("4K (up to 3840 × 2160)", RenderSize.FourK),
                ("Full HD (up to 1920 × 1080)", RenderSize.FullHd),
                ("HD (up to 1280 × 720)", RenderSize.Hd)
            ]);
        }

        output ??= kind == InputKind.Directory
            ? Path.Combine(input, "ocr_output")
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!, "ocr_output");

        return new RunOptions(input, output, kind, lang, mode, pdfMode,
            directoryFiles, sort, renderSize, false);
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
            Ocr — PDF Text Extraction and Image OCR Tool

            Usage:
              Ocr                              Run in interactive mode (prompts for options)
              Ocr -i <path> [options]          Run with command-line arguments
              Ocr -h | --help                  Show this help message

            Options:
              -i, --input <path>               Input PDF file, image file, or directory of PDFs/images
              -o, --output <dir>               Output directory (default: <input_dir>/ocr_output)
              -l, --lang, --language <lang>    OCR language: fas | eng | fas+eng (prompted if OCR is needed and omitted)
              -m, --mode, --output-mode <mode> Output mode for PDF or directory:
                                                 single  One combined .txt file for all selected input
                                                 each    One .txt per PDF page, or per directory input file
                                                 split   One .txt per PDF plus one combined .txt for directory images
              --pdf-mode <mode>                PDF processing: extract (embedded text) | ocr (render pages and OCR)
                                                 Prompted when a PDF is selected and this option is omitted
              --files, --directory-files <selection>
                                                 Directory inputs: both | pdf | images
                                                 Prompted for folders containing both types
              --render-size, --pdf-render-size <size>
                                                 PDF OCR resolution: hd | fullhd | 4k
                                                 Prompted for OCR-processed PDFs if omitted
              -s, --sort, --sort-by <by>        Sort order for combined directory output:
                                                 number  By number in filename (default)
                                                 name    Alphabetically by filename
                                                 date    By last modified time
                                                 size    By file size
                                                 Prompted for combined output if omitted
              -h, --help                       Show this help

            Supported Inputs:
              • PDF (.pdf)
              • Images (.png, .jpg, .jpeg, .bmp, .tif, .tiff, .webp, .gif)
              • Directory containing supported PDFs and/or images

            Examples:
              Ocr -i document.pdf -o ./out -l fas+eng -m single
              Ocr -i ./scans -o ./out -l fas+eng -m single -s number
              Ocr -i ./mixed -o ./out --files pdf --pdf-mode extract
              Ocr -i photo.jpg -o ./out -l eng

            Fully specified mixed-directory command (does not prompt):
              Ocr -i ./mixed -o ./out --files both --pdf-mode ocr -l fas+eng -m single -s number --render-size fullhd
            """);
    }

    internal static IEnumerable<string> SelectDirectoryFiles(
        IEnumerable<string> files,
        DirectoryFileSelection selection) => selection switch
    {
        DirectoryFileSelection.PdfOnly => files.Where(IsPdf),
        DirectoryFileSelection.ImagesOnly => files.Where(IsSupportedImage),
        _ => files
    };
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
        ConsoleHelper.WriteLine($"Tessdata directory: {tessDataDir}", ConsoleColor.Cyan);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        foreach (string lang in languages)
        {
            string filePath = Path.Combine(tessDataDir, $"{lang}.traineddata");
            if (File.Exists(filePath))
            {
                ConsoleHelper.WriteLine($"  ✓ {lang}.traineddata already present.", ConsoleColor.Green);
                continue;
            }

            string url = $"{BaseUrl}{lang}.traineddata";
            ConsoleHelper.Write($"  ↓ Downloading {lang}.traineddata … ", ConsoleColor.Yellow);

            try
            {
                byte[] data = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(filePath, data);
                ConsoleHelper.WriteLine($"OK ({data.Length:N0} bytes)", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine("FAILED", ConsoleColor.Red);
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
internal static class PdfTextExtractor
{
    internal static string[] ExtractPages(string pdfPath)
    {
        string[] pages;
        if (TryExtractWithPoppler(pdfPath, out string extracted))
        {
            pages = extracted.Split('\f')
                .Select(page => page.Trim())
                .ToArray();
            if (pages.Length > 0 && string.IsNullOrWhiteSpace(pages[^1]))
                pages = pages[..^1];
        }
        else
        {
            ConsoleHelper.WriteLine(
                "  ⚠ 'pdftotext' was not found; falling back to built-in extraction, which may have limited right-to-left text ordering.",
                ConsoleColor.Yellow);
            using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            pages = document.GetPages()
                .Select(page => UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page).Trim())
                .ToArray();
        }

        if (pages.All(string.IsNullOrWhiteSpace))
        {
            ConsoleHelper.WriteLine(
                $"  ⚠ No embedded text found in '{Path.GetFileName(pdfPath)}'. If it is scanned, choose PDF image OCR.",
                ConsoleColor.Yellow);
        }

        return pages;
    }

    private static bool TryExtractWithPoppler(string pdfPath, out string text)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pdftotext",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-enc");
        psi.ArgumentList.Add("UTF-8");
        psi.ArgumentList.Add("-layout");
        psi.ArgumentList.Add(pdfPath);
        psi.ArgumentList.Add("-");

        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start pdftotext process.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            text = string.Empty;
            return false;
        }

        using (process)
        {
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            text = process.StandardOutput.ReadToEnd();
            string error = errorTask.GetAwaiter().GetResult();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"pdftotext exited with code {process.ExitCode}: {error.Trim()}");
        }

        return true;
    }

    internal static (double Width, double Height)[] GetPageSizes(string pdfPath)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        return document.GetPages()
            .Select(page => (Convert.ToDouble(page.Width), Convert.ToDouble(page.Height)))
            .ToArray();
    }
}

internal static class PdfRenderer
{
    private static readonly object _consoleLock = new();

    static PdfRenderer()
    {
        if (OperatingSystem.IsLinux())
        {
            NativeLibrary.SetDllImportResolver(
                typeof(PdfToImageConverter).Assembly,
                static (libraryName, _, _) =>
                    libraryName == "libdl" ? NativeLibrary.Load("libdl.so.2") : IntPtr.Zero);
        }
    }

    internal static string[] RenderToTempPngs(string pdfPath, string tempDir, RenderSize renderSize)
    {
        Directory.CreateDirectory(tempDir);

        var pageSizes = PdfTextExtractor.GetPageSizes(pdfPath);
        int pageCount = pageSizes.Length;

        if (pageCount == 0)
            throw new InvalidOperationException("PDF contains zero pages.");

        (int maxWidth, int maxHeight) = renderSize switch
        {
            RenderSize.Hd => (1280, 720),
            RenderSize.FullHd => (1920, 1080),
            RenderSize.FourK => (3840, 2160),
            _ => throw new ArgumentOutOfRangeException(nameof(renderSize))
        };

        ConsoleHelper.WriteLine($"Rendering {pageCount} PDF page(s)...", ConsoleColor.Cyan);
        string[] results = new string[pageCount];

        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        Parallel.For(0, pageCount, options, i =>
        {
            using var stream    = File.OpenRead(pdfPath);
            using var converter = new PdfToImageConverter();
            converter.Load(stream);

            (double pageWidth, double pageHeight) = pageSizes[i];
            double fitScale = Math.Min(maxWidth / pageWidth, maxHeight / pageHeight);
            var size = new Syncfusion.Drawing.SizeF(
                (float)Math.Max(1, Math.Round(pageWidth * fitScale)),
                (float)Math.Max(1, Math.Round(pageHeight * fitScale)));
            var images = converter.Convert(i, i, size, true, false, true);

            if (images[0] is not { Length: > 0 } imgStream)
                throw new InvalidOperationException($"PDF page {i + 1} produced an empty image.");

            string outPath = Path.Combine(tempDir, $"Page_{i + 1:D5}.png");
            imgStream.Position = 0;
            using (var fs = File.Create(outPath))
                imgStream.CopyTo(fs);
            imgStream.Dispose();

            results[i] = outPath;
            lock (_consoleLock)
                ConsoleHelper.WriteLine($"  • Rendered page {i + 1}/{pageCount}", ConsoleColor.DarkCyan);
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
        ConsoleHelper.WriteLine($"Running OCR on {imageFiles.Length} image(s) [{language}]...", ConsoleColor.Cyan);
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
                if (string.IsNullOrWhiteSpace(text))
                    ConsoleHelper.WriteLine($"  ⚠ No text in '{name}'", ConsoleColor.Yellow);
                else
                    ConsoleHelper.WriteLine($"  ✓ {name} → {text.Length:N0} chars", ConsoleColor.Green);
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
        bool needsOcr = opts.Kind switch
        {
            InputKind.Image => true,
            InputKind.Pdf => opts.PdfMode == PdfProcessingMode.ImageOcr,
            InputKind.Directory => CliOptions.SelectDirectoryFiles(
                    Directory.GetFiles(opts.InputPath).Where(CliOptions.IsSupportedFile),
                    opts.DirectoryFiles)
                .Any(file => CliOptions.IsSupportedImage(file) ||
                    (CliOptions.IsPdf(file) && opts.PdfMode == PdfProcessingMode.ImageOcr)),
            _ => false
        };

        if (needsOcr)
            TesseractSetup.EnsureTrainedData(opts.Language.Split('+'));

        string tessDataDir = needsOcr ? TesseractSetup.GetTessDataPath() : string.Empty;

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
        ConsoleHelper.WriteLine($"  ✓ Saved: {outPath} ({text.Length:N0} chars)", ConsoleColor.Green);
    }

    private static void RunPdf(RunOptions opts, string tessDataDir)
    {
        if (opts.PdfMode == PdfProcessingMode.TextExtraction)
        {
            string[] pageTexts = PdfTextExtractor.ExtractPages(opts.InputPath);
            if (opts.OutputMode == OutputMode.SingleFile)
            {
                string outFile = Path.Combine(opts.OutputDir,
                    Path.GetFileNameWithoutExtension(opts.InputPath) + ".txt");
                WritePdfCombined(outFile, pageTexts);
            }
            else
            {
                WritePdfPages(opts.OutputDir, Path.GetFileNameWithoutExtension(opts.InputPath), pageTexts);
            }
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}");
        try
        {
            string[] pageImages = PdfRenderer.RenderToTempPngs(
                opts.InputPath, tempDir, opts.RenderSize);

            string[] texts = OcrEngine.RecognizeAll(pageImages, tessDataDir, opts.Language);

            if (opts.OutputMode == OutputMode.SingleFile)
            {
                string outFile = Path.Combine(opts.OutputDir,
                    Path.GetFileNameWithoutExtension(opts.InputPath) + ".txt");
                WritePdfCombined(outFile, texts);
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
        string[] files = CliOptions.SelectDirectoryFiles(
                Directory.GetFiles(opts.InputPath).Where(CliOptions.IsSupportedFile),
                opts.DirectoryFiles)
            .ToArray();

        if (files.Length == 0)
            throw new FileNotFoundException($"No supported image or PDF files found in '{opts.InputPath}'.");

        files = SortFiles(files, opts.SortBy);

        string[] texts;
        if (files.All(CliOptions.IsSupportedImage))
        {
            texts = OcrEngine.RecognizeAll(files, tessDataDir, opts.Language);
        }
        else
        {
            ConsoleHelper.WriteLine($"Found {files.Length} file(s) in directory...", ConsoleColor.Cyan);
            texts = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (CliOptions.IsPdf(file))
                {
                    ConsoleHelper.WriteLine($"Processing PDF: {Path.GetFileName(file)}", ConsoleColor.Cyan);
                    texts[i] = ProcessPdfAsCombinedText(file, opts, tessDataDir);
                }
                else
                {
                    texts[i] = OcrEngine.RecognizeFile(file, tessDataDir, opts.Language);
                    ConsoleHelper.WriteLine($"  ✓ {Path.GetFileName(file)} → {texts[i].Length:N0} chars", ConsoleColor.Green);
                }
            }
        }

        if (opts.OutputMode == OutputMode.SingleFile)
        {
            string outFile = Path.Combine(opts.OutputDir, "combined_text.txt");
            WriteCombined(outFile, files, texts);
        }
        else if (opts.OutputMode == OutputMode.PdfPerFileImagesCombined)
        {
            WritePdfPerFileAndCombinedImages(opts.OutputDir, files, texts);
        }
        else
        {
            WritePerItem(opts.OutputDir, files, texts);
        }
    }

    private static string ProcessPdfAsCombinedText(string pdfPath, RunOptions opts, string tessDataDir)
    {
        if (opts.PdfMode == PdfProcessingMode.TextExtraction)
            return FormatPdfPages(PdfTextExtractor.ExtractPages(pdfPath));

        string tempDir = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}");
        try
        {
            string[] pageImages = PdfRenderer.RenderToTempPngs(
                pdfPath, tempDir, opts.RenderSize);
            string[] pageTexts = OcrEngine.RecognizeAll(pageImages, tessDataDir, opts.Language);
            return FormatPdfPages(pageTexts);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
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

    private static string FormatCombined(string[] sourceFiles, string[] texts)
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

        return sb.ToString().Trim();
    }

    private static string FormatPdfPages(string[] pageTexts)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < pageTexts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(pageTexts[i]))
                continue;

            sb.AppendLine($"=== Page {i + 1} ===");
            sb.AppendLine(pageTexts[i]);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private static void WritePdfCombined(string outFile, string[] pageTexts)
    {
        File.WriteAllText(outFile, FormatPdfPages(pageTexts), Encoding.UTF8);
        ConsoleHelper.WriteLine($"PDF text saved to: {outFile}", ConsoleColor.Green);
    }

    private static void WritePdfPages(string outputDir, string pdfName, string[] pageTexts)
    {
        for (int i = 0; i < pageTexts.Length; i++)
        {
            string outFile = Path.Combine(outputDir, $"{pdfName}_Page_{i + 1:D5}.txt");
            File.WriteAllText(outFile, pageTexts[i], Encoding.UTF8);
        }

        ConsoleHelper.WriteLine($"Saved {pageTexts.Length} PDF page text file(s) to: {outputDir}", ConsoleColor.Green);
    }

    private static void WritePdfPerFileAndCombinedImages(
        string outputDir,
        string[] sourceFiles,
        string[] texts)
    {
        var imageFiles = new List<string>();
        var imageTexts = new List<string>();

        for (int i = 0; i < sourceFiles.Length; i++)
        {
            if (CliOptions.IsPdf(sourceFiles[i]))
            {
                string outFile = Path.Combine(
                    outputDir,
                    Path.GetFileName(sourceFiles[i]) + ".txt");
                File.WriteAllText(outFile, texts[i], Encoding.UTF8);
                ConsoleHelper.WriteLine($"PDF text saved to: {outFile}", ConsoleColor.Green);
            }
            else
            {
                imageFiles.Add(sourceFiles[i]);
                imageTexts.Add(texts[i]);
            }
        }

        if (imageFiles.Count > 0)
        {
            string outFile = Path.Combine(outputDir, "combined_images.txt");
            WriteCombined(outFile, imageFiles.ToArray(), imageTexts.ToArray());
        }
    }

    private static void WriteCombined(string outFile, string[] sourceFiles, string[] texts)
    {
        File.WriteAllText(outFile, FormatCombined(sourceFiles, texts), Encoding.UTF8);
        ConsoleHelper.WriteLine($"Combined output saved to: {outFile}", ConsoleColor.Green);
    }

    private static void WritePerItem(string outputDir, string[] sourceFiles, string[] texts)
    {
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string outFile = Path.Combine(outputDir,
                Path.GetFileNameWithoutExtension(sourceFiles[i]) + ".txt");
            File.WriteAllText(outFile, texts[i], Encoding.UTF8);
        }

        ConsoleHelper.WriteLine($"Saved {sourceFiles.Length} text file(s) to: {outputDir}", ConsoleColor.Green);
    }
}
