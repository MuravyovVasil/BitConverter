namespace BitConverter.Core;

public class ReplacementRule
{
    public byte RuleId { get; }

    // ⚡️ ТЕПЕР ТУТ БАЙТИ, А НЕ БІТИ
    public byte[] PatternBytes { get; }
    public int PatternLength { get; } // Довжина у БАЙТАХ

    public byte[] ReplacementBits { get; }
    public int ReplacementLength { get; } // Довжина у БІТАХ

    public ReplacementRule(byte ruleId, byte[] patternBytes, byte[] replacementBits, int replacementLength)
    {
        RuleId = ruleId;
        PatternBytes = patternBytes;
        PatternLength = patternBytes.Length;
        ReplacementBits = replacementBits;
        ReplacementLength = replacementLength;
    }
}

public readonly struct GlobalChange
{
    public readonly byte RuleId;
    public readonly long DstStartBit;
    public readonly long SrcStartBit;

    public GlobalChange(byte ruleId, long dstStart, long srcStart)
    {
        RuleId = ruleId;
        DstStartBit = dstStart;
        SrcStartBit = srcStart;
    }
}

public readonly struct PassLog
{
    public readonly long BitOffset;
    public readonly byte OriginalBit;

    public PassLog(long bitOffset, byte originalBit)
    {
        BitOffset = bitOffset;
        OriginalBit = originalBit;
    }
}