namespace Ocr;

/// <summary>Identifies the kind of input selected for OCR processing.</summary>
internal enum InputKind
{
    /// <summary>A single PDF document.</summary>
    Pdf,
    /// <summary>A single supported image.</summary>
    Image,
    /// <summary>A folder containing supported PDFs, images, or both.</summary>
    Directory
}

/// <summary>Controls how recognized text is grouped into output files.</summary>
internal enum OutputMode
{
    /// <summary>Writes all selected input text into a combined file.</summary>
    SingleFile,
    /// <summary>Writes a separate text file for each page or source file.</summary>
    PerItem,
    /// <summary>Writes one file per PDF and one combined file for directory images.</summary>
    PdfPerFileImagesCombined
}

/// <summary>Specifies the ordering applied to files in a directory.</summary>
internal enum SortBy
{
    /// <summary>Sorts alphabetically by file name.</summary>
    Name,
    /// <summary>Sorts by the first number found in each file name.</summary>
    Number,
    /// <summary>Sorts by last modification time.</summary>
    Date,
    /// <summary>Sorts by file size.</summary>
    Size
}

/// <summary>Selects direct PDF text extraction or image-based OCR.</summary>
internal enum PdfProcessingMode
{
    /// <summary>Extracts embedded text without recognizing rendered page images.</summary>
    TextExtraction,
    /// <summary>Renders each page to an image and recognizes its text with Tesseract.</summary>
    ImageOcr
}

/// <summary>Specifies the maximum bounding resolution used to render PDF pages.</summary>
internal enum RenderSize
{
    /// <summary>Limits a rendered page to 1280 by 720 pixels.</summary>
    Hd,
    /// <summary>Limits a rendered page to 1920 by 1080 pixels.</summary>
    FullHd,
    /// <summary>Limits a rendered page to 3840 by 2160 pixels.</summary>
    FourK
}

/// <summary>Controls which supported file types are selected from a directory.</summary>
internal enum DirectoryFileSelection
{
    /// <summary>Processes both PDFs and images.</summary>
    Both,
    /// <summary>Processes PDFs and skips images.</summary>
    PdfOnly,
    /// <summary>Processes images and skips PDFs.</summary>
    ImagesOnly
}

/// <summary>Contains validated input, processing, and output choices for one run.</summary>
/// <param name="InputPath">Path to the source file or directory.</param>
/// <param name="OutputDir">Directory where generated text files are written.</param>
/// <param name="Kind">Whether the input is a PDF, image, or directory.</param>
/// <param name="Language">Tesseract language code or codes joined with '+'.</param>
/// <param name="OutputMode">How source text is grouped into output files.</param>
/// <param name="PdfMode">How selected PDFs are processed.</param>
/// <param name="DirectoryFiles">Which supported file types are processed from a directory.</param>
/// <param name="SortBy">Ordering used for directory inputs.</param>
/// <param name="RenderSize">Maximum rendering resolution for PDF image OCR.</param>
/// <param name="Interactive">Whether the run started from interactive prompts.</param>
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

/// <summary>Runs the application while converting unhandled exceptions into CLI errors.</summary>
internal static class AppRunner
{
    /// <summary>Executes an application action and returns its process exit code.</summary>
    /// <param name="action">The action to run.</param>
    /// <returns>Zero when the action completes; one when it throws.</returns>
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

/// <summary>Provides formatted console prompts, choices, status messages, and errors.</summary>
internal static class ConsoleHelper
{
    /// <summary>Tracks whether the current run has displayed an interactive question.</summary>
    private static bool _wasPrompted;

    /// <summary>Gets whether this run has asked the user any questions.</summary>
    internal static bool WasPrompted => _wasPrompted;

    /// <summary>Resets prompt tracking before parsing a new run.</summary>
    internal static void ResetPromptState() => _wasPrompted = false;

    /// <summary>Indicates whether ANSI-like console foreground coloring should be used.</summary>
    private static bool UseColor =>
        !Console.IsOutputRedirected &&
        Environment.GetEnvironmentVariable("NO_COLOR") is null;

    /// <summary>Writes a colored message followed by a line break.</summary>
    /// <param name="message">Text to display.</param>
    /// <param name="color">Foreground color used when terminal colors are enabled.</param>
    internal static void WriteLine(string message, ConsoleColor color)
    {
        WriteColored(message, color, line: true);
    }

    /// <summary>Writes a colored message without adding a line break.</summary>
    /// <param name="message">Text to display.</param>
    /// <param name="color">Foreground color used when terminal colors are enabled.</param>
    internal static void Write(string message, ConsoleColor color)
    {
        WriteColored(message, color, line: false);
    }

    /// <summary>Writes an error message to standard error in red when colors are enabled.</summary>
    /// <param name="message">Error text to display.</param>
    internal static void WriteError(string message)
    {
        WriteColored(message, ConsoleColor.Red, line: true, error: true);
    }

    /// <summary>Writes to standard output or error with optional terminal coloring.</summary>
    /// <param name="message">Text to display.</param>
    /// <param name="color">Foreground color used when terminal colors are enabled.</param>
    /// <param name="line">Whether to append a line break.</param>
    /// <param name="error">Whether to write to standard error.</param>
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

    /// <summary>Clears an attached terminal before a fully specified, non-interactive run.</summary>
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

    /// <summary>Asks for a text value, optionally using a default when the answer is blank.</summary>
    /// <param name="prompt">Question shown to the user.</param>
    /// <param name="defaultValue">Value returned when the user enters no text; otherwise required.</param>
    /// <returns>The trimmed answer or the supplied default.</returns>
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

    /// <summary>Asks for a positive integer value.</summary>
    /// <param name="prompt">Question shown to the user.</param>
    /// <param name="defaultValue">Value used when the user accepts the displayed default.</param>
    /// <returns>The selected positive integer.</returns>
    internal static int AskInt(string prompt, int defaultValue)
    {
        string raw = Ask(prompt, defaultValue.ToString());
        if (!int.TryParse(raw, out int result) || result <= 0)
            throw new ArgumentException($"'{prompt}' must be a positive integer.");
        return result;
    }

    /// <summary>Asks for a positive floating-point value.</summary>
    /// <param name="prompt">Question shown to the user.</param>
    /// <param name="defaultValue">Value used when the user accepts the displayed default.</param>
    /// <returns>The selected positive number.</returns>
    internal static float AskFloat(string prompt, float defaultValue)
    {
        string raw = Ask(prompt, defaultValue.ToString());
        if (!float.TryParse(raw, out float result) || result <= 0)
            throw new ArgumentException($"'{prompt}' must be a positive number.");
        return result;
    }

    /// <summary>Displays numbered choices and returns the selected value.</summary>
    /// <typeparam name="T">Type associated with each choice.</typeparam>
    /// <param name="title">Question shown above the choices.</param>
    /// <param name="choices">Labels and values offered to the user.</param>
    /// <returns>The value associated with the selected choice.</returns>
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

    /// <summary>Displays successful completion and optionally waits for a key press.</summary>
    /// <param name="message">Completion message to display.</param>
    /// <param name="waitForKey">Whether to wait for input before exiting.</param>
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

/// <summary>Loads supported images and prepares still images for OCR.</summary>
internal static class ImageProcessor
{
    /// <summary>Decodes an image file into a bitmap.</summary>
    /// <param name="imagePath">Path of the image to decode.</param>
    /// <returns>The decoded image bitmap.</returns>
    /// <exception cref="InvalidOperationException">The image could not be decoded.</exception>
    internal static SKBitmap Load(string imagePath) =>
        SKBitmap.Decode(imagePath)
        ?? throw new InvalidOperationException($"Could not decode image '{imagePath}'.");

    /// <summary>Checks whether Tesseract should handle a potentially multi-frame image directly.</summary>
    /// <param name="imagePath">Path of the image to inspect.</param>
    /// <returns><see langword="true"/> for TIFF or GIF files; otherwise <see langword="false"/>.</returns>
    internal static bool HasMultipleFrames(string imagePath) =>
        Path.GetExtension(imagePath).ToLowerInvariant() is ".tif" or ".tiff" or ".gif";

    /// <summary>Creates a grayscale copy of an image using weighted RGB luminance.</summary>
    /// <param name="image">Source bitmap. The caller retains ownership of this bitmap.</param>
    /// <returns>A new grayscale bitmap with the source dimensions and alpha values.</returns>
    internal static SKBitmap ConvertToGrayscale(SKBitmap image)
    {
        var grayscale = new SKBitmap(image.Width, image.Height);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                SKColor pixel = image.GetPixel(x, y);
                byte luminance = (byte)Math.Clamp(
                    (int)Math.Round(0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue),
                    0,
                    255);
                grayscale.SetPixel(x, y, new SKColor(luminance, luminance, luminance, pixel.Alpha));
            }
        }

        return grayscale;
    }
}

/// <summary>Writes OCR text for a single image to its output file.</summary>
internal static class ImageOutput
{
    /// <summary>Saves image OCR text using the input image's base name.</summary>
    /// <param name="options">Run settings that provide the input and output paths.</param>
    /// <param name="text">Recognized text to save.</param>
    internal static void SaveText(RunOptions options, string text)
    {
        Directory.CreateDirectory(options.OutputDir);
        string outputPath = Path.Combine(
            options.OutputDir,
            Path.GetFileNameWithoutExtension(options.InputPath) + ".txt");
        File.WriteAllText(outputPath, text, Encoding.UTF8);
        ConsoleHelper.WriteLine($"  ✓ Saved: {outputPath} ({text.Length:N0} chars)", ConsoleColor.Green);
    }
}

/// <summary>Recognizes supported inputs and builds run settings from arguments or prompts.</summary>
internal static class CliOptions
{
    /// <summary>File extensions accepted as image inputs.</summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp", ".gif"
    };

    /// <summary>Determines whether a path has a supported raster-image extension.</summary>
    /// <param name="path">File path to inspect.</param>
    /// <returns><see langword="true"/> when the extension is supported.</returns>
    internal static bool IsSupportedImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path));

    /// <summary>Determines whether a path has the PDF extension.</summary>
    /// <param name="path">File path to inspect.</param>
    /// <returns><see langword="true"/> when the extension is .pdf.</returns>
    internal static bool IsPdf(string path) =>
        Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>Determines whether a path is a supported image or PDF.</summary>
    /// <param name="path">File path to inspect.</param>
    /// <returns><see langword="true"/> when the file type is supported.</returns>
    internal static bool IsSupportedFile(string path) =>
        IsSupportedImage(path) || IsPdf(path);

    /// <summary>Parses command-line arguments or prompts the user for missing options.</summary>
    /// <param name="args">Arguments passed to the application.</param>
    /// <param name="options">Receives the parsed run settings when parsing succeeds.</param>
    /// <returns><see langword="false"/> when help was shown; otherwise <see langword="true"/>.</returns>
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

    /// <summary>Collects input, processing, and output choices through interactive prompts.</summary>
    /// <returns>Validated options for the requested run.</returns>
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

    /// <summary>Parses explicit arguments and asks only for applicable values that were omitted.</summary>
    /// <param name="args">Command-line arguments to parse.</param>
    /// <returns>Validated options for the requested run.</returns>
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

    /// <summary>Validates an input path and determines whether it is a PDF, image, or directory.</summary>
    /// <param name="path">Input path provided by the user.</param>
    /// <returns>The corresponding input kind.</returns>
    /// <exception cref="FileNotFoundException">The path does not exist.</exception>
    /// <exception cref="NotSupportedException">The file extension is not supported.</exception>
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

    /// <summary>Prints command-line usage, supported options, and example invocations.</summary>
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

    /// <summary>Filters supported directory files according to the requested file-type selection.</summary>
    /// <param name="files">Supported candidate files discovered in the directory.</param>
    /// <param name="selection">File types to retain.</param>
    /// <returns>The candidate files included by the selection.</returns>
    internal static IEnumerable<string> SelectDirectoryFiles(
        IEnumerable<string> files,
        DirectoryFileSelection selection) => selection switch
    {
        DirectoryFileSelection.PdfOnly => files.Where(IsPdf),
        DirectoryFileSelection.ImagesOnly => files.Where(IsSupportedImage),
        _ => files
    };
}

/// <summary>Locates Tesseract and installs the trained language data needed for OCR.</summary>
internal static class TesseractSetup
{
    /// <summary>Base URL used to download missing trained-data files.</summary>
    private const string BaseUrl =
        "https://github.com/tesseract-ocr/tessdata_best/raw/main/";

    /// <summary>Finds or creates the platform-specific Tesseract tessdata directory.</summary>
    /// <returns>Path where Tesseract language data is stored.</returns>
    /// <exception cref="PlatformNotSupportedException">The operating system is not supported.</exception>
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

    /// <summary>Finds the installed Tesseract executable or returns its command name.</summary>
    /// <returns>An executable path suitable for starting Tesseract.</returns>
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

    /// <summary>Downloads any missing trained-data files for the requested OCR languages.</summary>
    /// <param name="languages">Language codes such as <c>fas</c> and <c>eng</c>.</param>
    /// <exception cref="InvalidOperationException">A language-data file could not be downloaded.</exception>
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

/// <summary>Extracts embedded PDF text and reads page dimensions.</summary>
internal static class PdfTextExtractor
{
    /// <summary>Extracts text from each PDF page, preferring Poppler for robust text layout.</summary>
    /// <param name="pdfPath">PDF file to read.</param>
    /// <returns>One extracted text string for each page, in document order.</returns>
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

    /// <summary>Uses the Poppler command-line tool to extract UTF-8 text while preserving layout.</summary>
    /// <param name="pdfPath">PDF file to read.</param>
    /// <param name="text">Receives extracted text when Poppler is available.</param>
    /// <returns><see langword="true"/> when Poppler completed successfully; <see langword="false"/> if it is unavailable.</returns>
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

    /// <summary>Reads the width and height of every page in a PDF.</summary>
    /// <param name="pdfPath">PDF file to inspect.</param>
    /// <returns>Page dimensions in PDF points, in document order.</returns>
    internal static (double Width, double Height)[] GetPageSizes(string pdfPath)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        return document.GetPages()
            .Select(page => (Convert.ToDouble(page.Width), Convert.ToDouble(page.Height)))
            .ToArray();
    }
}

/// <summary>Renders PDF pages as temporary PNG images for OCR processing.</summary>
internal static class PdfRenderer
{
    /// <summary>Synchronizes progress messages emitted by parallel page rendering.</summary>
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

    /// <summary>Renders every page within the selected resolution bounds while preserving aspect ratio.</summary>
    /// <param name="pdfPath">PDF file to render.</param>
    /// <param name="tempDir">Directory where page PNG files are created.</param>
    /// <param name="renderSize">Maximum rendering resolution for each page.</param>
    /// <returns>Temporary page-image paths in PDF page order.</returns>
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

/// <summary>Runs Tesseract against one or more images and returns recognized text.</summary>
internal static class OcrEngine
{
    /// <summary>Synchronizes status messages emitted by parallel image recognition.</summary>
    private static readonly object _consoleLock = new();

    /// <summary>Recognizes text from multiple images concurrently, preserving input order in the result.</summary>
    /// <param name="imageFiles">Image paths to process.</param>
    /// <param name="tessDataDir">Directory containing the requested Tesseract language data.</param>
    /// <param name="language">Tesseract language code or codes joined with '+'.</param>
    /// <returns>Recognized text for each image, at the matching index.</returns>
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

    /// <summary>Prepares a still image as grayscale and recognizes its text.</summary>
    /// <param name="imagePath">Path of the source image.</param>
    /// <param name="tessDataDir">Directory containing the requested Tesseract language data.</param>
    /// <param name="language">Tesseract language code or codes joined with '+'.</param>
    /// <returns>Recognized text.</returns>
    internal static string RecognizeFile(string imagePath, string tessDataDir, string language)
    {
        if (ImageProcessor.HasMultipleFrames(imagePath))
            return RecognizeFileWithoutPreprocessing(imagePath, tessDataDir, language);

        using var image = ImageProcessor.Load(imagePath);
        using var grayscaleImage = ImageProcessor.ConvertToGrayscale(image);
        return RecognizeImage(grayscaleImage, tessDataDir, language);
    }

    /// <summary>Encodes a bitmap temporarily as PNG and asks Tesseract to recognize its text.</summary>
    /// <param name="image">Image bitmap to recognize.</param>
    /// <param name="tessDataDir">Directory containing the requested Tesseract language data.</param>
    /// <param name="language">Tesseract language code or codes joined with '+'.</param>
    /// <returns>Recognized text.</returns>
    internal static string RecognizeImage(SKBitmap image, string tessDataDir, string language)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}.png");
        try
        {
            using (var skImage = SKImage.FromBitmap(image))
            using (var encodedImage = skImage.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(tempPath, encodedImage.ToArray());

            return RecognizeFileWithoutPreprocessing(tempPath, tessDataDir, language);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>Invokes Tesseract for an image that should not be preprocessed.</summary>
    /// <param name="imagePath">Image path passed directly to Tesseract.</param>
    /// <param name="tessDataDir">Directory containing the requested Tesseract language data.</param>
    /// <param name="language">Tesseract language code or codes joined with '+'.</param>
    /// <returns>Recognized text.</returns>
    private static string RecognizeFileWithoutPreprocessing(string imagePath, string tessDataDir, string language)
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

/// <summary>Coordinates input processing, OCR or extraction, and text-file output.</summary>
internal static class OcrPipeline
{
    /// <summary>Pattern used to find the first numeric value in a file name.</summary>
    private static readonly Regex NumberPattern = new(@"\d+", RegexOptions.Compiled);

    /// <summary>Determines whether the selected input requires Tesseract OCR.</summary>
    /// <param name="opts">Validated run options.</param>
    /// <returns><see langword="true"/> when an image or OCR-mode PDF will be processed.</returns>
    internal static bool RequiresOcr(RunOptions opts) => opts.Kind switch
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

    /// <summary>Creates the output directory and dispatches processing for the selected input kind.</summary>
    /// <param name="opts">Validated run options.</param>
    internal static void Run(RunOptions opts)
    {
        Directory.CreateDirectory(opts.OutputDir);
        string tessDataDir = RequiresOcr(opts) ? TesseractSetup.GetTessDataPath() : string.Empty;

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

    /// <summary>Loads, grayscales, and recognizes a single still image before saving its text.</summary>
    /// <param name="opts">Validated image input and output settings.</param>
    /// <param name="tessDataDir">Directory containing the required Tesseract language data.</param>
    private static void RunSingleImage(RunOptions opts, string tessDataDir)
    {
        using var image = ImageProcessor.Load(opts.InputPath);
        using var grayscaleImage = ImageProcessor.ConvertToGrayscale(image);
        string text = OcrEngine.RecognizeImage(grayscaleImage, tessDataDir, opts.Language);
        ImageOutput.SaveText(opts, text);
    }

    /// <summary>Extracts embedded PDF text or renders and OCRs the PDF pages.</summary>
    /// <param name="opts">Validated PDF input, processing, and output settings.</param>
    /// <param name="tessDataDir">Directory containing Tesseract language data for OCR mode.</param>
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

    /// <summary>Processes the selected directory files and writes the requested output layout.</summary>
    /// <param name="opts">Validated directory selection and output settings.</param>
    /// <param name="tessDataDir">Directory containing Tesseract language data when OCR is required.</param>
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

    /// <summary>Processes one PDF for use as a text entry in combined directory output.</summary>
    /// <param name="pdfPath">PDF file to process.</param>
    /// <param name="opts">PDF extraction or OCR settings.</param>
    /// <param name="tessDataDir">Directory containing Tesseract language data for OCR mode.</param>
    /// <returns>Page-labeled text extracted from the PDF.</returns>
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

    /// <summary>Sorts file paths using the selected ordering strategy.</summary>
    /// <param name="files">File paths to sort.</param>
    /// <param name="sortBy">Ordering strategy to apply.</param>
    /// <returns>A new array in the selected order.</returns>
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

    /// <summary>Returns the first number found in a file name, or zero when none exists.</summary>
    /// <param name="name">File name to inspect.</param>
    /// <returns>The first parsed integer in the name, or zero.</returns>
    private static int ExtractNumber(string name)
    {
        var match = NumberPattern.Match(name);
        return match.Success && int.TryParse(match.Value, out int n) ? n : 0;
    }

    /// <summary>Formats non-empty recognized text with a heading for each source file.</summary>
    /// <param name="sourceFiles">Source paths corresponding to the text entries.</param>
    /// <param name="texts">Recognized text at the matching source-file index.</param>
    /// <returns>Combined text with source headings.</returns>
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

    /// <summary>Formats non-empty page text with a page-number heading.</summary>
    /// <param name="pageTexts">Extracted or recognized page text in page order.</param>
    /// <returns>Combined page-labeled text.</returns>
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

    /// <summary>Writes combined page-labeled PDF text to a file.</summary>
    /// <param name="outFile">Destination text-file path.</param>
    /// <param name="pageTexts">Text for each PDF page.</param>
    private static void WritePdfCombined(string outFile, string[] pageTexts)
    {
        File.WriteAllText(outFile, FormatPdfPages(pageTexts), Encoding.UTF8);
        ConsoleHelper.WriteLine($"PDF text saved to: {outFile}", ConsoleColor.Green);
    }

    /// <summary>Writes each PDF page's text to its own page-numbered file.</summary>
    /// <param name="outputDir">Destination directory.</param>
    /// <param name="pdfName">PDF base name used in output filenames.</param>
    /// <param name="pageTexts">Text for each PDF page.</param>
    private static void WritePdfPages(string outputDir, string pdfName, string[] pageTexts)
    {
        for (int i = 0; i < pageTexts.Length; i++)
        {
            string outFile = Path.Combine(outputDir, $"{pdfName}_Page_{i + 1:D5}.txt");
            File.WriteAllText(outFile, pageTexts[i], Encoding.UTF8);
        }

        ConsoleHelper.WriteLine($"Saved {pageTexts.Length} PDF page text file(s) to: {outputDir}", ConsoleColor.Green);
    }

    /// <summary>Writes one text file per PDF and one combined text file for all images.</summary>
    /// <param name="outputDir">Destination directory.</param>
    /// <param name="sourceFiles">Selected directory files.</param>
    /// <param name="texts">Recognized or extracted text at the matching source-file index.</param>
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

    /// <summary>Writes combined source-labeled text to one UTF-8 file.</summary>
    /// <param name="outFile">Destination text-file path.</param>
    /// <param name="sourceFiles">Source paths corresponding to the text entries.</param>
    /// <param name="texts">Text entries in source-file order.</param>
    private static void WriteCombined(string outFile, string[] sourceFiles, string[] texts)
    {
        File.WriteAllText(outFile, FormatCombined(sourceFiles, texts), Encoding.UTF8);
        ConsoleHelper.WriteLine($"Combined output saved to: {outFile}", ConsoleColor.Green);
    }

    /// <summary>Writes one text file for each source file.</summary>
    /// <param name="outputDir">Destination directory.</param>
    /// <param name="sourceFiles">Source paths used to derive output names.</param>
    /// <param name="texts">Text entries at the matching source-file index.</param>
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
