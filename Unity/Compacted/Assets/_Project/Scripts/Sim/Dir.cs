namespace Compacted.Sim
{
    /// <summary>Grid direction. Values match the web prototype: 0 = N, 1 = E, 2 = S, 3 = W.</summary>
    public enum Dir : byte
    {
        N = 0,
        E = 1,
        S = 2,
        W = 3,
    }

    /// <summary>Integer grid position or offset. +x is east, +y is south (screen-down), as in the prototype.</summary>
    public readonly struct Cell : System.IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y) { X = x; Y = y; }

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(Cell a, Cell b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Cell a, Cell b) => !(a == b);
        public bool Equals(Cell other) => this == other;
        public override bool Equals(object obj) => obj is Cell c && this == c;
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X}, {Y})";
    }

    public static class DirExt
    {
        static readonly Cell[] Offsets = { new Cell(0, -1), new Cell(1, 0), new Cell(0, 1), new Cell(-1, 0) };

        /// <summary>The direction pointing the other way.</summary>
        public static Dir Opposite(this Dir d) => (Dir)(((int)d + 2) & 3);

        /// <summary>Rotate clockwise by a number of quarter turns (negative = counter-clockwise).</summary>
        public static Dir Rotate(this Dir d, int quarterTurns) => (Dir)((((int)d + quarterTurns) % 4 + 4) % 4);

        /// <summary>The one-cell step in this direction.</summary>
        public static Cell Offset(this Dir d) => Offsets[(int)d];

        /// <summary>The cell next to <paramref name="c"/> in this direction.</summary>
        public static Cell Step(this Cell c, Dir d) => c + d.Offset();
    }
}
