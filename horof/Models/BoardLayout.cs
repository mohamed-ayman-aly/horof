namespace horof.Models;

public static class BoardLayout
{
    public const int Rows = 5;
    public const int Cols = 5;

    private static readonly char[,] DefaultLetters =
    {
        { 'د', 'س', 'ه', 'ح', 'خ' },
        { 'و', 'غ', 'ل', 'ر', 'ت' },
        { 'ظ', 'ك', 'ص', 'ض', 'ب' },
        { 'ع', 'ط', 'ي', 'ا', 'ن' },
        { 'م', 'ذ', 'ز', 'ف', 'ج' },
    };

    public static IReadOnlyList<HexCell> CreateCells(int? seed = null)
    {
        var letters = (char[,])DefaultLetters.Clone();
        if (seed is int s)
            ShuffleLetters(letters, s);

        var cells = new List<HexCell>(Rows * Cols);
        var index = 0;
        for (var row = 0; row < Rows; row++)
        for (var col = 0; col < Cols; col++)
        {
            cells.Add(new HexCell
            {
                Row = row,
                Col = col,
                Index = index++,
                Letter = letters[row, col],
                Owner = Team.None
            });
        }

        return cells;
    }

    public static IReadOnlyList<int> GetNeighborIndices(int index)
    {
        var cell = IndexToRowCol(index);
        var neighbors = new List<int>();
        var directions = cell.row % 2 == 0
            ? new[] { (-1, 0), (-1, -1), (0, -1), (0, 1), (1, -1), (1, 0) }
            : new[] { (-1, 0), (-1, 1), (0, -1), (0, 1), (1, 0), (1, 1) };

        foreach (var (dr, dc) in directions)
        {
            var nr = cell.row + dr;
            var nc = cell.col + dc;
            if (nr is >= 0 and < Rows && nc is >= 0 and < Cols)
                neighbors.Add(RowColToIndex(nr, nc));
        }

        return neighbors;
    }

    public static int RowColToIndex(int row, int col) => row * Cols + col;

    public static (int row, int col) IndexToRowCol(int index) => (index / Cols, index % Cols);

    private static void ShuffleLetters(char[,] letters, int seed)
    {
        var rng = new Random(seed);
        var flat = new char[Rows * Cols];
        var i = 0;
        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Cols; c++)
            flat[i++] = letters[r, c];

        for (i = flat.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (flat[i], flat[j]) = (flat[j], flat[i]);
        }

        i = 0;
        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Cols; c++)
            letters[r, c] = flat[i++];
    }
}
