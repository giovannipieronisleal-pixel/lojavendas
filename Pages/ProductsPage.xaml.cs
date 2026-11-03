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
            var products = await _commerceService.GetProductsAsync();
            _products.Clear();
            foreach (var product in products)
                _products.Add(product);

            ProductCountLabel.Text = _products.Count.ToString();
            StockCountLabel.Text = _products.Sum(product => product.Stock).ToString();
            LowStockCountLabel.Text = _products.Count(product => product.Stock is > 0 and < 10).ToString();
            OutOfStockCountLabel.Text = _products.Count(product => product.Stock == 0).ToString();
            ProductListCountLabel.Text = $"Todos os produtos  {_products.Count}";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar o catálogo local de produtos.");
            await DisplayAlertAsync("Erro ao carregar produtos", "Não foi possível carregar o catálogo local.", "OK");
        }
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
}
