
namespace Sanabi.Framework.Misc;

/// <summary>
///     Implementation of PCG32; a (pseudo-)random
///         number generator.
/// </summary>
/// <remarks>
///     We make our own implementation because
///         we can't rely on any .NET implementation
///         to always stay the same.
///
///     This should always be provided with an explicit
///         seed as it will not generate one if given
///         none.
/// </remarks>
public struct Pcg32
{
    private ulong _state;
    private readonly ulong _inc;

    public Pcg32(ulong seed, ulong seq = 1)
    {
        _state = 0;
        _inc = (seq << 1) | 1;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    public uint NextUInt()
    {
        ulong oldstate = _state;
        _state = oldstate * 6364136223846793005UL + _inc;
        uint xorshifted = (uint)(((oldstate >> 18) ^ oldstate) >> 27);
        int rot = (int)(oldstate >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    public int NextInt() => (int)(NextUInt() & 0x7FFFFFFF);

    public uint NextUInt(uint max) => NextUInt() % max;

    public double NextDouble() =>
        (NextUInt() >> 11) * (1.0 / (1ul << 53));

    public void NextBytes(Span<byte> dest)
    {
        int i = 0;
        while (i < dest.Length)
        {
            uint r = NextUInt();
            int n = Math.Min(4, dest.Length - i);
            for (int j = 0; j < n; j++)
                dest[i++] = (byte)(r >> (j * 8));
        }
    }

    public Guid NextGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        NextBytes(bytes);

        // Set version (4)
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);

        // Set variant (RFC 4122)
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
