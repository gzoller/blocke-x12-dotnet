namespace Blocke.X12.Models;

public sealed record X12Version(string Major, string? Implementation)
{
    public string Pretty() => Implementation is null ? Major : $"{Major}/{Implementation}";

    public static bool TryParse(string code, out X12Version? version)
    {
        var trimmed = code.Trim();
        if (trimmed.Length == 4 && trimmed.All(char.IsDigit))
        {
            version = new X12Version($"00{trimmed}", null);
            return true;
        }

        var xIndex = trimmed.IndexOf('X', StringComparison.Ordinal);
        if (xIndex == 6 && trimmed.Length > 7 && trimmed[..6].All(char.IsDigit))
        {
            version = new X12Version(trimmed[..6], trimmed[(xIndex + 1)..]);
            return true;
        }

        if (trimmed.Length == 6 && trimmed.All(char.IsDigit))
        {
            version = new X12Version(trimmed, null);
            return true;
        }

        version = null;
        return false;
    }
}
