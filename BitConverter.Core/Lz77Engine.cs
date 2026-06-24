using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace BitConverter.Core;

/// <summary>
/// High-performance LZ77 compression engine using Fibonacci hashing 
/// and 64-bit vectorized memory scanning for O(1) match lookups.
/// </summary>
public class Lz77Engine
{
    private const int MinMatchLength = 3;

    // Golden ratio multiplier for fast Fibonacci hashing to distribute 32-bit values.
    private const uint HashMultiplier = 0x9E3779B1;

    private readonly int _windowSize;
    private readonly int _maxMatchLength;
    private readonly int _searchDepth;

    public Lz77Engine(CompressionOptions options)
    {
        _windowSize = options.LzWindowSize;
        _maxMatchLength = options.LzMaxMatchLength;
        _searchDepth = options.LzSearchDepth;
    }

    public byte[] Compress(byte[] data)
    {
        if (data.Length < 4) return data;

        // Allocate buffer: original size + space for flags + safety margin
        byte[] output = new byte[data.Length + (data.Length / 8) + 16];
        int outPos = 0;

        int[] head = new int[65536];
        Array.Fill(head, -1);
        int[] chain = new int[data.Length];

        byte flagByte = 0;
        int flagBitPos = 0;

        byte[] tokenBuffer = new byte[24];
        int tokenPos = 0;

        int pos = 0;
        int limit = data.Length - 4;

        while (pos < data.Length)
        {
            int matchDistance = 0;
            int matchLength = 0;

            if (pos <= limit)
            {
                // Calculate hash for the current 4-byte sequence
                uint hash = (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos)) * HashMultiplier) >> 16;
                int currentChainNode = head[hash];
                int bound = Math.Max(0, pos - _windowSize);

                int currentSearchDepth = _searchDepth;

                // Traverse the hash chain to find the longest match within the window
                while (currentChainNode >= bound && currentSearchDepth-- > 0)
                {
                    int len = GetFastMatchLength(data, currentChainNode, pos);
                    if (len > matchLength)
                    {
                        matchLength = len;
                        matchDistance = pos - currentChainNode;
                        if (matchLength == _maxMatchLength) break; // Reached maximum allowed match length
                    }
                    currentChainNode = chain[currentChainNode];
                }
            }

            if (matchLength >= MinMatchLength)
            {
                // Mark bit as 1 (Match Pointer)
                flagByte |= (byte)(1 << flagBitPos);

                // Token structure: [Distance: 12 bits] | [Length Code: 4 bits]
                int lengthCode = matchLength - MinMatchLength;
                ushort token = (ushort)((matchDistance << 4) | lengthCode);

                tokenBuffer[tokenPos++] = (byte)(token & 0xFF);
                tokenBuffer[tokenPos++] = (byte)(token >> 8);

                // Update hash chains for all matched bytes to improve future lookups
                for (int i = 0; i < matchLength; i++)
                {
                    if (pos + i <= limit)
                    {
                        uint h = (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + i)) * HashMultiplier) >> 16;
                        chain[pos + i] = head[h];
                        head[h] = pos + i;
                    }
                }
                pos += matchLength;
            }
            else
            {
                // Mark bit as 0 (Literal)
                tokenBuffer[tokenPos++] = data[pos];

                if (pos <= limit)
                {
                    uint h = (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos)) * HashMultiplier) >> 16;
                    chain[pos] = head[h];
                    head[h] = pos;
                }
                pos++;
            }

            flagBitPos++;

            // Flush the group of 8 tokens
            if (flagBitPos == 8)
            {
                output[outPos++] = flagByte;
                Array.Copy(tokenBuffer, 0, output, outPos, tokenPos);
                outPos += tokenPos;

                flagByte = 0;
                flagBitPos = 0;
                tokenPos = 0;
            }
        }

        // Flush remaining tokens
        if (flagBitPos > 0)
        {
            output[outPos++] = flagByte;
            Array.Copy(tokenBuffer, 0, output, outPos, tokenPos);
            outPos += tokenPos;
        }

        byte[] result = new byte[outPos];
        Array.Copy(output, result, outPos);
        return result;
    }

    /// <summary>
    /// Compares two byte sequences 8 bytes at a time using 64-bit registers.
    /// This vectorized approach significantly outperforms byte-by-byte comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetFastMatchLength(byte[] data, int pos1, int pos2)
    {
        int len = 0;
        int maxSafeLen = Math.Min(_maxMatchLength, data.Length - pos2);

        // Fast 64-bit comparison path
        if (len + 8 <= maxSafeLen && pos1 + 8 <= data.Length)
        {
            ulong a = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos1 + len));
            ulong b = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos2 + len));

            if (a == b)
            {
                len += 8;
                if (len + 8 <= maxSafeLen && pos1 + 16 <= data.Length)
                {
                    a = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos1 + len));
                    b = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos2 + len));
                    if (a == b) len += 8;
                }
            }
        }

        // Fallback for remaining bytes
        while (len < maxSafeLen && data[pos1 + len] == data[pos2 + len])
        {
            len++;
        }

        return len;
    }

    public byte[] Decompress(byte[] compressed)
    {
        if (compressed.Length == 0) return Array.Empty<byte>();

        // Start with a reasonable buffer and expand if necessary
        byte[] output = new byte[Math.Max(1048576, _windowSize * 2)];
        int outPos = 0;
        int pos = 0;

        while (pos < compressed.Length)
        {
            byte flagByte = compressed[pos++];

            for (int i = 0; i < 8 && pos < compressed.Length; i++)
            {
                bool isMatch = ((flagByte >> i) & 1) == 1;

                if (isMatch)
                {
                    if (pos + 1 >= compressed.Length) break;

                    ushort token = (ushort)(compressed[pos] | (compressed[pos + 1] << 8));
                    pos += 2;

                    int matchDistance = token >> 4;
                    int matchLength = (token & 0x0F) + MinMatchLength;

                    int startPos = outPos - matchDistance;

                    for (int j = 0; j < matchLength; j++)
                    {
                        if (outPos >= output.Length) Array.Resize(ref output, output.Length * 2);
                        output[outPos++] = output[startPos + j];
                    }
                }
                else
                {
                    if (outPos >= output.Length) Array.Resize(ref output, output.Length * 2);
                    output[outPos++] = compressed[pos++];
                }
            }
        }

        byte[] result = new byte[outPos];
        Array.Copy(output, result, outPos);
        return result;
    }
}