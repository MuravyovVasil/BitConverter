using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BitConverter.Core;

public class ReverseEngine
{
    private const int BufferSize = 1048576; // 1 MB
    private static readonly byte[] ExpectedMagic = { (byte)'M', (byte)'B', (byte)'I', (byte)'T', 3 };

    public async Task ReverseFileAsync(string mbitPath, string originalOutPath, IProgress<double> progress, CancellationToken ct)
    {
        long totalBytes = new FileInfo(mbitPath).Length;

        // EDGE CASE 1: File is too small to contain even a valid header.
        if (totalBytes < 5)
        {
            throw new InvalidDataException("File is too small to be a valid .mbit archive.");
        }

        await using var outStream = new FileStream(originalOutPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
        await using var inStream = new FileStream(mbitPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);

        byte[] magic = new byte[5];
        int read = await inStream.ReadAsync(magic, 0, 5, ct);
        if (read < 5 || !magic.AsSpan().SequenceEqual(ExpectedMagic))
        {
            throw new InvalidDataException("Invalid file format. Expected MBIT3 magic header.");
        }

        // EDGE CASE 2: Empty archive (only 5-byte header exists).
        if (totalBytes == 5)
        {
            progress?.Report(100);
            return;
        }

        var lz77 = new Lz77Engine(CompressionOptions.Standard());
        var huffman = new HuffmanEngine();

        long processedBytes = 5;
        byte[] header = new byte[9];

        while (processedBytes < totalBytes)
        {
            int hRead = await inStream.ReadAsync(header, 0, 9, ct);
            if (hRead < 9) break;
            processedBytes += 9;

            byte chunkType = header[0];
            int compSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1, 4));
            int uncompSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(5, 4)); // Only needed for allocation if used differently

            byte[] compressedChunk = new byte[compSize];
            await inStream.ReadExactlyAsync(compressedChunk, 0, compSize, ct);
            processedBytes += compSize;

            byte[] uncompressedChunk;

            // Adaptive decoding based on ChunkType
            if (chunkType == 0) // Type 0: Raw Data
            {
                uncompressedChunk = compressedChunk;
            }
            else if (chunkType == 1) // Type 1: LZ77 Only
            {
                uncompressedChunk = await Task.Run(() => lz77.Decompress(compressedChunk), ct);
            }
            else if (chunkType == 2) // Type 2: LZ77 + Dynamic Huffman (DEFLATE approach)
            {
                byte[] lz77Data = await Task.Run(() => huffman.Decompress(compressedChunk), ct);
                uncompressedChunk = await Task.Run(() => lz77.Decompress(lz77Data), ct);
            }
            else if (chunkType == 3) // Type 3: Huffman Only
            {
                uncompressedChunk = await Task.Run(() => huffman.Decompress(compressedChunk), ct);
            }
            else
            {
                throw new InvalidDataException($"Unknown chunk type detected: {chunkType}. Archive might be corrupted.");
            }

            await outStream.WriteAsync(uncompressedChunk, ct);
            progress?.Report(Math.Min(100, ((double)processedBytes / totalBytes) * 100));
        }

        progress?.Report(100);
    }
}