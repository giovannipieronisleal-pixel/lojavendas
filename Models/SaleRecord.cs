using SQLite;

namespace lojavendas.Models;

[Table("Sales")]
public sealed class SaleRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(20)]
    public string SaleNumber { get; set; } = string.Empty;

    [MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string SellerName { get; set; } = string.Empty;

    [MaxLength(32)]
    public string PaymentMethod { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    public long TotalInCents { get; set; }
    public long AmountPaidInCents { get; set; }
    public long ChangeInCents { get; set; }
    public DateTime CreatedAt { get; set; }

    [Ignore]
    public decimal Total
    {
        get => TotalInCents / 100m;
        set => TotalInCents = decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }

    [Ignore]
    public decimal AmountPaid
    {
        get => AmountPaidInCents / 100m;
        set => AmountPaidInCents = decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }

    [Ignore]
    public decimal Change
    {
        get => ChangeInCents / 100m;
        set => ChangeInCents = decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }

    [Ignore]
    public string TotalText => Total.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    [Ignore]
    public string CreatedAtText => CreatedAt.ToString("dd/MM/yyyy HH:mm");

    [Ignore]
    public string CustomerDisplay => string.IsNullOrWhiteSpace(CustomerName) ? "Consumidor" : CustomerName;

    [Ignore]
    public string OutstandingText => (Total - AmountPaid).ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    [Ignore]
    public string LatestPaymentMethodText { get; set; } = "Ainda não pago";

    [Ignore]
    public bool CanReceivePayment => Status == "Pendente";
}

[Table("SalePayments")]
public sealed class SalePaymentRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaleId { get; set; }

    [MaxLength(20)]
    public string ReceiptNumber { get; set; } = string.Empty;

    [MaxLength(32)]
    public string PaymentMethod { get; set; } = string.Empty;

    public long AmountInCents { get; set; }
    public long TotalPaidInCents { get; set; }
    public long OutstandingAfterInCents { get; set; }
    public long SaleTotalInCents { get; set; }
    public DateTime CreatedAt { get; set; }

    [Ignore]
    public decimal Amount => AmountInCents / 100m;

    [Ignore]
    public decimal TotalPaid => TotalPaidInCents / 100m;

    [Ignore]
    public decimal OutstandingAfter => OutstandingAfterInCents / 100m;

    [Ignore]
    public decimal SaleTotal => SaleTotalInCents / 100m;

    [Ignore]
    public string CreatedAtText => CreatedAt.ToString("dd/MM/yyyy HH:mm");
}

[Table("SaleItems")]
public sealed class SaleItemRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaleId { get; set; }

    public int ProductId { get; set; }

    [MaxLength(120)]
    public string ProductName { get; set; } = string.Empty;

    public long UnitPriceInCents { get; set; }
    public int Quantity { get; set; }
    public long SubtotalInCents { get; set; }

    [Ignore]
    public decimal UnitPrice
    {
        get => UnitPriceInCents / 100m;
        set => UnitPriceInCents = decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }

    [Ignore]
    public decimal Subtotal
    {
        get => SubtotalInCents / 100m;
        set => SubtotalInCents = decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }
}

public sealed class SaleLineInput
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
}

public sealed class SaleInput
{
    public string CustomerName { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public decimal AmountPaid { get; init; }
    public IReadOnlyList<SaleLineInput> Items { get; init; } = [];
}
