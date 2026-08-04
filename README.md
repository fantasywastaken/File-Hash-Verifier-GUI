# File Hash Verifier GUI

A fast, professional Windows desktop tool for computing and verifying cryptographic file hashes.

### How It Works

File Hash Verifier GUI is a native WPF application built on .NET 8 that computes MD5, SHA1, SHA256, and SHA512 checksums for any file you drop onto its window. When a file is added, the app reads it once into memory and runs all four cryptographic algorithms in parallel through `Task.Run` and `Task.WhenAll`, keeping the UI responsive with an indeterminate progress indicator during computation. Results are cached in memory per file path, so reselecting a previously hashed file is instant. A verification panel lets you paste an expected hash value, pick the algorithm, and instantly compare it against the computed digest. A green row highlight signals a match, red signals a mismatch, and the comparison is case-insensitive and whitespace-tolerant so hashes copied from any source work without cleanup.

## Setup

### Requirements

- Windows 10 or Windows 11
- .NET 8 SDK (for building) or .NET 8 Desktop Runtime (for running)

### Build

```
dotnet build "File-Hash-Verifier-GUI.csproj" -c Release
```

The compiled executable will be placed under `bin\Release\net8.0-windows\File-Hash-Verifier-GUI.exe`.

To run it directly during development:

```
dotnet run --project "File-Hash-Verifier-GUI.csproj"
```

### Usage

1. Launch `File-Hash-Verifier-GUI.exe`.
2. Drag one or more files onto the drop zone, or click **Browse Files** to pick them from an open file dialog.
3. Select a file from the left list. All four hashes are computed on a background thread and displayed on the right.
4. Click **Copy** next to any hash row to copy that digest to the clipboard.
5. To verify a download, paste the publisher's expected hash into the **Verify Against Expected Hash** box, choose the matching algorithm from the dropdown, and press **Verify**. The corresponding row turns green on match or red on mismatch.
6. Click **Clear** to remove all files and reset the workspace.

### Features

- Drag-and-drop file input with a visible highlighted drop zone
- Multi-file selection through a native open file dialog
- Simultaneous computation of MD5, SHA1, SHA256, and SHA512 in parallel
- One-click copy for every computed hash
- Expected-hash verification with algorithm selector and pass/fail row highlighting
- In-memory cache keyed by file path so revisiting a file is instant
- Fully custom dark theme with hand-crafted control templates, no third-party UI libraries
- Non-blocking asynchronous hashing keeps the UI responsive on large files
- Graceful error handling for locked, missing, or unreadable files
