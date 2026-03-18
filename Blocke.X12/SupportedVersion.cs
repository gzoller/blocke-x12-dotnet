using Blocke.X12.Models;

namespace Blocke.X12;

public static class SupportedVersion
{
    public static bool TryFromParsed(X12Version version, out SupportedX12Version supportedVersion, out string? error)
    {
        try
        {
            supportedVersion = SupportedX12VersionExtensions.FromMajor(version.Major);
            error = null;
            return true;
        }
        catch (ArgumentException)
        {
            supportedVersion = default;
            error = $"Unsupported X12 version: {version.Pretty()}";
            return false;
        }
    }
}
