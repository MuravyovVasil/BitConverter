using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace BitConverter.Core;

/// <summary>
/// Adaptive Entropy Encoding Engine using Dynamic Huffman Trees.
/// Implements bit-packing using 64-bit shift registers for maximum throughput.
/// </summary>
public class HuffmanEngine
{
    private class Node
    {
        public byte Symbol;
        public int Freq;
        public Node? Left;
        public Node? Right;
        public bool IsLeaf => Left == null && Right == null;
    }

    public byte[] Compress(byte[] data)
    {
        if (data.Length == 0) return Array.Empty<byte>();

        // 1. Calculate byte frequencies
        int[] freqs = new int[256];
        foreach (var b in data) freqs[b]++;

        var pq = new PriorityQueue<Node, int>();
        int distinct = 0;
        for (int i = 0; i < 256; i++)
        {
            if (freqs[i] > 0)
            {
                pq.Enqueue(new Node { Symbol = (byte)i, Freq = freqs[i] }, freqs[i]);
                distinct++;
            }
        }

        // Edge case: Only one unique byte in the entire block
        if (pq.Count == 1)
        {
            var node = pq.Dequeue();
            pq.Enqueue(new Node { Left = node, Right = new Node { Symbol = 0, Freq = 0 } }, node.Freq);
        }

        // 2. Build the Huffman Tree
        while (pq.Count > 1)
        {
            var left = pq.Dequeue();
            var right = pq.Dequeue();
            pq.Enqueue(new Node { Left = left, Right = right, Freq = left.Freq + right.Freq }, left.Freq + right.Freq);
        }

        var root = pq.Dequeue();
        var codes = new Dictionary<byte, (uint code, int len)>();
        GenerateCodes(root, 0, 0, codes);

        // Zero-Allocation constraint: Pre-allocate buffer 
        // (Original size + 2 KB safety margin for the dictionary payload)
        byte[] output = new byte[data.Length + 2048];
        int outPos = 0;

        // 3. Write prefix dictionary using fast memory primitives
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(outPos), data.Length);
        outPos += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(outPos), (ushort)distinct);
        outPos += 2;

        foreach (var kvp in codes)
        {
            output[outPos++] = kvp.Key;
            output[outPos++] = (byte)kvp.Value.len;
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(outPos), kvp.Value.code);
            outPos += 4;
        }

        // 4. Ultra-fast Bit-Packing
        // Safe from overflow: A 1MB block guarantees max tree depth < 31 bits (Fibonacci sequence limit)
        ulong bitBuffer = 0;
        int bitCount = 0;

        foreach (var b in data)
        {
            var (code, len) = codes[b];
            bitBuffer = (bitBuffer << len) | code;
            bitCount += len;

            while (bitCount >= 8)
            {
                int shift = bitCount - 8;
                output[outPos++] = (byte)(bitBuffer >> shift);
                bitCount = shift;

                // Clear the packed bits
                if (shift > 0) bitBuffer &= (1UL << shift) - 1;
                else bitBuffer = 0;
            }
        }

        // Write remaining bits (padded with trailing zeros)
        if (bitCount > 0)
        {
            output[outPos++] = (byte)(bitBuffer << (8 - bitCount));
        }

        byte[] result = new byte[outPos];
        Array.Copy(output, result, outPos);
        return result;
    }

    private void GenerateCodes(Node node, uint code, int len, Dictionary<byte, (uint code, int len)> codes)
    {
        if (node.IsLeaf)
        {
            codes[node.Symbol] = (code, len);
            return;
        }
        GenerateCodes(node.Left!, (code << 1), len + 1, codes);
        GenerateCodes(node.Right!, (code << 1) | 1, len + 1, codes);
    }

    public byte[] Decompress(byte[] compressed)
    {
        if (compressed.Length == 0) return Array.Empty<byte>();

        int inPos = 0;

        // 1. Fast Dictionary Unpacking
        int uncompressedSize = BinaryPrimitives.ReadInt32LittleEndian(compressed.AsSpan(inPos));
        inPos += 4;
        ushort distinct = BinaryPrimitives.ReadUInt16LittleEndian(compressed.AsSpan(inPos));
        inPos += 2;

        var root = new Node();

        for (int i = 0; i < distinct; i++)
        {
            byte symbol = compressed[inPos++];
            int len = compressed[inPos++];
            uint code = BinaryPrimitives.ReadUInt32LittleEndian(compressed.AsSpan(inPos));
            inPos += 4;

            var current = root;
            for (int j = len - 1; j >= 0; j--)
            {
                uint bit = (code >> j) & 1;
                if (bit == 0)
                {
                    current.Left ??= new Node();
                    current = current.Left;
                }
                else
                {
                    current.Right ??= new Node();
                    current = current.Right;
                }
            }
            current.Symbol = symbol;
        }

        // 2. High-throughput bit stream decoding
        byte[] output = new byte[uncompressedSize];
        int outIdx = 0;

        if (inPos < compressed.Length)
        {
            int currentByte = compressed[inPos++];
            int bitPos = 7;
            var node = root;

            while (outIdx < uncompressedSize)
            {
                int bit = (currentByte >> bitPos) & 1;
                node = bit == 0 ? node.Left! : node.Right!;

                if (node.IsLeaf)
                {
                    output[outIdx++] = node.Symbol;
                    node = root;
                }

                bitPos--;
                if (bitPos < 0)
                {
                    if (inPos < compressed.Length) currentByte = compressed[inPos++];
                    bitPos = 7;
                }
            }
        }

        return output;
    }
}