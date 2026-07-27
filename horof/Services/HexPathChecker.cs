using horof.Models;

namespace horof.Services;

public static class HexPathChecker
{
    public static bool HasWinningPath(IReadOnlyList<HexCell> cells, Team team)
    {
        if (team == Team.Orange)
            return HasVerticalPath(cells, team);

        if (team == Team.Green)
            return HasHorizontalPath(cells, team);

        return false;
    }

    private static bool HasVerticalPath(IReadOnlyList<HexCell> cells, Team team)
    {
        var topIndices = Enumerable.Range(0, BoardLayout.Cols).ToList();
        var bottomIndices = Enumerable.Range((BoardLayout.Rows - 1) * BoardLayout.Cols, BoardLayout.Cols).ToList();
        var owned = cells.Where(c => c.Owner == team).Select(c => c.Index).ToHashSet();
        if (owned.Count == 0)
            return false;

        foreach (var start in topIndices.Where(owned.Contains))
        {
            var visited = new HashSet<int>();
            if (Dfs(start, owned, visited, idx => bottomIndices.Contains(idx)))
                return true;
        }

        return false;
    }

    private static bool HasHorizontalPath(IReadOnlyList<HexCell> cells, Team team)
    {
        var owned = cells.Where(c => c.Owner == team).Select(c => c.Index).ToHashSet();
        if (owned.Count == 0)
            return false;

        foreach (var start in owned.Where(i => i % BoardLayout.Cols == 0))
        {
            var visited = new HashSet<int>();
            if (Dfs(start, owned, visited, idx => idx % BoardLayout.Cols == BoardLayout.Cols - 1))
                return true;
        }

        return false;
    }

    private static bool Dfs(int index, HashSet<int> owned, HashSet<int> visited, Func<int, bool> isGoal)
    {
        if (!owned.Contains(index) || !visited.Add(index))
            return false;

        if (isGoal(index))
            return true;

        foreach (var neighbor in BoardLayout.GetNeighborIndices(index))
        {
            if (Dfs(neighbor, owned, visited, isGoal))
                return true;
        }

        return false;
    }
}
