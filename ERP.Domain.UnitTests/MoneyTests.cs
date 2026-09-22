using Xunit;
using ERP.Domain.Common.ValueObjects;

namespace ERP.Domain.UnitTests;

/// <summary>
/// Unit tests for the Money value object
/// </summary>
public class MoneyTests
{
    [Fact]
    public void Of_WithNegativeAmount_ShouldThrowArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => Money.Of(-1m));
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Fact]
    public void Of_WithZero_ShouldSucceed()
    {
        var money = Money.Of(0m);
        Assert.Equal(0m, money.Amount);
    }

    [Fact]
    public void Zero_ShouldHaveAmountZero()
    {
        Assert.Equal(0m, Money.Zero.Amount);
    }

    [Fact]
    public void ImplicitConversion_ToDecimal_ShouldReturnAmount()
    {
        var money = Money.Of(42.5m);
        decimal amount = money;
        Assert.Equal(42.5m, amount);
    }

    [Fact]
    public void Rounded_ShouldRoundToTwoDecimalPlaces_UsingBankersRounding()
    {
        // 2.345 rounds to 2.34 under MidpointRounding.ToEven (decimal.Round's
        // default, and what every pre-existing Math.Round(x, 2) call in this
        // codebase already relies on) — Rounded() must match that, not
        // silently switch to AwayFromZero.
        var money = Money.Of(2.345m);
        Assert.Equal(2.34m, money.Rounded().Amount);
    }

    [Fact]
    public void Addition_ShouldSumAmounts()
    {
        var result = Money.Of(10m) + Money.Of(5m);
        Assert.Equal(15m, result.Amount);
    }

    [Fact]
    public void MultiplyByDecimal_ShouldScaleAmount()
    {
        var result = Money.Of(10m) * 3m;
        Assert.Equal(30m, result.Amount);
    }

    [Fact]
    public void Equality_ShouldBeStructural()
    {
        Assert.Equal(Money.Of(10m), Money.Of(10m));
        Assert.NotEqual(Money.Of(10m), Money.Of(11m));
    }

    [Fact]
    public void ToString_ShouldFormatWithTwoDecimals()
    {
        Assert.Equal("10.00", Money.Of(10m).ToString());
    }

    [Fact]
    public void FromPersistedValue_WithNegativeAmount_ShouldNotThrow()
    {
        // A row written before this invariant existed must still be readable
        // (see the EF Core value converters in ERPDbContext) instead of
        // throwing and breaking the whole query.
        var money = Money.FromPersistedValue(-5m);
        Assert.Equal(-5m, money.Amount);
    }
}
