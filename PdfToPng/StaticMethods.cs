using Syncfusion.PdfToImageConverter;

namespace PdfToPng;

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

    internal static int AskInt(string prompt)
    {
        string raw = Ask(prompt);
        if (!int.TryParse(raw, out int result) || result <= 0)
            throw new ArgumentException($"'{prompt}' must be a positive integer.");
        return result;
    }

    internal static float AskFloat(string prompt)
    {
        string raw = Ask(prompt);
        if (!float.TryParse(raw, out float result) || result <= 0)
            throw new ArgumentException($"'{prompt}' must be a positive number.");
        return result;
    }

    internal static void Done(string message)
    {
        Console.WriteLine($"\n✓ {message}");
        Console.Write("Press any key to exit...");
        Console.Read();
    }
}

// ───────────────────────────────────────────────
//  PdfConverter  —  PDF-to-PNG conversion logic
// ───────────────────────────────────────────────
internal static class PdfConverter
{
    private static readonly object _consoleLock = new();

    internal static void ConvertAllPages(string pdfPath, string outputDir,
                                         int width, int height, float scale)
    {
        ValidateInputs(pdfPath, outputDir);

        int pageCount = GetPageCount(pdfPath);
        Console.WriteLine($"Found {pageCount} page(s) in the PDF.");

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        Parallel.For(0, pageCount, options, i =>
        {
            try
            {
                ConvertSinglePage(pdfPath, outputDir, i, width, height, scale);
            }
            catch (Exception ex)
            {
                WriteThreadSafe($"⚠ Error on page {i + 1}: {ex.Message}");
            }
        });
    }

    // ── private helpers ──

    private static void ValidateInputs(string pdfPath, string outputDir)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException($"PDF not found: {pdfPath}");

        Directory.CreateDirectory(outputDir);
    }

    private static int GetPageCount(string pdfPath)
    {
        using var stream    = File.OpenRead(pdfPath);
        using var converter = new PdfToImageConverter();
        converter.Load(stream);

        if (converter.PageCount == 0)
            throw new InvalidOperationException("PDF has zero pages.");

        return converter.PageCount;
    }

    private static void ConvertSinglePage(string pdfPath, string outputDir,
                                           int pageIndex,
                                           int width, int height, float scale)
    {
        using var stream    = File.OpenRead(pdfPath);
        using var converter = new PdfToImageConverter();
        converter.Load(stream);
        converter.ScaleFactor = scale;

        var size   = new Syncfusion.Drawing.SizeF(width, height);
        var images = converter.Convert(pageIndex, pageIndex, size, true, false, true);

        if (images[0] is not { Length: > 0 } image)
        {
            WriteThreadSafe($"⚠ Page {pageIndex + 1} produced an empty image.");
            return;
        }

        image.Position = 0;
        string fileName = $"Page_{pageIndex + 1:D5}.png";
        string filePath = Path.Combine(outputDir, fileName);

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        image.CopyTo(fs);
        image.Dispose();

        WriteThreadSafe($"  Saved {fileName} ({fs.Length:N0} bytes)");
    }

    private static void WriteThreadSafe(string msg)
    {
        lock (_consoleLock) { Console.WriteLine(msg); }
    }
}
