namespace ERP.Domain.Common.ValueObjects;

/// <summary>
/// A monetary amount. Guarantees the "can't be negative" invariant that was
/// previously re-checked by hand at every call site that touched a price/total
/// (e.g. SalesOrderLine.Create's "Unit price cannot be negative" guard).
///
/// Deliberately does not perform rounding on construction — call sites that
/// need Math.Round(x, 2) semantics (matching existing calculations) still do
/// so explicitly via <see cref="Rounded"/>, so introducing this type doesn't
/// silently change any previously-computed amount.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static readonly Money Zero = new(0m);

    public static Money Of(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative", nameof(amount));

        return new Money(amount);
    }

    /// <summary>
    /// Reconstructs a Money from a value that was already persisted, without
    /// re-validating it. The non-negative invariant is enforced on write (via
    /// <see cref="Of"/>), not on read — a row written before this invariant
    /// existed (or corrupted some other way) must still be readable instead of
    /// throwing and taking down the whole query. Only ever call this when
    /// hydrating from storage (see the EF Core value converters in
    /// ERPDbContext); business logic should always go through <see cref="Of"/>.
    /// </summary>
    public static Money FromPersistedValue(decimal amount) => new(amount);

    public Money Rounded() => new(Math.Round(Amount, 2));

    public static implicit operator decimal(Money money) => money.Amount;

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor);

    public override string ToString() => Amount.ToString("N2");
}
