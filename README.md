# OCR Pipeline

A three-step pipeline that converts scanned PDFs into searchable text.

## Pipeline Steps

| Step | Project | Description |
|------|---------|-------------|
| 1 | **PdfToPng** | Converts each PDF page into a high-resolution PNG image |
| 2 | **PngToTxt** | Runs Tesseract OCR on each PNG (supports Persian & English) |
| 3 | **TxtCombiner** | Merges all `Page_*.txt` files into a single ordered text file |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) or later
- [Tesseract OCR engine](https://tesseract-ocr.github.io/tessdoc/Installation.html) installed on the system

> **Note:** `PngToTxt` will **automatically download** the best `fas.traineddata` and `eng.traineddata` files to your system's Tesseract data directory. No manual download needed.

### Linux (Ubuntu/Debian)
```bash
sudo apt update && sudo apt install -y tesseract-ocr
```

### Windows
Download and install from: https://github.com/UB-Mannheim/tesseract/wiki

## Build & Run

```bash
# Build entire solution
dotnet build

# Run each step
dotnet run --project PdfToPng
dotnet run --project PngToTxt
dotnet run --project TxtCombiner
```

## Publish

Each project can be published as a self-contained single-file executable:

```bash
# Linux x64
dotnet publish PdfToPng  -c Release -r linux-x64
dotnet publish PngToTxt  -c Release -r linux-x64
dotnet publish TxtCombiner -c Release -r linux-x64

# Windows x64
dotnet publish PdfToPng  -c Release -r win-x64
dotnet publish PngToTxt  -c Release -r win-x64
dotnet publish TxtCombiner -c Release -r win-x64
```

Published binaries go to `<project>/publish/<rid>/`.

## Architecture

```
Program.cs      →  Simple, self-explanatory entry point (< 10 lines)
StaticMethods.cs →  All business logic, organized by Single Responsibility
```

**Principles applied:** DRY · KISS · SRP
- Cross-platform (Linux & Windows)
- No traineddata files shipped in the project — auto-downloaded at runtime
- Thread-safe parallel processing for maximum performance
