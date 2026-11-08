using Servora.Application.Catalog;
using Servora.Domain.Catalog;

namespace Servora.Tests.Unit.Catalog;

/// <summary>Tests of the server-side price calculation of configured catalog items.</summary>
public sealed class CatalogPriceCalculatorTests
{
    private static readonly CatalogPricing s_laptop = new("Business laptop", 39m, CatalogBillingType.Monthly, 36, 0m);

    private static readonly IReadOnlyList<CatalogVariableDefinition> s_variables =
    [
        Define(new CatalogVariable { Name = CatalogPriceCalculator.CostCenterVariable, Label = "Cost centre", Type = CatalogVariableType.Choice, Options = "MKT-2040 · Marketing\nIT-4000 · IT", Order = 10 }),
        Define(new CatalogVariable { Name = "memory", Label = "Memory", Type = CatalogVariableType.Choice, Options = "16 GB RAM | 0\n32 GB RAM | +8.00", DefaultValue = "16 GB RAM", Order = 20 }),
        Define(new CatalogVariable { Name = "os", Label = "OS", Type = CatalogVariableType.Choice, Options = "Windows 11 Enterprise\nUbuntu", Order = 30 }),
        Define(new CatalogVariable { Name = "dock", Label = "USB-C docking station", Type = CatalogVariableType.Boolean, PriceDelta = 5m, Order = 40 }),
        Define(new CatalogVariable { Name = "return_device", Label = "Return old device", Type = CatalogVariableType.Boolean, Order = 50 }),
    ];

    [Fact]
    public void Defaults_UseTheBasePrice()
    {
        var quote = CatalogPriceCalculator.Calculate(s_laptop, s_variables, new Dictionary<string, string?>());
        Assert.Equal(39m, quote.UnitPrice);
        Assert.Equal(39m, quote.Total);
        Assert.True(quote.IsMonthly);
        Assert.Equal(36, quote.TermMonths);
        Assert.Single(quote.Lines);
        Assert.Equal(["16 GB RAM"], quote.SelectedOptions);
    }

    [Fact]
    public void Options_AddTheirPriceImpacts_LikeTheReference()
    {
        var quote = CatalogPriceCalculator.Calculate(s_laptop, s_variables, new Dictionary<string, string?>
        {
            [CatalogPriceCalculator.CostCenterVariable] = "MKT-2040 · Marketing",
            ["memory"] = "32 GB RAM",
            ["os"] = "Windows 11 Enterprise",
            ["dock"] = "true",
            ["return_device"] = "true",
        });

        Assert.Equal(52m, quote.UnitPrice);
        Assert.Equal([39m, 8m, 5m], quote.Lines.Select(l => l.Amount));
        Assert.Equal(["Business laptop", "32 GB RAM", "USB-C docking station"], quote.Lines.Select(l => l.Label));
        Assert.Equal(["32 GB RAM", "Windows 11 Enterprise", "USB-C docking station"], quote.SelectedOptions);
    }

    [Fact]
    public void Quantity_MultipliesTotals_AndOneTimeCostsAreSeparate()
    {
        var pricing = s_laptop with { OneTimePrice = 49m };
        var quote = CatalogPriceCalculator.Calculate(pricing, s_variables, new Dictionary<string, string?> { ["dock"] = "yes" }, quantity: 3);
        Assert.Equal(44m, quote.UnitPrice);
        Assert.Equal(132m, quote.Total);
        Assert.Equal(49m, quote.UnitOneTimePrice);
        Assert.Equal(147m, quote.OneTimeTotal);
    }

    [Fact]
    public void UnknownOrInvalidAnswers_DoNotChangeThePrice()
    {
        var quote = CatalogPriceCalculator.Calculate(s_laptop, s_variables, new Dictionary<string, string?>
        {
            ["memory"] = "128 GB RAM",
            ["dock"] = "false",
            ["price"] = "0.01",
            ["unit_price"] = "-100",
        });
        Assert.Equal(39m, quote.UnitPrice);
    }

    [Fact]
    public void OneTimeItems_HaveNoTermOrOneTimeSurcharge()
    {
        var pricing = new CatalogPricing("Headset", 189m, CatalogBillingType.OneTime, 36, 25m);
        var quote = CatalogPriceCalculator.Calculate(pricing, [], null, quantity: 2);
        Assert.False(quote.IsMonthly);
        Assert.Null(quote.TermMonths);
        Assert.Equal(0m, quote.UnitOneTimePrice);
        Assert.Equal(378m, quote.Total);
    }

    [Fact]
    public void NegativeTotals_AreClampedToZero()
    {
        var pricing = new CatalogPricing("Voucher", 5m, CatalogBillingType.OneTime, null, 0m);
        var discount = Define(new CatalogVariable { Name = "plan", Label = "Plan", Type = CatalogVariableType.Choice, Options = "Basic | -10" });
        var quote = CatalogPriceCalculator.Calculate(pricing, [discount], new Dictionary<string, string?> { ["plan"] = "Basic" });
        Assert.Equal(0m, quote.UnitPrice);
    }

    private static CatalogVariableDefinition Define(CatalogVariable variable) => CatalogVariableDefinition.From(variable);
}
