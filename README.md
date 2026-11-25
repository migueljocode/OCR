# OCR

A unified, cross-platform CLI & interactive text extraction tool for **PDFs**, **images**, and **directories of PDFs and/or images**.

## Features

- **Any supported input**:
  - **PDF (`.pdf`)** — choose direct embedded-text extraction via Poppler for text PDFs or page rendering plus OCR for scans/screenshots; output either **one combined `.txt`** or **one `.txt` per page**
  - **Single image** (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`, `.webp`, `.gif`) — converts still images to grayscale before OCR and outputs **one `.txt` file**; multi-page TIFFs and animated GIFs are passed directly to Tesseract to preserve their frames
  - **Directory of PDFs and/or images** — when both types are present, choose whether to process both, PDFs only, or images only; choose a combined output, one `.txt` per input, or (for mixed inputs) one `.txt` per PDF plus one combined image `.txt` (`<file>.pdf.txt` for each PDF)
- **OCR language data** (`fas.traineddata` and `eng.traineddata`, `tessdata_best`) is auto-downloaded only when OCR is selected:
  - Linux: `/usr/share/tesseract-ocr/5/tessdata`
  - Windows: `C:\Program Files\Tesseract-OCR\tessdata`
- **Dual mode**: Run with `-i` / CLI flags or run with no arguments for interactive prompts; `-h` / `--help` for usage.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for building from source)
- [Poppler](https://poppler.freedesktop.org/) (`pdftotext`) for best-quality direct PDF text extraction; the built-in extractor is used if it is unavailable
- [Tesseract OCR engine](https://tesseract-ocr.github.io/tessdoc/Installation.html) installed on the system for images and scanned-PDF OCR; direct PDF text extraction does not require Tesseract

### Linux (Ubuntu/Debian)
```bash
sudo apt update && sudo apt install -y tesseract-ocr
```

### Windows
Download and install from: https://github.com/UB-Mannheim/tesseract/wiki

## Build & Run

```bash
dotnet build OCR.slnx
dotnet run --project Ocr -- -h
```

## Publish (Single-File Executable)

```bash
# Linux x64
dotnet publish Ocr -c Release -r linux-x64

# Windows x64
dotnet publish Ocr -c Release -r win-x64
```

Published binary goes to `Ocr/publish/<rid>/` (single self-contained compressed executable, no `.pdb`).

For command-line PDF processing, `--pdf-mode extract` reads embedded text directly, while `--pdf-mode ocr` renders pages for OCR. If relevant options are omitted, the CLI prompts for them: PDF processing method, mixed-directory file selection, OCR language, output layout, sorting, and render size. To bypass all questions, supply the relevant options, such as `--files both --pdf-mode ocr -l fas+eng -m single -s number --render-size fullhd` for a mixed directory. When every relevant decision is provided and the process is attached to a terminal, the screen is cleared before progress output. For mixed directories, `--files both|pdf|images` selects which files to process, and `-m split` writes one text file per PDF and one combined text file for images. Choose `--render-size hd`, `fullhd`, or `4k` for PDF OCR; page aspect ratios are preserved. For example:

```bash
dotnet run --project Ocr -- -i document.pdf -o ./out --pdf-mode extract
dotnet run --project Ocr -- -i scanned.pdf -o ./out --pdf-mode ocr --render-size 4k -l fas+eng
dotnet run --project Ocr -- -i ./mixed -o ./out --files pdf --pdf-mode extract
dotnet run --project Ocr -- -i ./mixed -o ./out --files both -m split
dotnet run --project Ocr -- -i ./mixed -o ./out --files both --pdf-mode ocr -l fas+eng -m single -s number --render-size fullhd
```

## Architecture

```
Ocr/
├── GlobalUsings.cs   →  All global usings
├── Program.cs        →  Clean presentation-style entry point
├── StaticMethods.cs  →  SRP-organized helpers & OCR pipeline
└── Ocr.csproj        →  .NET 10, compressed single-file publish config
```
