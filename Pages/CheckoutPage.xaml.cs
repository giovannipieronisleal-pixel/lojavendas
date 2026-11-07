using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using lojavendas.Models;
using lojavendas.Services;
using Microsoft.Extensions.Logging;

namespace lojavendas.Pages;

public sealed class CartLine : INotifyPropertyChanged
{
    public ProductRecord Product { get; }
    public int Quantity { get; private set; }
    public string SubtotalText => (Product.Price * Quantity)
        .ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

    public CartLine(ProductRecord product, int quantity)
    {
        Product = product;
        Quantity = quantity;
    }

    public void AddQuantity(int quantity)
    {
        Quantity += quantity;
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(SubtotalText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public partial class CheckoutPage : ContentPage
{
    private readonly CommerceService _commerceService;
    private readonly ILocalAuthenticationService _authenticationService;
    private readonly ILogger<CheckoutPage> _logger;
    private readonly ObservableCollection<CartLine> _cart = [];
    private readonly ObservableCollection<ProductRecord> _searchResults = [];
    private readonly List<ProductRecord> _availableProducts = [];
    private ProductRecord? _selectedProduct;
    private int _quantity = 1;
    private string _paymentMethod = "Dinheiro";
    private bool _isUpdatingSearchText;
    private string? _lastOperatorUsername;

    public CheckoutPage(
        CommerceService commerceService,
        ILocalAuthenticationService authenticationService,
        ILogger<CheckoutPage> logger)
    {
        InitializeComponent();
        _commerceService = commerceService;
        _authenticationService = authenticationService;
        _logger = logger;
        Cart = _cart;
        SearchResults = _searchResults;
        BindingContext = this;

        SetPaymentMethod("Dinheiro");
        _cart.CollectionChanged += (_, _) => UpdateTotals();
    }

    public ObservableCollection<CartLine> Cart { get; }
    public ObservableCollection<ProductRecord> SearchResults { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var employee = _authenticationService.CurrentEmployee;
        if (employee is null || !employee.IsActive)
        {
            await Shell.Current.GoToAsync(AppRoutes.Login);
            return;
        }

        if (!string.Equals(_lastOperatorUsername, employee.Username, StringComparison.OrdinalIgnoreCase))
        {
            _cart.Clear();
            AmountReceivedEntry.Text = string.Empty;
            SetPaymentMethod("Dinheiro");
            _lastOperatorUsername = employee.Username;
        }

        var isSeller = employee.Role == EmployeeRoles.Seller;
        BackToSalesButton.IsVisible = !isSeller;
        DebtorsTabButton.IsVisible = !isSeller;
        SellerLabel.Text = employee.Name;

        try
        {
            _availableProducts.Clear();
            _availableProducts.AddRange(await _commerceService.GetProductsAsync(availableOnly: true));
            FilterProducts(ProductSearchEntry.Text);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar produtos disponíveis para o caixa.");
            await DisplayAlertAsync("Erro ao carregar produtos", "Não foi possível consultar o catálogo local.", "OK");
        }
    }

    private decimal Total => _cart.Sum(line => line.Product.Price * line.Quantity);
    private string FormatCurrency(decimal value) =>
        value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    private async void OnBackToSalesClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(AppRoutes.Sales);

    private async void OnDebtorsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(AppRoutes.Debtors);

    private void OnProductSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSearchText)
            return;

        _selectedProduct = null;
        _quantity = 1;
        QuantityLabel.Text = _quantity.ToString();
        FilterProducts(e.NewTextValue);
    }

    private void OnProductSearchCompleted(object? sender, EventArgs e) =>
        SelectSearchResult();

    private void OnProductSearchClicked(object? sender, EventArgs e) =>
        SelectSearchResult();

    private void OnSearchResultTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ProductRecord product })
            SelectProduct(product);
    }

    private void FilterProducts(string? query)
    {
        _searchResults.Clear();
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            ProductSearchResults.IsVisible = false;
            NoProductsFoundLabel.IsVisible = false;
            return;
        }

        var matches = _availableProducts
            .Where(product =>
                product.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                product.Code.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(product => string.Equals(product.Code, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ThenBy(product => product.Name)
            .Take(8)
            .ToList();

        foreach (var product in matches)
            _searchResults.Add(product);

        ProductSearchResults.IsVisible = matches.Count > 0;
        NoProductsFoundLabel.IsVisible = matches.Count == 0;
    }

    private void SelectSearchResult()
    {
        if (_searchResults.Count == 0)
            return;

        var query = ProductSearchEntry.Text?.Trim();
        var match = _searchResults.FirstOrDefault(product =>
                        string.Equals(product.Code, query, StringComparison.OrdinalIgnoreCase)) ??
                    _searchResults.FirstOrDefault();
        if (match is not null)
            SelectProduct(match);
    }

    private void SelectProduct(ProductRecord product)
    {
        _selectedProduct = product;
        _quantity = 1;
        QuantityLabel.Text = _quantity.ToString();
        _isUpdatingSearchText = true;
        ProductSearchEntry.Text = $"{product.Name} · {product.Code}";
        _isUpdatingSearchText = false;
        ProductSearchResults.IsVisible = false;
        NoProductsFoundLabel.IsVisible = false;
        CheckoutErrorLabel.IsVisible = false;
    }

    private void OnDecreaseQuantityClicked(object? sender, EventArgs e)
    {
        if (_quantity <= 1)
            return;

        _quantity--;
        QuantityLabel.Text = _quantity.ToString();
    }

    private void OnIncreaseQuantityClicked(object? sender, EventArgs e)
    {
        if (_selectedProduct is not null && _quantity >= _selectedProduct.Stock)
        {
            ShowCheckoutError($"Estoque disponível de {_selectedProduct.Name}: {_selectedProduct.Stock} unidade(s).");
            return;
        }

        _quantity++;
        QuantityLabel.Text = _quantity.ToString();
        CheckoutErrorLabel.IsVisible = false;
    }

    private void OnAddProductClicked(object? sender, EventArgs e)
    {
        CheckoutErrorLabel.IsVisible = false;
        if (_selectedProduct is not ProductRecord product)
        {
            ShowCheckoutError("Pesquise e selecione um produto antes de adicioná-lo.");
            return;
        }

        var currentQuantity = _cart.FirstOrDefault(line => line.Product.Id == product.Id)?.Quantity ?? 0;
        if (currentQuantity + _quantity > product.Stock)
        {
            ShowCheckoutError($"Estoque disponível de {product.Name}: {product.Stock} unidade(s).");
            return;
        }

        var line = _cart.FirstOrDefault(item => item.Product.Id == product.Id);
        if (line is null)
            _cart.Add(new CartLine(product, _quantity));
        else
            line.AddQuantity(_quantity);

        _selectedProduct = null;
        _quantity = 1;
        QuantityLabel.Text = "1";
        _isUpdatingSearchText = true;
        ProductSearchEntry.Text = string.Empty;
        _isUpdatingSearchText = false;
        _searchResults.Clear();
        ProductSearchResults.IsVisible = false;
        NoProductsFoundLabel.IsVisible = false;
        UpdateTotals();
    }

    private void OnRemoveItemClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: CartLine line })
            _cart.Remove(line);
    }

    private void OnCashPaymentClicked(object? sender, EventArgs e) => SetPaymentMethod("Dinheiro");
    private void OnPixPaymentClicked(object? sender, EventArgs e) => SetPaymentMethod("Pix");
    private void OnCardPaymentClicked(object? sender, EventArgs e) => SetPaymentMethod("Cartão");
    private void OnPayLaterClicked(object? sender, EventArgs e) => SetPaymentMethod("Pagar depois");

    private void SetPaymentMethod(string paymentMethod)
    {
        _paymentMethod = paymentMethod;
        CashPaymentPanel.IsVisible = paymentMethod == "Dinheiro";
        PayLaterHint.IsVisible = paymentMethod == "Pagar depois";
        SelectedPaymentLabel.Text = paymentMethod == "Pagar depois"
            ? "Forma selecionada: Devedor"
            : $"Forma selecionada: {paymentMethod}";

        SetPaymentButtonStyle(CashPaymentButton, paymentMethod == "Dinheiro");
        SetPaymentButtonStyle(PixPaymentButton, paymentMethod == "Pix");
        SetPaymentButtonStyle(CardPaymentButton, paymentMethod == "Cartão");
        SetPaymentButtonStyle(PayLaterButton, paymentMethod == "Pagar depois");
    }

    private static void SetPaymentButtonStyle(Button button, bool isSelected)
    {
        button.BackgroundColor = isSelected ? Color.FromArgb("#EBEFE6") : Colors.White;
        button.TextColor = isSelected ? Color.FromArgb("#293F35") : Color.FromArgb("#242B28");
        button.BorderColor = isSelected ? Color.FromArgb("#52675E") : Color.FromArgb("#D7DBD2");
    }

    private void OnAmountReceivedChanged(object? sender, TextChangedEventArgs e)
    {
        var parsed = TryParseMoney(e.NewTextValue, out var received);
        ChangeLabel.Text = parsed ? FormatCurrency(Math.Max(0, received - Total)) : FormatCurrency(0);
    }

    private async void OnReceiveClicked(object? sender, EventArgs e)
    {
        CheckoutErrorLabel.IsVisible = false;
        var employee = _authenticationService.CurrentEmployee;
        if (employee is null || !employee.IsActive)
        {
            ShowCheckoutError("Sua sessão terminou. Entre novamente para registrar a venda.");
            await Shell.Current.GoToAsync(AppRoutes.Login);
            return;
        }

        if (_cart.Count == 0)
        {
            ShowCheckoutError("Adicione pelo menos um produto à venda.");
            return;
        }

        var paymentMethod = _paymentMethod;
        var customerName = string.Empty;
        if (paymentMethod == "Pagar depois")
        {
            var selectedName = await PickCustomerNameAsync();
            if (selectedName is null)
                return;
            customerName = selectedName;
            if (string.IsNullOrWhiteSpace(customerName))
            {
                ShowCheckoutError("Informe o nome do cliente para registrar a venda a prazo.");
                return;
            }
        }

        var amountPaid = Total;
        if (paymentMethod == "Dinheiro")
        {
            if (!TryParseMoney(AmountReceivedEntry.Text, out amountPaid) || amountPaid < Total)
            {
                ShowCheckoutError("O valor recebido precisa ser igual ou maior que o total.");
                return;
            }
        }

        try
        {
            var sale = await _commerceService.CreateSaleAsync(new SaleInput
            {
                CustomerName = customerName,
                PaymentMethod = paymentMethod,
                AmountPaid = amountPaid,
                Items = _cart.Select(line => new SaleLineInput
                {
                    ProductId = line.Product.Id,
                    Quantity = line.Quantity
                }).ToList()
            }, employee.Name);

            var confirmation = sale.Status == "Pendente"
                ? $"Venda {sale.SaleNumber} registrada para {sale.CustomerName}. Total em aberto: {FormatCurrency(sale.Total)}."
                : $"Venda {sale.SaleNumber} concluída. Troco: {FormatCurrency(sale.Change)}.";
            await DisplayAlertAsync("Venda registrada", confirmation, "OK");
            _cart.Clear();
            AmountReceivedEntry.Text = string.Empty;
            await RefreshProductsAsync();
            await Shell.Current.GoToAsync(AppRoutes.Sales);
        }
        catch (ArgumentException exception)
        {
            ShowCheckoutError(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCheckoutError(exception.Message);
            await RefreshProductsAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao registrar venda no banco SQLite.");
            ShowCheckoutError("Não foi possível registrar a venda. Tente novamente.");
        }
    }

    /// <summary>
    /// Permite escolher um cliente que já tem conta (para somar a dívida) ou digitar um novo nome.
    /// Retorna null se o usuário cancelar.
    /// </summary>
    private async Task<string?> PickCustomerNameAsync()
    {
        const string newCustomerOption = "＋ Novo cliente";
        IReadOnlyList<string> existingNames = [];
        try
        {
            existingNames = await _commerceService.GetCreditCustomerNamesAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Falha ao carregar clientes existentes; seguindo com digitação manual.");
        }

        if (existingNames.Count > 0)
        {
            var options = new[] { newCustomerOption }.Concat(existingNames).ToArray();
            var choice = await DisplayActionSheetAsync(
                "Adicionar na conta de qual cliente?",
                "Cancelar",
                null,
                options);
            if (choice is null || choice == "Cancelar")
                return null;
            if (choice != newCustomerOption)
                return choice;
        }

        return await DisplayPromptAsync(
            "Identificar cliente",
            "Informe o nome da pessoa responsável pela dívida.\nSe o nome já existir, a venda será somada à conta dele.",
            "Continuar",
            "Cancelar",
            "Nome do cliente",
            maxLength: 120,
            keyboard: Keyboard.Text);
    }

    private async Task RefreshProductsAsync()
    {
        _availableProducts.Clear();
        _availableProducts.AddRange(await _commerceService.GetProductsAsync(availableOnly: true));
        FilterProducts(ProductSearchEntry.Text);
    }

    private void UpdateTotals()
    {
        var quantity = _cart.Sum(line => line.Quantity);
        EmptyCartLabel.IsVisible = _cart.Count == 0;
        CartCountLabel.Text = $"{quantity} {(quantity == 1 ? "item" : "itens")}";
        CartSummaryLabel.Text = CartCountLabel.Text;
        TotalLabel.Text = FormatCurrency(Total);
        if (TryParseMoney(AmountReceivedEntry.Text, out var received))
            ChangeLabel.Text = FormatCurrency(Math.Max(0, received - Total));
    }

    private void ShowCheckoutError(string message)
    {
        CheckoutErrorLabel.Text = message;
        CheckoutErrorLabel.IsVisible = true;
    }

    private static bool TryParseMoney(string? value, out decimal amount)
    {
        return MoneyParser.TryParse(value, out amount);
    }
}
