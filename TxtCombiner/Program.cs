// ──────────────────────────────────────────────────────────
//  TxtCombiner  —  Merge ordered Page_*.txt into one file
// ──────────────────────────────────────────────────────────

string inputDir  = ConsoleHelper.Ask("Directory containing Page_*.txt files");
string outputDir = ConsoleHelper.Ask("Output directory");

TextMerger.CombinePageFiles(inputDir, outputDir);

ConsoleHelper.Done("Text files combined successfully.");
