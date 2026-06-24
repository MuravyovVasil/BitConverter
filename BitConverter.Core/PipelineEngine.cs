using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BitConverter.Core;

public class PipelineEngine
{
    private const int BufferSize = 1048576; // 1 МБ
    private static readonly byte[] MagicHeader = { (byte)'M', (byte)'B', (byte)'I', (byte)'T', 3 };

    public async Task<int> ProcessFileAsync(string inputPath, string outputPath, IProgress<double> progress, CancellationToken ct, CompressionOptions? options = null)
    {
        // Якщо налаштування не передано, використовуємо Стандартні
        options ??= CompressionOptions.Standard();

        long totalBytes = new FileInfo(inputPath).Length;
        long processedBytes = 0;

        await using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, options.BlockSize, FileOptions.SequentialScan | FileOptions.Asynchronous);

        await outStream.WriteAsync(MagicHeader, ct);

        if (totalBytes == 0)
        {
            progress?.Report(100);
            return 1;
        }

        await using var inStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, options.BlockSize, FileOptions.SequentialScan | FileOptions.Asynchronous);

        int maxConcurrency = options.MaxConcurrency;
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var pendingWrites = new Queue<Task<byte[]>>();

        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(options.BlockSize);

        try
        {
            int bytesRead;
            while ((bytesRead = await inStream.ReadAsync(readBuffer, 0, options.BlockSize, ct)) > 0)
            {
                byte[] chunkToProcess = new byte[bytesRead];
                Array.Copy(readBuffer, chunkToProcess, bytesRead);

                await semaphore.WaitAsync(ct);

                var compressionTask = Task.Run(() =>
                {
                    try
                    {
                        return CompressBlockSync(chunkToProcess, options);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);

                pendingWrites.Enqueue(compressionTask);

                while (pendingWrites.Count >= maxConcurrency)
                {
                    var oldestTask = pendingWrites.Dequeue();
                    byte[] frameData = await oldestTask;
                    await outStream.WriteAsync(frameData, ct);

                    processedBytes += options.BlockSize;
                    progress?.Report(Math.Min(100, ((double)processedBytes / totalBytes) * 100));
                }
            }

            while (pendingWrites.Count > 0)
            {
                var oldestTask = pendingWrites.Dequeue();
                byte[] frameData = await oldestTask;
                await outStream.WriteAsync(frameData, ct);

                processedBytes += options.BlockSize;
                progress?.Report(Math.Min(100, ((double)processedBytes / totalBytes) * 100));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }

        ArrayPool<byte>.Shared.Return(readBuffer);
        progress?.Report(100);
        return 1;
    }

    private byte[] CompressBlockSync(byte[] exactChunk, CompressionOptions options)
    {
        if (exactChunk.Length < 4)
        {
            byte[] microFrame = new byte[exactChunk.Length + 9];
            microFrame[0] = 0;
            BinaryPrimitives.WriteInt32LittleEndian(microFrame.AsSpan(1, 4), exactChunk.Length);
            BinaryPrimitives.WriteInt32LittleEndian(microFrame.AsSpan(5, 4), exactChunk.Length);
            Array.Copy(exactChunk, 0, microFrame, 9, exactChunk.Length);
            return microFrame;
        }

        var lz77 = new Lz77Engine(options);
        var huffman = new HuffmanEngine();

        // 1. Тестуємо LZ77
        byte[] lz77Chunk = lz77.Compress(exactChunk);

        // 2. Тестуємо подвійне стиснення (LZ77 + Huffman)
        byte[] lz77HuffmanChunk = huffman.Compress(lz77Chunk);

        // 3. ⚡️ Тестуємо чистий Хаффман (обходячи штрафи LZ77)
        byte[] huffmanOnlyChunk = huffman.Compress(exactChunk);

        // --- ВИБИРАЄМО ПЕРЕМОЖЦЯ ---
        byte[] finalChunk = exactChunk;
        byte chunkType = 0; // За замовчуванням сирі дані
        int minLength = exactChunk.Length;

        // Чи переміг LZ77?
        if (lz77Chunk.Length < minLength)
        {
            finalChunk = lz77Chunk;
            chunkType = 1;
            minLength = lz77Chunk.Length;
        }

        // Чи перемогла зв'язка LZ77 + Huffman?
        if (lz77HuffmanChunk.Length < minLength)
        {
            finalChunk = lz77HuffmanChunk;
            chunkType = 2;
            minLength = lz77HuffmanChunk.Length;
        }

        // Чи переміг чистий Хаффман? (Ось тут ми зловимо твої 2%)
        if (huffmanOnlyChunk.Length < minLength)
        {
            finalChunk = huffmanOnlyChunk;
            chunkType = 3;
            // minLength оновлювати не треба, бо це остання перевірка
        }

        // Збираємо фінальний фрейм
        byte[] fullFrame = new byte[finalChunk.Length + 9];
        fullFrame[0] = chunkType;
        BinaryPrimitives.WriteInt32LittleEndian(fullFrame.AsSpan(1, 4), finalChunk.Length);
        BinaryPrimitives.WriteInt32LittleEndian(fullFrame.AsSpan(5, 4), exactChunk.Length);
        Array.Copy(finalChunk, 0, fullFrame, 9, finalChunk.Length);

        return fullFrame;
    }

}