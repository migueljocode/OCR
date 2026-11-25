namespace PngToTxt;

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

    internal static string AskLanguage()
    {
        Console.WriteLine("Select OCR language:");
        Console.WriteLine("  1. Persian (fas)");
        Console.WriteLine("  2. English (eng)");
        Console.WriteLine("  3. Both    (fas+eng)");
        Console.Write("Choice [1/2/3]: ");

        return Console.ReadLine()?.Trim() switch
        {
            "1" => "fas",
            "2" => "eng",
            "3" => "fas+eng",
            _   => throw new ArgumentException("Invalid language selection.")
        };
    }

    internal static void Done(string message)
    {
        Console.WriteLine($"\n✓ {message}");
        Console.Write("Press any key to exit...");
        Console.Read();
    }
}

// ───────────────────────────────────────────────
//  TesseractSetup  —  auto-download traineddata
// ───────────────────────────────────────────────
internal static class TesseractSetup
{
    private const string BaseUrl =
        "https://github.com/tesseract-ocr/tessdata_best/raw/main/";

    /// <summary>
    /// Returns the OS-specific tessdata directory where Tesseract
    /// looks for .traineddata files.
    /// </summary>
    internal static string GetTessDataPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Standard Ubuntu/Debian Tesseract 5 path
            string[] candidates =
            [
                "/usr/share/tesseract-ocr/5/tessdata",
                "/usr/share/tesseract-ocr/4/tessdata",
                "/usr/share/tessdata"
            ];
            foreach (string dir in candidates)
                if (Directory.Exists(dir))
                    return dir;

            // Fallback: create the Tesseract 5 path
            string fallback = candidates[0];
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Standard Windows Tesseract install paths
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

            // Fallback: use AppData
            string fallback = candidates[^1];
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        throw new PlatformNotSupportedException(
            "Only Linux and Windows are supported.");
    }

    /// <summary>
    /// Returns the OS-specific tesseract executable path or command name.
    /// </summary>
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

    /// <summary>
    /// Downloads any missing .traineddata files for the requested
    /// languages into the OS tessdata directory.
    /// </summary>
    internal static void EnsureTrainedData(params string[] languages)
    {
        string tessDataDir = GetTessDataPath();
        Console.WriteLine($"Tessdata directory: {tessDataDir}");

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(5);

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
                Console.WriteLine($"FAILED");
                throw new InvalidOperationException(
                    $"Could not download {lang}.traineddata from {url}. " +
                    $"Error: {ex.Message}\n" +
                    $"Please download it manually to: {tessDataDir}");
            }
        }
    }
}

// ───────────────────────────────────────────────
//  ImagePreprocessor  —  grayscale via SkiaSharp
// ───────────────────────────────────────────────
internal static class ImagePreprocessor
{
    /// <summary>
    /// Loads an image, converts to grayscale, saves to a temp file,
    /// and returns the temp path. Caller must delete the file.
    /// </summary>
    internal static string ToGrayscaleTempFile(string sourcePath)
    {
        string tempPath = Path.Combine(Path.GetTempPath(),
                                       $"{Guid.NewGuid()}.png");

        using var original = SKBitmap.Decode(sourcePath);
        using var grayscale = new SKBitmap(original.Width, original.Height);

        using (var canvas = new SKCanvas(grayscale))
        {
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(
            [
                0.299f, 0.587f, 0.114f, 0, 0,
                0.299f, 0.587f, 0.114f, 0, 0,
                0.299f, 0.587f, 0.114f, 0, 0,
                0,      0,      0,      1, 0
            ]);
            canvas.DrawBitmap(original, 0, 0, paint);
        }

        using var image = SKImage.FromBitmap(grayscale);
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(tempPath);
        data.SaveTo(stream);

        return tempPath;
    }
}

// ───────────────────────────────────────────────
//  OcrProcessor  —  multi-threaded OCR engine
// ───────────────────────────────────────────────
internal static class OcrProcessor
{
    private static readonly object _consoleLock = new();
    private static readonly object _writerLock  = new();

    internal static void ProcessAllImages(string inputDir, string outputDir,
                                          string language)
    {
        if (!Directory.Exists(inputDir))
            throw new DirectoryNotFoundException($"Input directory not found: {inputDir}");

        Directory.CreateDirectory(outputDir);

        string[] pngFiles = Directory.GetFiles(inputDir, "*.png");
        if (pngFiles.Length == 0)
            throw new FileNotFoundException($"No PNG files found in '{inputDir}'.");

        Console.WriteLine($"Found {pngFiles.Length} PNG file(s) for OCR.");

        string tessDataDir  = TesseractSetup.GetTessDataPath();
        string combinedPath = Path.Combine(outputDir, "combined_output.txt");

        using var combinedWriter = new StreamWriter(combinedPath, false,
                                                     Encoding.UTF8);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        Parallel.ForEach(pngFiles, options, pngFile =>
        {
            ProcessSingleImage(pngFile, outputDir, language,
                               tessDataDir, combinedWriter);
        });

        Console.WriteLine($"Combined output saved to: {combinedPath}");
    }

    // ── private helpers ──

    private static void ProcessSingleImage(string pngFile, string outputDir,
                                            string language, string tessDataDir,
                                            StreamWriter combinedWriter)
    {
        string? tempGray = null;
        try
        {
            tempGray = ImagePreprocessor.ToGrayscaleTempFile(pngFile);

            string text = RunTesseract(tempGray, tessDataDir, language);
            string name = Path.GetFileName(pngFile);

            if (string.IsNullOrWhiteSpace(text))
            {
                WriteThreadSafe($"  ⚠ No text in '{name}'.");
                return;
            }

            // Save individual file
            string outFile = Path.Combine(outputDir,
                Path.GetFileNameWithoutExtension(pngFile) + ".txt");
            File.WriteAllText(outFile, text, Encoding.UTF8);
            WriteThreadSafe($"  ✓ {name} → {text.Length:N0} chars");

            // Append to combined file
            lock (_writerLock)
            {
                combinedWriter.WriteLine($"=== {name} ===");
                combinedWriter.WriteLine(text);
                combinedWriter.WriteLine();
            }
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException?.Message ?? ex.Message;
            WriteThreadSafe($"  ✗ Error on '{Path.GetFileName(pngFile)}': {msg}");
        }
        finally
        {
            if (tempGray != null && File.Exists(tempGray))
                File.Delete(tempGray);
        }
    }

    private static string RunTesseract(string imagePath, string tessDataDir, string language)
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
        psi.ArgumentList.Add(imagePath);
        psi.ArgumentList.Add("stdout");
        psi.ArgumentList.Add("--tessdata-dir");
        psi.ArgumentList.Add(tessDataDir);
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add(language);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start tesseract process.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"tesseract exited with code {process.ExitCode}: {error.Trim()}");

        return output;
    }

    private static void WriteThreadSafe(string msg)
    {
        lock (_consoleLock) { Console.WriteLine(msg); }
    }
}
