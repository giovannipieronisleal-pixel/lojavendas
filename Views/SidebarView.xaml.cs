using lojavendas.Models;

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

    /// <summary>Disparado quando um item do menu é tocado (usado para fechar o menu no celular).</summary>
    public event EventHandler? ItemSelected;

    public SidebarView()
    {
        InitializeComponent();
        StoreNameLabel.Text = Preferences.Default.Get("storeName", "Loja Centro");
        UpdateSelection();
    }

    private static void OnActiveRouteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((SidebarView)bindable).UpdateSelection();
    }

    public void UpdateSelection()
    {
        var employee = (Shell.Current as AppShell)?.CurrentEmployee;
        var role = employee?.Role;
        var isAdministrator = role == EmployeeRoles.Administration;
        var canSeeSales = role is EmployeeRoles.Administration or EmployeeRoles.Manager;

        EmployeesNav.IsVisible = isAdministrator;
        ProductsNav.IsVisible = isAdministrator;
        AnalyticsNav.IsVisible = isAdministrator;
        DebtorsNav.IsVisible = canSeeSales;
        CheckoutNav.IsVisible = employee is not null;
        StoreNameLabel.Text = Preferences.Default.Get("storeName", "Loja Centro");

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
        SetSubItem(CheckoutBackground, CheckoutLabel, ActiveRoute == "checkout");
        SetSubItem(DebtorsBackground, DebtorsLabel, ActiveRoute == "debts");
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

    private static void SetSubItem(Border background, Label label, bool isSelected)
    {
        background.BackgroundColor = isSelected ? Color.FromArgb("#3A5348") : Colors.Transparent;
        label.TextColor = isSelected ? Colors.White : Color.FromArgb("#D7DBD2");
        label.FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
    }

    private async Task NavigateAsync(string route)
    {
        ItemSelected?.Invoke(this, EventArgs.Empty);
        await Shell.Current.GoToAsync(route);
    }

    private async void OnEmployeesClicked(object? sender, EventArgs e) =>
        await NavigateAsync(AppRoutes.Employees);

    private async void OnSalesClicked(object? sender, EventArgs e)
    {
        var route = (Shell.Current as AppShell)?.CurrentEmployee?.Role == EmployeeRoles.Seller
            ? AppRoutes.Checkout
            : AppRoutes.Sales;
        await NavigateAsync(route);
    }

    private async void OnCheckoutClicked(object? sender, EventArgs e) =>
        await NavigateAsync(AppRoutes.Checkout);

    private async void OnDebtorsClicked(object? sender, EventArgs e) =>
        await NavigateAsync(AppRoutes.Debtors);

    private async void OnProductsClicked(object? sender, EventArgs e) =>
        await NavigateAsync(AppRoutes.Products);

    private async void OnAnalyticsClicked(object? sender, EventArgs e) =>
        await NavigateAsync(AppRoutes.Analytics);

    private async void OnStoreNameClicked(object? sender, EventArgs e)
    {
        var currentName = StoreNameLabel.Text;
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
        StoreNameLabel.Text = newName;
    }
}
