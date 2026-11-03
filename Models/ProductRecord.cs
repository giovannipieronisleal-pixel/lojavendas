using SQLite;

namespace lojavendas.Models;

[Table("Products")]
public sealed class ProductRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64), Unique]
    public string Code { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Category { get; set; } = string.Empty;

    public long PriceInCents { get; set; }
    public int Stock { get; set; }

    [Ignore]
    public decimal Price
    {
        get => PriceInCents / 100m;
        set => PriceInCents = decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }

    [Ignore]
    public string PriceText => Price.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    [Ignore]
    public string StockText => $"{Stock} un.";

    [Ignore]
    public string AvailabilityText => Stock == 0 ? "Sem estoque" : Stock < 10 ? "Estoque baixo" : "Disponível";
}

public sealed class ProductInput
{
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
}
