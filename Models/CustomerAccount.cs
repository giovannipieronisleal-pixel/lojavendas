using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace lojavendas.Models;

/// <summary>
/// Conta de um cliente devedor: agrupa todas as vendas a prazo feitas com o mesmo nome.
/// </summary>
public sealed class CustomerAccount : INotifyPropertyChanged
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private bool _isExpanded;

    public CustomerAccount(string name, IReadOnlyList<SaleRecord> sales)
    {
        Name = name;
        Sales = sales;
    }

    public string Name { get; }

    /// <summary>Vendas a prazo do cliente, da mais recente para a mais antiga.</summary>
    public IReadOnlyList<SaleRecord> Sales { get; }

    public decimal Outstanding => Sales
        .Where(sale => sale.Status == "Pendente")
        .Sum(sale => sale.Total - sale.AmountPaid);

    public decimal TotalPurchased => Sales.Sum(sale => sale.Total);

    public int PendingSalesCount => Sales.Count(sale => sale.Status == "Pendente");

    public bool CanReceivePayment => Outstanding > 0;

    public string OutstandingText => Outstanding.ToString("C", PtBr);

    public string TotalPurchasedText => TotalPurchased.ToString("C", PtBr);

    public string StatusText => CanReceivePayment ? "Em aberto" : "Quitado";

    public Color StatusColor => CanReceivePayment ? Color.FromArgb("#A97528") : Color.FromArgb("#3F7A52");

    public string SummaryText
    {
        get
        {
            var salesText = Sales.Count == 1 ? "1 compra" : $"{Sales.Count} compras";
            var pendingText = PendingSalesCount == 1 ? "1 pendente" : $"{PendingSalesCount} pendentes";
            return $"{salesText} · {pendingText}";
        }
    }

    public string LastPurchaseText => Sales.Count == 0
        ? "—"
        : Sales.Max(sale => sale.CreatedAt).ToString("dd/MM/yyyy HH:mm");

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToggleText));
        }
    }

    public string ToggleText => IsExpanded ? "Ocultar compras" : "Ver compras";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
