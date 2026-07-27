namespace horof.Models;

public class HexCell
{
    public int Row { get; init; }
    public int Col { get; init; }
    public int Index { get; init; }
    public char Letter { get; set; }
    public Team Owner { get; set; }
}
