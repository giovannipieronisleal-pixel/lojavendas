using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using lojavendas.Models;
using lojavendas.Services;
using Microsoft.Extensions.Logging;

namespace lojavendas.Pages;

public sealed class AnalyticsProductRow
{
    public string Name { get; init; } = string.Empty;
    public string QuantityText { get; init; } = string.Empty;
    public double Share { get; init; }
}

public sealed class AnalyticsPaymentRow
{
    public string Name { get; init; } = string.Empty;
    public string AmountText { get; init; } = string.Empty;
}

public sealed class AnalyticsSellerRow
{
    public string Name { get; init; } = string.Empty;
    public int SalesCount { get; init; }
    public string RevenueText { get; init; } = string.Empty;
}

public partial class AnalyticsPage : ContentPage
{
    private readonly CommerceService _commerceService;
    private readonly ILogger<AnalyticsPage> _logger;
    private readonly ObservableCollection<AnalyticsProductRow> _topProducts = [];
    private readonly ObservableCollection<AnalyticsPaymentRow> _paymentMethods = [];
    private readonly ObservableCollection<AnalyticsSellerRow> _sellerPerformance = [];
    private IReadOnlyList<SaleRecord> _sales = [];
    private IReadOnlyList<SaleItemRecord> _saleItems = [];
    private IReadOnlyList<SalePaymentRecord> _payments = [];
    private DateTime? _chartStart;
    private DateTime? _chartEndExclusive;
    private IReadOnlyList<SaleRecord> _chartPeriodSales = [];
    private IReadOnlyList<SalePaymentRecord> _chartDebtPayments = [];
    private bool _isUpdatingFilters;

    public AnalyticsPage(CommerceService commerceService, ILogger<AnalyticsPage> logger)
    {
        InitializeComponent();
        _commerceService = commerceService;
        _logger = logger;
        TopProducts = _topProducts;
        PaymentMethods = _paymentMethods;
        SellerPerformance = _sellerPerformance;
        BindingContext = this;

        var today = DateTime.Today;
        _isUpdatingFilters = true;
        StartDatePicker.Date = today.AddDays(-29);
        EndDatePicker.Date = today;
        SellerPicker.ItemsSource = new List<string> { "Todos" };
        SellerPicker.SelectedIndex = 0;
        _isUpdatingFilters = false;
    }

    public ObservableCollection<AnalyticsProductRow> TopProducts { get; }
    public ObservableCollection<AnalyticsPaymentRow> PaymentMethods { get; }
    public ObservableCollection<AnalyticsSellerRow> SellerPerformance { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Yield();
        await LoadAnalyticsAsync();
        await RefreshBackupStatusAsync();
    }

    private async Task LoadAnalyticsAsync()
    {
        try
        {
            var data = await _commerceService.GetAnalyticsDataAsync();
            _sales = data.Sales;
            _saleItems = data.Items;
            _payments = data.Payments;

            var selectedSeller = SellerPicker.SelectedItem as string;
            var sellers = new[] { "Todos" }
                .Concat(_sales.Select(sale => sale.SellerName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
                .ToList();

            _isUpdatingFilters = true;
            SellerPicker.ItemsSource = sellers;
            SellerPicker.SelectedItem = sellers.Contains(selectedSeller, StringComparer.OrdinalIgnoreCase)
                ? sellers.First(name => string.Equals(name, selectedSeller, StringComparison.OrdinalIgnoreCase))
                : "Todos";
            _isUpdatingFilters = false;
            UpdateAnalytics();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar os dados para a análise de vendas.");
            await DisplayAlertAsync("Erro na análise", "Não foi possível consultar os dados locais de vendas.", "OK");
        }
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFilters)
            return;

        if (StartDatePicker.Date > EndDatePicker.Date)
        {
            EndDatePicker.Date = StartDatePicker.Date;
            return;
        }

        UpdateAnalytics();
    }

    private void UpdateAnalytics()
    {
        if (_sales.Count == 0 && _saleItems.Count == 0 && _payments.Count == 0)
        {
            RenderEmptyCharts();
            RevenueLabel.Text = FormatCurrency(0);
            CompletedSalesLabel.Text = "0";
            CompletionRateLabel.Text = "0% das vendas do período";
            AverageTicketLabel.Text = FormatCurrency(0);
            OutstandingDebtLabel.Text = FormatCurrency(0);
            OutstandingDebtCountLabel.Text = "0 vendas pendentes";
            UpdatedAtLabel.Text = $"Atualizado em {DateTime.Now:dd/MM/yyyy HH:mm}";
            _topProducts.Clear();
            _paymentMethods.Clear();
            _sellerPerformance.Clear();
            return;
        }

        var start = StartDatePicker.Date.GetValueOrDefault().Date;
        var endExclusive = EndDatePicker.Date.GetValueOrDefault().Date.AddDays(1);
        var selectedSeller = SellerPicker.SelectedItem as string;
        var sellerSales = _sales.Where(sale =>
                (selectedSeller is null or "Todos" ||
                 string.Equals(sale.SellerName, selectedSeller, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var sellerSaleIds = sellerSales.Select(sale => sale.Id).ToHashSet();
        var periodSales = sellerSales.Where(sale =>
                sale.CreatedAt >= start &&
                sale.CreatedAt < endExclusive)
            .ToList();
        var periodSaleIds = periodSales.Select(sale => sale.Id).ToHashSet();
        var completedSales = periodSales.Where(sale => sale.Status == "Concluída").ToList();
        var debtPayments = _payments.Where(payment =>
                payment.CreatedAt >= start &&
                payment.CreatedAt < endExclusive &&
                sellerSaleIds.Contains(payment.SaleId))
            .ToList();

        var revenue = periodSales
            .Where(sale => sale.Status == "Concluída" && sale.PaymentMethod != "Pagar depois")
            .Sum(sale => sale.Total) + debtPayments.Sum(payment => payment.Amount);
        RevenueLabel.Text = FormatCurrency(revenue);
        RevenueDetailLabel.Text = $"{periodSales.Count} vendas registradas no período";
        CompletedSalesLabel.Text = completedSales.Count.ToString(CultureInfo.GetCultureInfo("pt-BR"));
        var completionRate = periodSales.Count == 0 ? 0 : completedSales.Count * 100d / periodSales.Count;
        CompletionRateLabel.Text = $"{completionRate:0.#}% das vendas do período";
        AverageTicketLabel.Text = FormatCurrency(
            completedSales.Count == 0 ? 0 : completedSales.Average(sale => sale.Total));

        var pendingSales = _sales
            .Where(sale => sale.Status == "Pendente" &&
                           (selectedSeller is null or "Todos" ||
                            string.Equals(sale.SellerName, selectedSeller, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        OutstandingDebtLabel.Text = FormatCurrency(pendingSales.Sum(sale => sale.Total - sale.AmountPaid));
        OutstandingDebtCountLabel.Text = $"{pendingSales.Count} vendas pendentes";

        RenderRevenueChart(start, endExclusive, periodSales, debtPayments);
        RenderPaymentChart(periodSales, debtPayments);
        UpdatePaymentMethods(periodSales, debtPayments);
        UpdateTopProducts(periodSaleIds);
        UpdateSellerPerformance(periodSales);
        UpdatedAtLabel.Text = $"Atualizado em {DateTime.Now:dd/MM/yyyy HH:mm}";
    }

    private void OnRevenueChartSizeChanged(object? sender, EventArgs e) =>
        RenderCurrentRevenueChart();

    private void OnPaymentChartSizeChanged(object? sender, EventArgs e) =>
        RenderCurrentPaymentChart();

    private void RenderCurrentRevenueChart()
    {
        if (_chartStart is not DateTime start || _chartEndExclusive is not DateTime endExclusive)
            return;

        RenderRevenueChart(start, endExclusive, _chartPeriodSales, _chartDebtPayments);
    }

    private void RenderCurrentPaymentChart()
    {
        if (_chartStart is null)
            return;

        RenderPaymentChart(_chartPeriodSales, _chartDebtPayments);
    }

    private void RenderRevenueChart(
        DateTime start,
        DateTime endExclusive,
        IReadOnlyList<SaleRecord> periodSales,
        IReadOnlyList<SalePaymentRecord> debtPayments)
    {
        _chartStart = start;
        _chartEndExclusive = endExclusive;
        _chartPeriodSales = periodSales;
        _chartDebtPayments = debtPayments;

        var dayCount = Math.Max(1, (endExclusive - start).Days);
        var dailyRevenue = new decimal[dayCount];
        foreach (var sale in periodSales.Where(sale =>
                     sale.Status == "Concluída" && sale.PaymentMethod != "Pagar depois"))
        {
            var day = (sale.CreatedAt.Date - start).Days;
            if (day >= 0 && day < dailyRevenue.Length)
                dailyRevenue[day] += sale.Total;
        }

        foreach (var payment in debtPayments)
        {
            var day = (payment.CreatedAt.Date - start).Days;
            if (day >= 0 && day < dailyRevenue.Length)
                dailyRevenue[day] += payment.Amount;
        }

        RevenueChart.Drawable = new RevenueChartDrawable(start, dailyRevenue);
        RevenueChartEmptyLabel.IsVisible = dailyRevenue.All(amount => amount == 0);
        RevenueChart.Invalidate();
    }

    private void RenderPaymentChart(
        IReadOnlyList<SaleRecord> periodSales,
        IReadOnlyList<SalePaymentRecord> debtPayments)
    {
        var amountsByMethod = GetAmountsByMethod(periodSales, debtPayments)
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ToList();
        PaymentChart.Drawable = new PaymentChartDrawable(amountsByMethod);
        PaymentChartEmptyLabel.IsVisible = amountsByMethod.Count == 0;
        PaymentChart.Invalidate();
    }

    private void UpdateTopProducts(HashSet<int> periodSaleIds)
    {
        _topProducts.Clear();
        var bestSellers = _saleItems
            .Where(item => periodSaleIds.Contains(item.SaleId))
            .GroupBy(item => item.ProductName)
            .Select(group => new
            {
                Name = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .OrderByDescending(item => item.Quantity)
            .Take(5)
            .ToList();
        var maxQuantity = bestSellers.Count == 0 ? 1 : bestSellers[0].Quantity;

        foreach (var item in bestSellers)
        {
            _topProducts.Add(new AnalyticsProductRow
            {
                Name = item.Name,
                QuantityText = $"{item.Quantity} un.",
                Share = (double)item.Quantity / maxQuantity
            });
        }
    }

    private void UpdatePaymentMethods(
        IReadOnlyList<SaleRecord> periodSales,
        IReadOnlyList<SalePaymentRecord> debtPayments)
    {
        var amountsByMethod = GetAmountsByMethod(periodSales, debtPayments);
        _paymentMethods.Clear();
        foreach (var item in amountsByMethod.OrderByDescending(item => item.Value))
        {
            _paymentMethods.Add(new AnalyticsPaymentRow
            {
                Name = item.Key,
                AmountText = FormatCurrency(item.Value)
            });
        }
    }

    private static Dictionary<string, decimal> GetAmountsByMethod(
        IReadOnlyList<SaleRecord> periodSales,
        IReadOnlyList<SalePaymentRecord> debtPayments)
    {
        var amountsByMethod = periodSales
            .Where(sale => sale.Status == "Concluída" && sale.PaymentMethod != "Pagar depois")
            .GroupBy(sale => sale.PaymentMethod)
            .ToDictionary(group => group.Key, group => group.Sum(sale => sale.Total));
        foreach (var payment in debtPayments)
        {
            amountsByMethod[payment.PaymentMethod] =
                amountsByMethod.GetValueOrDefault(payment.PaymentMethod) + payment.Amount;
        }

        return amountsByMethod;
    }

    private void UpdateSellerPerformance(IReadOnlyList<SaleRecord> periodSales)
    {
        _sellerPerformance.Clear();
        var sellers = periodSales
            .GroupBy(sale => sale.SellerName)
            .Select(group => new
            {
                Name = group.Key,
                Count = group.Count(),
                Revenue = group.Sum(sale => sale.Total)
            })
            .OrderByDescending(item => item.Revenue)
            .Take(10);

        foreach (var seller in sellers)
        {
            _sellerPerformance.Add(new AnalyticsSellerRow
            {
                Name = string.IsNullOrWhiteSpace(seller.Name) ? "Não informado" : seller.Name,
                SalesCount = seller.Count,
                RevenueText = FormatCurrency(seller.Revenue)
            });
        }
    }

    private void RenderEmptyCharts()
    {
        _chartStart = StartDatePicker.Date.GetValueOrDefault().Date;
        _chartEndExclusive = EndDatePicker.Date.GetValueOrDefault().Date.AddDays(1);
        _chartPeriodSales = [];
        _chartDebtPayments = [];

        RevenueChart.Drawable = new RevenueChartDrawable(_chartStart.Value, new decimal[1]);
        RevenueChartEmptyLabel.IsVisible = true;
        RevenueChart.Invalidate();

        PaymentChart.Drawable = new PaymentChartDrawable([]);
        PaymentChartEmptyLabel.IsVisible = true;
        PaymentChart.Invalidate();
    }

    private async Task RefreshBackupStatusAsync()
    {
        try
        {
            var backupPath = await _commerceService.CreateDailyBackupIfDueAsync();
            if (backupPath is null || !File.Exists(backupPath))
            {
                BackupStatusLabel.Text = "Nenhum backup disponível.";
                return;
            }

            var lastBackup = File.GetLastWriteTime(backupPath);
            BackupStatusLabel.Text = $"Último backup local: {lastBackup:dd/MM/yyyy HH:mm}";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao criar o backup diário local do banco.");
            BackupStatusLabel.Text = "Não foi possível criar o backup diário. Tente novamente.";
        }
    }

    private async void OnCreateBackupClicked(object? sender, EventArgs e)
    {
        try
        {
            var backupPath = await _commerceService.CreateManualBackupAsync();
            BackupStatusLabel.Text = $"Backup criado: {File.GetLastWriteTime(backupPath):dd/MM/yyyy HH:mm}";
            var action = await DisplayActionSheetAsync(
                "Backup criado com sucesso",
                "Fechar",
                null,
                "Compartilhar arquivo");
            if (action == "Compartilhar arquivo")
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Backup dos dados da loja",
                    File = new ShareFile(backupPath)
                });
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao criar ou compartilhar backup local.");
            await DisplayAlertAsync("Erro no backup", "Não foi possível criar ou compartilhar o arquivo de backup.", "OK");
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            var start = StartDatePicker.Date.GetValueOrDefault().Date;
            var endExclusive = EndDatePicker.Date.GetValueOrDefault().Date.AddDays(1);
            var selectedSeller = SellerPicker.SelectedItem as string;
            var selectedSales = _sales.Where(sale =>
                    sale.CreatedAt >= start &&
                    sale.CreatedAt < endExclusive &&
                    (selectedSeller is null or "Todos" ||
                     string.Equals(sale.SellerName, selectedSeller, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(sale => sale.CreatedAt)
                .ToList();

            var csv = new StringBuilder("Venda;Data;Cliente;Vendedor;Pagamento;Situação;Total;Pago;Saldo\n");
            foreach (var sale in selectedSales)
            {
                csv.Append(Csv(sale.SaleNumber)).Append(';')
                    .Append(Csv(sale.CreatedAt.ToString("dd/MM/yyyy HH:mm"))).Append(';')
                    .Append(Csv(sale.CustomerDisplay)).Append(';')
                    .Append(Csv(sale.SellerName)).Append(';')
                    .Append(Csv(sale.PaymentMethod)).Append(';')
                    .Append(Csv(sale.Status)).Append(';')
                    .Append(sale.Total.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                    .Append(sale.AmountPaid.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                    .Append((sale.Total - sale.AmountPaid).ToString("0.00", CultureInfo.InvariantCulture))
                    .AppendLine();
            }

            var reportPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"analise-vendas-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            await File.WriteAllTextAsync(reportPath, "\uFEFF" + csv, Encoding.UTF8);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Análise de vendas",
                File = new ShareFile(reportPath)
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao exportar análise de vendas.");
            await DisplayAlertAsync("Erro ao exportar", "Não foi possível gerar ou compartilhar o relatório.", "OK");
        }
    }

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string FormatCurrency(decimal value) =>
        value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
}
