using System.Collections.ObjectModel;
using lojavendas.Models;
using lojavendas.Services;
using Microsoft.Extensions.Logging;

namespace lojavendas.Pages;

public partial class SalesPage : ContentPage
{
    private readonly CommerceService _commerceService;
    private readonly ILogger<SalesPage> _logger;
    private readonly ObservableCollection<SaleRecord> _sales = [];

    public SalesPage(CommerceService commerceService, ILogger<SalesPage> logger)
    {
        InitializeComponent();
        _commerceService = commerceService;
        _logger = logger;
        Sales = _sales;
        BindingContext = this;
    }

    public ObservableCollection<SaleRecord> Sales { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var sales = await _commerceService.GetSalesAsync();
            _sales.Clear();
            foreach (var sale in sales)
                _sales.Add(sale);

            var completed = sales.Where(sale => sale.Status == "Concluída").ToList();
            var pending = sales.Where(sale => sale.Status == "Pendente").ToList();
            RevenueLabel.Text = completed.Sum(sale => sale.Total)
                .ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            CompletedCountLabel.Text = completed.Count.ToString();
            PendingTotalLabel.Text = pending.Sum(sale => sale.Total - sale.AmountPaid)
                .ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            PendingCountLabel.Text = $"{pending.Count} vendas pendentes";
            SalesListTitle.Text = $"Vendas registradas  {sales.Count}";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar o histórico local de vendas.");
            await DisplayAlertAsync("Erro ao carregar vendas", "Não foi possível consultar o histórico local.", "OK");
        }
    }

    private async void OnRegisterSaleClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//workspace/main/checkout");

    private async void OnDebtorsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//workspace/main/debts");
}
