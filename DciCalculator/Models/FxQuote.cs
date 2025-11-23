namespace DciCalculator.Models;


public readonly record struct FxQuote(
    decimal Bid,
    decimal Ask
)
{
    public decimal Mid => (Bid + Ask) / 2m;
}

