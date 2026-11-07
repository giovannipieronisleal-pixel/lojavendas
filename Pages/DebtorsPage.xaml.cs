using System.Collections.ObjectModel;
using System.Globalization;
using lojavendas.Models;
using lojavendas.Services;
using Microsoft.Extensions.Logging;

namespace lojavendas.Pages;

public partial class DebtorsPage : ContentPage
{
    private readonly CommerceService _commerceService;
    private readonly ILogger<DebtorsPage> _logger;
    private readonly ObservableCollection<CustomerAccount> _customers = [];
    private IReadOnlyList<CustomerAccount> _allCustomers = [];
    private bool _showPaidCustomers;

    public DebtorsPage(CommerceService commerceService, ILogger<DebtorsPage> logger)
    {
        InitializeComponent();
        _commerceService = commerceService;
        _logger = logger;
        Customers = _customers;
        BindingContext = this;
    }

    public ObservableCollection<CustomerAccount> Customers { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await RefreshDebtsAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar dívidas registradas no SQLite.");
            await DisplayAlertAsync("Erro ao carregar devedores", "Não foi possível consultar as vendas pendentes.", "OK");
        }
    }

    private async void OnBackToSalesClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(AppRoutes.Sales);

    private async void OnCheckoutClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(AppRoutes.Checkout);

    private void OnCustomerSearchTextChanged(object? sender, TextChangedEventArgs e) =>
        ApplyFilter();

    private void OnShowPaidToggleClicked(object? sender, EventArgs e)
    {
        _showPaidCustomers = !_showPaidCustomers;
        ShowPaidToggle.Text = _showPaidCustomers ? "Ocultar quitados" : "Mostrar quitados";
        ApplyFilter();
    }

    private void OnToggleSalesClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: CustomerAccount account })
            account.IsExpanded = !account.IsExpanded;
    }

    private async void OnReceiveFromCustomerClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: CustomerAccount account })
            return;

        var outstanding = account.Outstanding;
        if (outstanding <= 0)
            return;

        var amountText = await DisplayPromptAsync(
            "Receber pagamento",
            $"{account.Name}\nSaldo devedor: {FormatCurrency(outstanding)}.\n" +
            "O valor é abatido primeiro das compras mais antigas. Informe o valor recebido:",
            "Continuar",
            "Cancelar",
            "Valor",
            maxLength: 15,
            keyboard: Keyboard.Numeric,
            initialValue: outstanding.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")));
        if (amountText is null)
            return;
        if (!MoneyParser.TryParse(amountText, out var amount) || amount <= 0 || amount > outstanding)
        {
            await DisplayAlertAsync("Valor inválido", "Informe um valor maior que zero e igual ou menor que o saldo devedor.", "OK");
            return;
        }

        var paymentMethod = await DisplayActionSheetAsync(
            "Forma de pagamento",
            "Cancelar",
            null,
            "Dinheiro",
            "Pix",
            "Cartão");
        if (paymentMethod is not ("Dinheiro" or "Pix" or "Cartão"))
            return;

        try
        {
            var payments = await _commerceService.RegisterCustomerPaymentAsync(account.Name, amount, paymentMethod);
            var salesById = account.Sales.ToDictionary(sale => sale.Id);
            var receipt = FormatCustomerReceipt(account.Name, payments, salesById, outstanding - amount);
            await RefreshDebtsAsync();

            var title = payments.Count == 1
                ? $"Comprovante {payments[0].ReceiptNumber}"
                : "Comprovante de pagamento";
            var action = await DisplayActionSheetAsync(title, "Fechar", null, "Compartilhar comprovante");
            if (action == "Compartilhar comprovante")
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = title,
                    Text = receipt
                });
            }
            else
            {
                await DisplayAlertAsync(title, receipt, "OK");
            }
        }
        catch (ArgumentException exception)
        {
            await DisplayAlertAsync("Pagamento não registrado", exception.Message, "OK");
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Pagamento não registrado", exception.Message, "OK");
            await RefreshDebtsAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao registrar pagamento do cliente {CustomerName}.", account.Name);
            await DisplayAlertAsync("Erro ao registrar pagamento", "Não foi possível registrar o pagamento no banco local.", "OK");
        }
    }

    private async void OnPaymentReceiptsClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: SaleRecord sale })
            return;

        try
        {
            var payments = await _commerceService.GetSalePaymentsAsync(sale.Id);
            if (payments.Count == 0)
            {
                await DisplayAlertAsync("Comprovantes", "Ainda não há pagamentos registrados para esta venda.", "OK");
                return;
            }

            var options = payments
                .Select(payment => $"{payment.ReceiptNumber} · {payment.PaymentMethod} · {FormatCurrency(payment.Amount)} · {payment.CreatedAtText}")
                .ToArray();
            var selected = await DisplayActionSheetAsync("Comprovantes de pagamento", "Fechar", null, options);
            var index = Array.IndexOf(options, selected);
            if (index < 0)
                return;

            var payment = payments[index];
            var receipt = FormatReceipt(sale, payment);
            var action = await DisplayActionSheetAsync(
                $"Comprovante {payment.ReceiptNumber}",
                "Fechar",
                null,
                "Compartilhar comprovante");
            if (action == "Compartilhar comprovante")
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = $"Comprovante {payment.ReceiptNumber}",
                    Text = receipt
                });
            }
            else
            {
                await DisplayAlertAsync($"Comprovante {payment.ReceiptNumber}", receipt, "OK");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar comprovantes da venda {SaleNumber}.", sale.SaleNumber);
            await DisplayAlertAsync("Erro ao carregar comprovantes", "Não foi possível consultar os pagamentos desta venda.", "OK");
        }
    }

    private async Task RefreshDebtsAsync()
    {
        var expandedKeys = _allCustomers
            .Where(account => account.IsExpanded)
            .Select(account => CommerceService.CustomerKey(account.Name))
            .ToHashSet();

        _allCustomers = await _commerceService.GetCustomerAccountsAsync();
        foreach (var account in _allCustomers)
            account.IsExpanded = expandedKeys.Contains(CommerceService.CustomerKey(account.Name));

        var debtors = _allCustomers.Where(account => account.CanReceivePayment).ToList();
        OutstandingTotalLabel.Text = FormatCurrency(debtors.Sum(account => account.Outstanding));
        OutstandingCountLabel.Text = debtors.Count == 1
            ? "1 cliente com saldo em aberto"
            : $"{debtors.Count} clientes com saldo em aberto";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = CommerceService.CustomerKey(CustomerSearchEntry.Text);
        var visible = _allCustomers
            .Where(account => _showPaidCustomers || account.CanReceivePayment)
            .Where(account => query.Length == 0 || CommerceService.CustomerKey(account.Name).Contains(query, StringComparison.Ordinal))
            .ToList();

        _customers.Clear();
        foreach (var account in visible)
            _customers.Add(account);

        EmptyStateLayout.IsVisible = visible.Count == 0;
        DebtListTitle.Text = $"Clientes  {visible.Count}";
    }

    private static string FormatCustomerReceipt(
        string customerName,
        IReadOnlyList<SalePaymentRecord> payments,
        IReadOnlyDictionary<int, SaleRecord> salesById,
        decimal remainingBalance)
    {
        var first = payments[0];
        var lines = payments.Select(payment =>
        {
            var saleNumber = salesById.TryGetValue(payment.SaleId, out var sale) ? sale.SaleNumber : $"#{payment.SaleId}";
            var status = payment.OutstandingAfter == 0 ? "quitada" : $"restam {FormatCurrency(payment.OutstandingAfter)}";
            return $"  {payment.ReceiptNumber} · Venda {saleNumber}: {FormatCurrency(payment.Amount)} ({status})";
        });

        var sellers = payments
            .Select(payment => salesById.TryGetValue(payment.SaleId, out var sale) ? sale.SellerName : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct();

        return "COMPROVANTE DE PAGAMENTO\n\n" +
               $"Cliente: {customerName}\n" +
               $"Data: {first.CreatedAtText}\n" +
               $"Forma de pagamento: {first.PaymentMethod}\n" +
               $"Valor recebido: {FormatCurrency(payments.Sum(payment => payment.Amount))}\n\n" +
               "Abatido em:\n" +
               string.Join("\n", lines) + "\n\n" +
               $"Saldo restante do cliente: {FormatCurrency(Math.Max(0, remainingBalance))}\n" +
               $"Atendente(s) da venda: {string.Join(", ", sellers)}\n\n" +
               "Comprovante comercial; não substitui nota fiscal.";
    }

    private static string FormatReceipt(SaleRecord sale, SalePaymentRecord payment) =>
        $"COMPROVANTE DE PAGAMENTO\n\n" +
        $"Comprovante: {payment.ReceiptNumber}\n" +
        $"Venda: {sale.SaleNumber}\n" +
        $"Cliente: {sale.CustomerDisplay}\n" +
        $"Data: {payment.CreatedAtText}\n" +
        $"Forma de pagamento: {payment.PaymentMethod}\n" +
        $"Valor recebido: {FormatCurrency(payment.Amount)}\n" +
        $"Total da venda: {FormatCurrency(payment.SaleTotal)}\n" +
        $"Total pago: {FormatCurrency(payment.TotalPaid)}\n" +
        $"Saldo restante: {FormatCurrency(payment.OutstandingAfter)}\n" +
        $"Atendente: {sale.SellerName}\n\n" +
        "Comprovante comercial; não substitui nota fiscal.";

    private static string FormatCurrency(decimal value) =>
        value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
}
