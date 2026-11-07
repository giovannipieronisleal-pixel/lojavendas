using System.Collections.ObjectModel;
using lojavendas.Models;
using lojavendas.Services;
using Microsoft.Extensions.Logging;

namespace lojavendas.Pages;

public partial class ProductsPage : ContentPage
{
    private readonly CommerceService _commerceService;
    private readonly ILogger<ProductsPage> _logger;
    private readonly ObservableCollection<ProductRecord> _products = [];
    private IReadOnlyList<ProductRecord> _allProducts = [];

    public ProductsPage(CommerceService commerceService, ILogger<ProductsPage> logger)
    {
        InitializeComponent();
        _commerceService = commerceService;
        _logger = logger;
        Products = _products;
        BindingContext = this;
    }

    public ObservableCollection<ProductRecord> Products { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            _allProducts = await _commerceService.GetProductsAsync();

            ProductCountLabel.Text = _allProducts.Count.ToString();
            StockCountLabel.Text = _allProducts.Sum(product => product.Stock).ToString();
            LowStockCountLabel.Text = _allProducts.Count(product => product.Stock is > 0 and < 10).ToString();
            OutOfStockCountLabel.Text = _allProducts.Count(product => product.Stock == 0).ToString();

            ApplyFilter();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar o catálogo local de produtos.");
            await DisplayAlertAsync("Erro ao carregar produtos", "Não foi possível carregar o catálogo local.", "OK");
        }
    }

    private void OnProductFilterTextChanged(object? sender, TextChangedEventArgs e) =>
        ApplyFilter();

    private void ApplyFilter()
    {
        var query = ProductFilterEntry?.Text?.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _allProducts
            : _allProducts
                .Where(product =>
                    product.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    product.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    product.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _products.Clear();
        foreach (var product in filtered)
            _products.Add(product);

        EmptyStateLayout.IsVisible = _products.Count == 0;
        ProductListCountLabel.Text = $"Todos os produtos  {_products.Count}";
    }

    private async void OnNewProductClicked(object? sender, EventArgs e)
    {
        var editor = new ProductEditorPage();
        await Navigation.PushModalAsync(editor);
        var input = await editor.Completion;
        if (input is null)
            return;

        try
        {
            await _commerceService.CreateProductAsync(input);
            await LoadProductsAsync();
        }
        catch (ArgumentException exception)
        {
            await DisplayAlertAsync("Dados inválidos", exception.Message, "OK");
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Não foi possível salvar", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao salvar produto no banco SQLite.");
            await DisplayAlertAsync("Erro ao salvar produto", "O produto não foi salvo no banco local.", "OK");
        }
    }

    private async void OnEditProductClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: ProductRecord product })
            return;

        var editor = new ProductEditorPage(product);
        await Navigation.PushModalAsync(editor);
        var input = await editor.Completion;
        if (input is null)
            return;

        try
        {
            await _commerceService.UpdateProductAsync(product.Id, input);
            await LoadProductsAsync();
        }
        catch (ArgumentException exception)
        {
            await DisplayAlertAsync("Dados inválidos", exception.Message, "OK");
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Não foi possível salvar", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao atualizar o produto {ProductCode}.", product.Code);
            await DisplayAlertAsync("Erro ao salvar produto", "As alterações não foram salvas no banco local.", "OK");
        }
    }

    private async void OnAddStockClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: ProductRecord product })
            return;

        var quantityText = await DisplayPromptAsync(
            "Adicionar ao estoque",
            $"{product.Name}\nEstoque atual: {product.Stock} un.\nQuantas unidades chegaram?",
            "Adicionar",
            "Cancelar",
            "Quantidade",
            maxLength: 6,
            keyboard: Keyboard.Numeric);
        if (quantityText is null)
            return;

        if (!int.TryParse(quantityText.Trim(), out var quantity) || quantity <= 0)
        {
            await DisplayAlertAsync("Quantidade inválida", "Informe um número inteiro maior que zero.", "OK");
            return;
        }

        try
        {
            var updated = await _commerceService.AddStockAsync(product.Id, quantity);
            await LoadProductsAsync();
            await DisplayAlertAsync("Estoque atualizado", $"{updated.Name}: agora com {updated.Stock} un.", "OK");
        }
        catch (ArgumentException exception)
        {
            await DisplayAlertAsync("Quantidade inválida", exception.Message, "OK");
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Não foi possível atualizar", exception.Message, "OK");
            await LoadProductsAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao adicionar estoque ao produto {ProductCode}.", product.Code);
            await DisplayAlertAsync("Erro ao atualizar estoque", "O estoque não foi atualizado no banco local.", "OK");
        }
    }
}
