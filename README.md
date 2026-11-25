# OCR

A unified, cross-platform CLI & interactive OCR tool for **PDFs**, **images**, and **image directories** using Tesseract OCR.

## Features

- **Any supported input**:
  - **PDF (`.pdf`)** — renders pages in parallel and runs OCR, outputting either **one combined `.txt`** for the whole PDF or **one `.txt` per page**
  - **Single image** (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`, `.webp`, `.gif`) — outputs **one `.txt` file**
  - **Directory of images** — outputs either **one combined `.txt`** (with sorting by `number`, `name`, `date`, or `size`) or **one `.txt` per image**
- **Auto-downloads** `fas.traineddata` and `eng.traineddata` (`tessdata_best`) to the OS Tesseract directory:
  - Linux: `/usr/share/tesseract-ocr/5/tessdata`
  - Windows: `C:\Program Files\Tesseract-OCR\tessdata`
- **Dual mode**: Run with `-i` / CLI flags or run with no arguments for interactive prompts; `-h` / `--help` for usage.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for building from source)
- [Tesseract OCR engine](https://tesseract-ocr.github.io/tessdoc/Installation.html) installed on the system

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

## Architecture

```
Ocr/
├── GlobalUsings.cs   →  All global usings
├── Program.cs        →  Clean presentation-style entry point
├── StaticMethods.cs  →  SRP-organized helpers & OCR pipeline
└── Ocr.csproj        →  .NET 10, compressed single-file publish config
```
