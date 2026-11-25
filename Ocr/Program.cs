// ──────────────────────────────────────────────────────────────
//  Ocr  —  Unified PDF / Image / Directory OCR Pipeline
// ──────────────────────────────────────────────────────────────

return AppRunner.Execute(() =>
{
    if (!CliOptions.TryParse(args, out var options))
        return;

    TesseractSetup.EnsureTrainedData("fas", "eng");
    OcrPipeline.Run(options);
    ConsoleHelper.Done("OCR processing completed successfully.", options.Interactive);
});
