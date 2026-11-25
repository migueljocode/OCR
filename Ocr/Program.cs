// ──────────────────────────────────────────────────────────────
//  Ocr  —  Unified PDF / Image / Directory OCR Pipeline
// ──────────────────────────────────────────────────────────────

return AppRunner.Execute(() =>
{
    // 1. Read the input path and processing choices from CLI arguments or user prompts.
    if (!CliOptions.TryParse(args, out RunOptions options))
        return;

    // 2. Install the selected Tesseract language data only for workflows that use OCR.
    //    Direct extraction from a text-based PDF does not need the OCR engine.
    if (OcrPipeline.RequiresOcr(options))
        TesseractSetup.EnsureTrainedData(options.Language.Split('+'));

    // 3. Process a single image here so the image-processing steps are easy to follow.
    if (options.Kind == InputKind.Image)
    {
        string tessDataDir = TesseractSetup.GetTessDataPath();
        string text;

        if (ImageProcessor.HasMultipleFrames(options.InputPath))
        {
            // Send animated GIFs and multi-page TIFFs directly to Tesseract so no frames are lost.
            text = OcrEngine.RecognizeFile(options.InputPath, tessDataDir, options.Language);
        }
        else
        {
            // Load the image, convert its colors to grayscale, and ask Tesseract to read the text.
            using var image = ImageProcessor.Load(options.InputPath);
            using var grayscaleImage = ImageProcessor.ConvertToGrayscale(image);
            text = OcrEngine.RecognizeImage(grayscaleImage, tessDataDir, options.Language);
        }

        // Save the recognized text as <image-name>.txt in the requested output directory.
        ImageOutput.SaveText(options, text);
    }
    else
    {
        // PDFs and directories use the pipeline: extract or OCR PDF pages, process images,
        // then write combined, per-input, or split PDF/image text files.
        OcrPipeline.Run(options);
    }

    // 4. Report success and pause only when the app was launched interactively.
    ConsoleHelper.Done("OCR processing completed successfully.", options.Interactive);
});
