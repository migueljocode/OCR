// ──────────────────────────────────────────────
//  PdfToPng  —  Convert each PDF page to a PNG
// ──────────────────────────────────────────────

using PdfToPng;

string pdfPath      = ConsoleHelper.Ask("PDF file path");
string outputDir    = ConsoleHelper.Ask("Output directory for PNGs");
int    width        = ConsoleHelper.AskInt("Output width  (e.g. 1920)");
int    height       = ConsoleHelper.AskInt("Output height (e.g. 1080)");
float  scale        = ConsoleHelper.AskFloat("Scale factor  (e.g. 1)");

PdfConverter.ConvertAllPages(pdfPath, outputDir, width, height, scale);

ConsoleHelper.Done("PDF pages converted to PNG successfully.");
