using System;

namespace EntropyTag.Domain
{
    public readonly struct TeamId : IEquatable<TeamId>
    {
        public TeamId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Team ID must be greater than zero.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool Equals(TeamId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TeamId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(TeamId left, TeamId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TeamId left, TeamId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Player ID must be greater than zero.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool Equals(PlayerId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(PlayerId left, PlayerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerId left, PlayerId right)
        {
            return !left.Equals(right);
        }
    }
}
