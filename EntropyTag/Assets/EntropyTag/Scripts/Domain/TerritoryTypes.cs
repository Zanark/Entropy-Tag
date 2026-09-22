using System;

namespace EntropyTag.Domain
{
    public enum TerritoryState
    {
        Neutral = 0,
        Ice = 1,
        Fire = 2,
        Mist = 3
    }

    public readonly struct TerritoryOwner : IEquatable<TerritoryOwner>
    {
        private TerritoryOwner(TeamId teamId, bool hasTeam)
        {
            TeamId = teamId;
            HasTeam = hasTeam;
        }

        public static TerritoryOwner None => default;

        public TeamId TeamId { get; }

        public bool HasTeam { get; }

        public static TerritoryOwner ForTeam(TeamId teamId)
        {
            return new TerritoryOwner(teamId, true);
        }

        public bool Equals(TerritoryOwner other)
        {
            return HasTeam == other.HasTeam && (!HasTeam || TeamId == other.TeamId);
        }

        public override bool Equals(object obj)
        {
            return obj is TerritoryOwner other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HasTeam ? TeamId.GetHashCode() : 0;
        }

        public static bool operator ==(TerritoryOwner left, TerritoryOwner right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TerritoryOwner left, TerritoryOwner right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct TerritoryCoordinate : IEquatable<TerritoryCoordinate>
    {
        public TerritoryCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(TerritoryCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is TerritoryCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }
    }

    public readonly struct TerritoryCell : IEquatable<TerritoryCell>
    {
        public TerritoryCell(
            TerritoryState state,
            TerritoryOwner owner,
            TerritoryOwner previousOwner)
        {
            if ((state == TerritoryState.Neutral || state == TerritoryState.Mist) && owner.HasTeam)
            {
                throw new ArgumentException("Neutral and Mist cells cannot have a current owner.", nameof(owner));
            }

            if ((state == TerritoryState.Ice || state == TerritoryState.Fire) && !owner.HasTeam)
            {
                throw new ArgumentException("Ice and Fire cells require a current owner.", nameof(owner));
            }

            State = state;
            Owner = owner;
            PreviousOwner = previousOwner;
        }

        public static TerritoryCell Neutral =>
            new TerritoryCell(TerritoryState.Neutral, TerritoryOwner.None, TerritoryOwner.None);

        public TerritoryState State { get; }

        public TerritoryOwner Owner { get; }

        public TerritoryOwner PreviousOwner { get; }

        public bool Equals(TerritoryCell other)
        {
            return State == other.State &&
                   Owner == other.Owner &&
                   PreviousOwner == other.PreviousOwner;
        }

        public override bool Equals(object obj)
        {
            return obj is TerritoryCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)State;
                hash = (hash * 397) ^ Owner.GetHashCode();
                hash = (hash * 397) ^ PreviousOwner.GetHashCode();
                return hash;
            }
        }
    }
}
