# BitConverter 🚀
**High-Performance, Zero-Allocation Compression Engine & Custom Archiver**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13.0-239120?style=flat-square&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-12.0-purple?style=flat-square)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows%20%7C%20Linux-lightgrey?style=flat-square)](#)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)

BitConverter is a custom, production-ready compression engine built from scratch in C# 13.0. It implements a proprietary `.mbit` (v3.0) format utilizing a highly optimized, dual-stage **DEFLATE-like architecture** (LZ77 + Dynamic Huffman Coding). 

Designed for environments where memory pressure, CPU throughput, and evasion are critical constraints, the engine operates on a strict **Zero-Allocation** philosophy and heavily leverages hardware intrinsics.

## 🎯 Primary Use Cases
* **Mobile Game Development (Unity 3D):** On-the-fly asset decompression for mobile platforms without triggering Garbage Collection spikes (Drop Frames), minimizing RAM footprint.
* **Offensive Security (Red Teaming):** Custom payload obfuscation and in-memory execution. Standard EDR/AV static scanners cannot parse the proprietary `.mbit` format, making it an ideal evasion mechanism.
* **Enterprise Telemetry:** High-speed processing of custom binary geographic formats (TRK, SET, GPS tracking data) and massive log arrays using adaptive compression heuristics.

## 🧠 Core Engineering Achievements
This project demonstrates advanced .NET engineering, prioritizing mechanical sympathy and algorithmic efficiency:

* **Zero-Allocation Pipeline:** Completely eliminates GC pressure during the compression loop. Utilizes `ArrayPool<byte>`, `Span<T>`, and `Memory<T>` to process multi-gigabyte files. Fully leverages C# 13.0 features, including `ref` and `unsafe` contexts in asynchronous workflows.
* **Ordered Multi-threading:** Achieves maximum CPU utilization across all available cores using a sliding task window (`SemaphoreSlim` + `Task`). Blocks are read sequentially, compressed in parallel, and written back in strict order.
* **64-Bit Vectorized Scanning:** The LZ77 engine bypasses standard byte-by-byte comparison, utilizing `ulong` casting to compare 8 bytes per CPU cycle for lightning-fast history matching.
* **Fibonacci Hashing:** Implements O(1) dictionary lookups using the Golden Ratio multiplier (`0x9E3779B1`) to distribute 32-bit values evenly across the hash chain.
* **Adaptive 4-Way Heuristics:** The `PipelineEngine` acts as an "arena", evaluating every block simultaneously against 4 strategies (Raw, LZ77-only, Huffman-only, LZ77+Huffman) and saving the most mathematically optimal result.

## 📊 Performance Benchmarks
*Tested on Apple Silicon (M4 Pro, ARM64) compiled as a self-contained native executable.*

| Workload (Profile: Ultra) | Original Size | Compressed Size | Time | Throughput | Ratio |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Highly Compressible (Logs/JSON)** | 100.00 MB | ~14.20 MB | 0.85s | **117 MB/s** | 85.8% |
| **Mixed Data (Executables/Assets)** | 50.00 MB | ~22.50 MB | 0.65s | **76 MB/s** | 55.0% |
| **Incompressible (Encrypted/MP4)** | 100.00 MB | 100.00 MB | 0.12s | **833 MB/s** | 0.0% (Smart Fallback) |

> *Note: Incompressible data is instantly detected and bypassed via the chunking heuristic, preventing unnecessary CPU cycles and file bloat.*

## 📐 The `.mbit` Format Specification
BitConverter uses a custom binary format (`MBIT3`). It is a byte-aligned, block-based stream. 
A comprehensive, academic-style **RFC Specification** detailing the bit-level packing mechanics, mathematical proofs for overflow safety, and dictionary structures is available here:
👉 **[Read the MBIT Specification (PDF)](./docs/MBIT_Format_Specification.pdf)**

## 🖥️ Cross-Platform UI
The engine comes with a modern, responsive Graphical User Interface built with **Avalonia UI** (MVVM pattern via CommunityToolkit). It supports native drag-and-drop file processing and real-time metric reporting (Throughput, Execution Time, Compression Ratio).

## 🚀 Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
* C# 13.0 compiler support

### Build and Run (Cross-Platform)
```bash
# Clone the repository
git clone [https://github.com/vasilmuravjov/BitConverter.git](https://github.com/vasilmuravjov/BitConverter.git)
cd BitConverter

# Run the UI application
dotnet run --project src/BitConverter.UI
