// ───────────────────────────────────────────────────────
//  PngToTxt  —  OCR PNG images to text via Tesseract
// ───────────────────────────────────────────────────────

TesseractSetup.EnsureTrainedData("fas", "eng");

string inputDir  = ConsoleHelper.Ask("PNG images directory");
string outputDir = ConsoleHelper.Ask("Output directory for TXT files");
string language  = ConsoleHelper.AskLanguage();

OcrProcessor.ProcessAllImages(inputDir, outputDir, language);

ConsoleHelper.Done("OCR processing completed successfully.");
