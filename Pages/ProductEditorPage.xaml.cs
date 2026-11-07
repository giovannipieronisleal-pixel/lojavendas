using lojavendas.Models;

namespace lojavendas.Pages;

public partial class ProductEditorPage : ContentPage
{
    private readonly TaskCompletionSource<ProductInput?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ProductEditorPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Abre o editor preenchido com os dados de um produto existente.
    /// </summary>
    public ProductEditorPage(ProductRecord product) : this()
    {
        TitleLabel.Text = "Editar produto";
        SubtitleLabel.Text = "Altere as informações, o preço ou corrija o estoque.";
        StockLabel.Text = "Estoque atual";
        NameEntry.Text = product.Name;
        CodeEntry.Text = product.Code;
        CategoryEntry.Text = product.Category;
        PriceEntry.Text = product.Price.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        StockEntry.Text = product.Stock.ToString();
    }

    public Task<ProductInput?> Completion => _completion.Task;

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        if (!MoneyParser.TryParse(PriceEntry.Text, out var price) || price <= 0)
        {
            ErrorLabel.Text = "Informe um preço válido maior que zero.";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (!int.TryParse(StockEntry.Text, out var stock))
        {
            ErrorLabel.Text = "Informe uma quantidade válida para o estoque.";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(NameEntry.Text) ||
            string.IsNullOrWhiteSpace(CodeEntry.Text) ||
            string.IsNullOrWhiteSpace(CategoryEntry.Text))
        {
            ErrorLabel.Text = "Preencha nome, código e categoria.";
            ErrorLabel.IsVisible = true;
            return;
        }

        _completion.TrySetResult(new ProductInput
        {
            Name = NameEntry.Text.Trim(),
            Code = CodeEntry.Text.Trim(),
            Category = CategoryEntry.Text.Trim(),
            Price = price,
            Stock = stock
        });
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _completion.TrySetResult(null);
        await Navigation.PopModalAsync();
    }
}
