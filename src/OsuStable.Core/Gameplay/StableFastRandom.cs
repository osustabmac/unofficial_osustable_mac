namespace OsuStable.Core.Gameplay;

// Direct port of osu_common.Helpers.FastRandom. Conversion determinism depends
// on both this xorshift sequence and its four-word state being identical.
public sealed class StableFastRandom
{
    private uint bitBuffer;
    private int bitIndex = 32;

    public StableFastRandom(int seed) => Reinitialise(seed);

    public uint W { get; private set; }
    public uint X { get; private set; }
    public uint Y { get; private set; }
    public uint Z { get; private set; }

    public void Reinitialise(int seed)
    {
        X = unchecked((uint)seed);
        Y = 842502087;
        Z = 3579807591;
        W = 273326509;
        bitIndex = 32;
    }

    public uint NextUInt() => advance();
    public int Next() => (int)(0x7fffffff & advance());

    public int Next(int upperBound)
    {
        if (upperBound < 0) throw new ArgumentOutOfRangeException(nameof(upperBound));
        return (int)(4.656612873077393E-10 * (0x7fffffff & advance()) * upperBound);
    }

    public int Next(int lowerBound, int upperBound)
    {
        if (lowerBound > upperBound) throw new ArgumentOutOfRangeException(nameof(upperBound));
        var range = upperBound - lowerBound;
        var value = advance();
        if (range < 0)
            return lowerBound + (int)(2.3283064365386963E-10 * value * ((long)upperBound - lowerBound));
        return lowerBound + (int)(4.656612873077393E-10 * (0x7fffffff & value) * range);
    }

    public double NextDouble() => 4.656612873077393E-10 * (0x7fffffff & advance());

    public void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var offset = 0;
        while (offset < buffer.Length)
        {
            var value = advance();
            for (var shift = 0; shift < 32 && offset < buffer.Length; shift += 8)
                buffer[offset++] = (byte)(value >> shift);
        }
    }

    public bool NextBool()
    {
        if (bitIndex == 32)
        {
            bitBuffer = advance();
            bitIndex = 1;
            return (bitBuffer & 1) == 1;
        }
        bitIndex++;
        return ((bitBuffer >>= 1) & 1) == 1;
    }

    private uint advance()
    {
        var temporary = X ^ (X << 11);
        X = Y;
        Y = Z;
        Z = W;
        W = W ^ (W >> 19) ^ (temporary ^ (temporary >> 8));
        return W;
    }
}
