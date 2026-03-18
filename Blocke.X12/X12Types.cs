namespace Blocke.X12.Models;

public static class X12Types
{
    public readonly record struct X12_ST(string Value)
    {
        public override string ToString() => Value;
    }
}
