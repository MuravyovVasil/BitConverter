namespace BitConverter.Core;

/// <summary>
/// Configuration profiles for the MBIT compression engine.
/// Controls memory allocation, concurrency, and algorithmic aggression.
/// </summary>
public class CompressionOptions
{
    // Block size read at once (affects RAM allocation footprint).
    public int BlockSize { get; set; } = 1048576; // Default: 1 MB

    // Maximum number of concurrent processing threads.
    public int MaxConcurrency { get; set; } = System.Environment.ProcessorCount;

    // --- LZ77 Engine Settings ---

    // History window size (maximum distance for match search).
    // Note: for our 2-byte token format, the max value is 4095 (12 bits).
    public int LzWindowSize { get; set; } = 4095;

    // Maximum match length (for a 4-bit length code, max is 18: 15 + 3 min match).
    public int LzMaxMatchLength { get; set; } = 18;

    // Search depth in the hash chain (affects speed vs. compression ratio).
    // Higher depth = better compression, but increased CPU cycles.
    public int LzSearchDepth { get; set; } = 100;

    // --- Performance Profiles ---

    public static CompressionOptions Fast() => new()
    {
        BlockSize = 524288,   // 512 KB (Lower memory footprint)
        LzSearchDepth = 10    // Very fast search, baseline compression
    };

    public static CompressionOptions Standard() => new()
    {
        BlockSize = 1048576,  // 1 MB
        LzSearchDepth = 100   // Optimal balance between speed and ratio
    };

    public static CompressionOptions Ultra() => new()
    {
        BlockSize = 4194304,  // 4 MB (Larger blocks optimize Huffman, but consume more RAM)
        LzSearchDepth = 4096  // Deep hash chain search (Slower, max compression)
    };
}