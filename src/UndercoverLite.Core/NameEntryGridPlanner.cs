namespace UndercoverLite.Core;

public enum GridAction { Up, Down, Left, Right, Accept, Done }

/// <summary>
/// Experimental navigation plan for the Undercover PS2 ENTER NAME screenshot.
/// Assumes cursor starts on A, a rectangular grid with no wrap, and that
/// D-pad inputs move one cell. Does NOT send keystrokes to the emulator.
/// </summary>
public static class NameEntryGridPlanner
{
    private static readonly string[] Rows = ["ABCDEFGHIJKLM", "NOPQRSTUVWXYZ", "1234567890"];

    public static IReadOnlyList<GridAction> Plan(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enter a name using A-Z and 0-9.", nameof(name));
        name = name.Trim().ToUpperInvariant();
        if (name.Length > 24)
            throw new ArgumentException("Prototype supports at most 24 characters; the game's actual limit is unverified.", nameof(name));

        var actions = new List<GridAction>();
        var row = 0;
        var column = 0;
        foreach (char c in name)
        {
            var targetRow = Array.FindIndex(Rows, s => s.Contains(c));
            if (targetRow == -1)
                throw new ArgumentException($"The captured on-screen grid has no confirmed tile for '{c}'.", nameof(name));
            var targetColumn = Rows[targetRow].IndexOf(c);

            // Move to an existing column BEFORE entering the short digits row;
            // leave the digits row BEFORE moving to an upper row's final columns.
            if (targetRow == 2 && column > 9)
                MoveHorizontal(actions, ref column, targetColumn);
            if (row == 2 && targetColumn > 9)
                MoveVertical(actions, ref row, targetRow);

            MoveVertical(actions, ref row, targetRow);
            MoveHorizontal(actions, ref column, targetColumn);
            actions.Add(GridAction.Accept);
        }
        actions.Add(GridAction.Done);
        return actions;
    }

    private static void MoveHorizontal(List<GridAction> steps, ref int column, int target)
    {
        while (column < target) { steps.Add(GridAction.Right); column++; }
        while (column > target) { steps.Add(GridAction.Left); column--; }
    }

    private static void MoveVertical(List<GridAction> steps, ref int row, int target)
    {
        while (row < target) { steps.Add(GridAction.Down); row++; }
        while (row > target) { steps.Add(GridAction.Up); row--; }
    }
}