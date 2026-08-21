using System;
using System.Collections.Generic;

namespace EntropyTag.Domain
{
    public interface IReadOnlyTerritoryField
    {
        int Width { get; }

        int Height { get; }

        int CellCount { get; }

        TerritoryCell GetCell(TerritoryCoordinate coordinate);
    }

    public interface ITerritoryField : IReadOnlyTerritoryField
    {
        StampResult ApplyStamp(
            IEnumerable<TerritoryCoordinate> coordinates,
            ElementId element,
            TeamId applyingTeam);

        void Reset();
    }

    public readonly struct StampResult
    {
        public StampResult(int attemptedCells, int changedCells, int bankAward)
        {
            AttemptedCells = attemptedCells;
            ChangedCells = changedCells;
            BankAward = bankAward;
        }

        public int AttemptedCells { get; }

        public int ChangedCells { get; }

        public int BankAward { get; }
    }

    public sealed class TerritoryField : ITerritoryField
    {
        private readonly TerritoryCell[] cells;
        private readonly TerritoryReactionResolver resolver;

        public TerritoryField(int width, int height, TerritoryReactionResolver reactionResolver)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Territory width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Territory height must be greater than zero.");
            }

            Width = width;
            Height = height;
            resolver = reactionResolver ?? throw new ArgumentNullException(nameof(reactionResolver));
            cells = new TerritoryCell[checked(width * height)];
            Reset();
        }

        public int Width { get; }

        public int Height { get; }

        public int CellCount => cells.Length;

        public TerritoryCell GetCell(TerritoryCoordinate coordinate)
        {
            return cells[GetIndex(coordinate)];
        }

        public StampResult ApplyStamp(
            IEnumerable<TerritoryCoordinate> coordinates,
            ElementId element,
            TeamId applyingTeam)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(nameof(coordinates));
            }

            var uniqueCoordinates = new HashSet<TerritoryCoordinate>();
            int attempted = 0;
            int changed = 0;
            int bankAward = 0;

            foreach (TerritoryCoordinate coordinate in coordinates)
            {
                attempted++;

                if (!uniqueCoordinates.Add(coordinate))
                {
                    continue;
                }

                int index = GetIndex(coordinate);
                TerritoryCell existing = cells[index];
                TerritoryResolution resolution = resolver.Resolve(existing, element, applyingTeam);

                if (!resolution.Applied)
                {
                    continue;
                }

                if (!resolution.Cell.Equals(existing))
                {
                    changed++;
                    cells[index] = resolution.Cell;
                }

                bankAward += resolution.BankAward;
            }

            return new StampResult(attempted, changed, bankAward);
        }

        public void Reset()
        {
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = TerritoryCell.Neutral;
            }
        }

        private int GetIndex(TerritoryCoordinate coordinate)
        {
            if (coordinate.X < 0 ||
                coordinate.X >= Width ||
                coordinate.Y < 0 ||
                coordinate.Y >= Height)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    $"Territory coordinate ({coordinate.X}, {coordinate.Y}) is outside {Width}x{Height}.");
            }

            return coordinate.Y * Width + coordinate.X;
        }
    }
}
