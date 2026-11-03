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
    private readonly ObservableCollection<SaleRecord> _debts = [];

    public DebtorsPage(CommerceService commerceService, ILogger<DebtorsPage> logger)
    {
        InitializeComponent();
        _commerceService = commerceService;
        _logger = logger;
        Debts = _debts;
        BindingContext = this;
    }

    public ObservableCollection<SaleRecord> Debts { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var debts = await _commerceService.GetCreditSalesAsync();
            var latestPaymentMethods = await _commerceService.GetLatestDebtPaymentMethodsAsync();
            _debts.Clear();
            foreach (var debt in debts)
            {
                if (latestPaymentMethods.TryGetValue(debt.Id, out var paymentMethod))
                    debt.LatestPaymentMethodText = paymentMethod;
                _debts.Add(debt);
            }
            var pendingDebts = debts.Where(debt => debt.CanReceivePayment).ToList();
            OutstandingTotalLabel.Text = pendingDebts.Sum(debt => debt.Total - debt.AmountPaid)
                .ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            OutstandingCountLabel.Text = $"{pendingDebts.Count} vendas pendentes";
            DebtListTitle.Text = $"Vendas a prazo  {debts.Count}";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar dívidas registradas no SQLite.");
            await DisplayAlertAsync("Erro ao carregar devedores", "Não foi possível consultar as vendas pendentes.", "OK");
        }
    }

    private async void OnBackToSalesClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//workspace/main/sales");

    private async void OnCheckoutClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//workspace/main/checkout");

    private async void OnRegisterDebtPaymentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: SaleRecord sale })
            return;

        var outstanding = sale.Total - sale.AmountPaid;
        var amountText = await DisplayPromptAsync(
            "Receber pagamento",
            $"Saldo devedor: {FormatCurrency(outstanding)}. Informe o valor recebido:",
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
            var payment = await _commerceService.RegisterDebtPaymentAsync(sale.Id, amount, paymentMethod);
            var receipt = FormatReceipt(sale, payment);
            await RefreshDebtsAsync();

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
            _logger.LogError(exception, "Falha ao registrar pagamento da venda {SaleNumber}.", sale.SaleNumber);
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
                await DisplayAlertAsync("Comprovantes", "Ainda não há pagamentos registrados para esta dívida.", "OK");
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
        var debts = await _commerceService.GetCreditSalesAsync();
        var latestPaymentMethods = await _commerceService.GetLatestDebtPaymentMethodsAsync();
        _debts.Clear();
        foreach (var debt in debts)
        {
            if (latestPaymentMethods.TryGetValue(debt.Id, out var paymentMethod))
                debt.LatestPaymentMethodText = paymentMethod;
            _debts.Add(debt);
        }

        var pendingDebts = debts.Where(debt => debt.CanReceivePayment).ToList();
        OutstandingTotalLabel.Text = pendingDebts.Sum(debt => debt.Total - debt.AmountPaid)
            .ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
        OutstandingCountLabel.Text = $"{pendingDebts.Count} vendas pendentes";
        DebtListTitle.Text = $"Vendas a prazo  {debts.Count}";
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
