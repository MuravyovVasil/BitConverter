using System;

namespace BitConverter.Core;

public ref struct BitStreamReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _bitPosition;
    private readonly int _maxBits;

    public BitStreamReader(ReadOnlySpan<byte> buffer, int validBytes)
    {
        _buffer = buffer;
        _bitPosition = 0;
        _maxBits = validBytes << 3;
    }

    public bool TryReadBit(out byte bit)
    {
        if (_bitPosition >= _maxBits)
        {
            bit = 0;
            return false;
        }

        int byteIndex = _bitPosition >> 3;
        int bitIndex = 7 - (_bitPosition & 7);

        bit = (byte)((_buffer[byteIndex] >> bitIndex) & 1);
        _bitPosition++;

        return true;
    }
}

public ref struct BitStreamWriter
{
    private readonly Span<byte> _buffer;
    private int _bitPosition;

    // Повертає загальну кількість бітів (цілих і нецілих байтів)
    public int CurrentBitPosition => _bitPosition;

    public BitStreamWriter(Span<byte> buffer, byte pendingByte, int pendingBits)
    {
        _buffer = buffer;
        _buffer.Clear();
        _bitPosition = pendingBits;

        // Відновлюємо неповний байт з попереднього чанку
        if (pendingBits > 0)
        {
            _buffer[0] = pendingByte;
        }
    }

    public void WriteBit(byte bit)
    {
        if ((_bitPosition >> 3) >= _buffer.Length) return;

        int byteIndex = _bitPosition >> 3;
        int bitIndex = 7 - (_bitPosition & 7);

        if (bit == 1)
        {
            _buffer[byteIndex] |= (byte)(1 << bitIndex);
        }

        _bitPosition++;
    }
}