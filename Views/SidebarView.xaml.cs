namespace lojavendas.Views;

public partial class SidebarView : ContentView
{
    public static readonly BindableProperty ActiveRouteProperty =
        BindableProperty.Create(
            nameof(ActiveRoute),
            typeof(string),
            typeof(SidebarView),
            "sales",
            propertyChanged: OnActiveRouteChanged);

    public string ActiveRoute
    {
        get => (string)GetValue(ActiveRouteProperty);
        set => SetValue(ActiveRouteProperty, value);
    }

    public SidebarView()
    {
        InitializeComponent();
        StoreNameButton.Text = Preferences.Default.Get("storeName", "Loja Centro");
        UpdateSelection();
    }

    private static void OnActiveRouteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((SidebarView)bindable).UpdateSelection();
    }

    private void UpdateSelection()
    {
        var employee = (Shell.Current as AppShell)?.CurrentEmployee;
        var isAdministrator = employee?.Role == lojavendas.Models.EmployeeRoles.Administration;
        EmployeesNav.IsVisible = isAdministrator;
        ProductsNav.IsVisible = isAdministrator;
        AnalyticsNav.IsVisible = isAdministrator;

        var assetSet = ActiveRoute is "employees" ? "employees"
            : ActiveRoute is "products" ? "products"
            : ActiveRoute is "analytics" ? "analytics"
            : "sales";
        var selectedColor = Color.FromArgb("#D4E6B0");
        var normalColor = Colors.Transparent;

        SetItem(EmployeesBackground, EmployeesIcon, EmployeesLabel, "users", assetSet, ActiveRoute == "employees", selectedColor, normalColor);
        SetItem(SalesBackground, SalesIcon, SalesLabel, "sales", assetSet, ActiveRoute is "sales" or "checkout" or "debts", selectedColor, normalColor);
        SetItem(ProductsBackground, ProductsIcon, ProductsLabel, "products", assetSet, ActiveRoute == "products", selectedColor, normalColor);
        SetItem(AnalyticsBackground, AnalyticsIcon, AnalyticsLabel, "analytics", assetSet, ActiveRoute == "analytics", selectedColor, normalColor);
    }

    private static void SetItem(
        Border background,
        Image icon,
        Label label,
        string iconName,
        string assetSet,
        bool isSelected,
        Color selectedColor,
        Color normalColor)
    {
        background.BackgroundColor = isSelected ? selectedColor : normalColor;
        icon.Source = $"nav_{iconName}_{assetSet}.svg";
        icon.Opacity = isSelected ? 1 : 0.88;
        label.TextColor = isSelected ? Color.FromArgb("#293F35") : Color.FromArgb("#F3F4E9");
    }

    private async void OnEmployeesClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//workspace/main/employees");

    private async void OnSalesClicked(object? sender, EventArgs e)
    {
        var route = (Shell.Current as AppShell)?.CurrentEmployee?.Role == lojavendas.Models.EmployeeRoles.Seller
            ? "//workspace/main/checkout"
            : "//workspace/main/sales";
        await Shell.Current.GoToAsync(route);
    }

    private async void OnProductsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//workspace/main/products");

    private async void OnAnalyticsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//workspace/main/analytics");

    private async void OnStoreNameClicked(object? sender, EventArgs e)
    {
        var currentName = StoreNameButton.Text;
        var newName = await Shell.Current.DisplayPromptAsync(
            "Nome da loja",
            "Digite o nome que será exibido no menu.",
            accept: "Salvar",
            cancel: "Cancelar",
            maxLength: 40,
            initialValue: currentName);

        if (newName is null)
            return;

        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            await Shell.Current.DisplayAlertAsync("Nome inválido", "Informe um nome para a loja.", "OK");
            return;
        }

        Preferences.Default.Set("storeName", newName);
        StoreNameButton.Text = newName;
    }
}
