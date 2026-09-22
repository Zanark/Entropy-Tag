using System;

namespace EntropyTag.Domain
{
    public interface IRandomSource
    {
        int NextInt(int maximumExclusive);

        double NextUnit();
    }

    public sealed class SeededRandomSource : IRandomSource
    {
        private uint state;

        public SeededRandomSource(int seed)
        {
            state = unchecked((uint)seed);

            if (state == 0u)
            {
                state = 0x6D2B79F5u;
            }
        }

        public int NextInt(int maximumExclusive)
        {
            if (maximumExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExclusive),
                    "Maximum must be greater than zero.");
            }

            return (int)(NextUInt() % (uint)maximumExclusive);
        }

        public double NextUnit()
        {
            return NextUInt() / ((double)uint.MaxValue + 1d);
        }

        private uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }
    }
}
