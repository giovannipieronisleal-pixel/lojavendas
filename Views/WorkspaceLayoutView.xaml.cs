namespace lojavendas.Views;

public partial class WorkspaceLayoutView : ContentView
{
    public static readonly BindableProperty ActiveRouteProperty =
        BindableProperty.Create(
            nameof(ActiveRoute),
            typeof(string),
            typeof(WorkspaceLayoutView),
            "sales",
            propertyChanged: OnActiveRouteChanged);

    public static readonly BindableProperty BreadcrumbProperty =
        BindableProperty.Create(
            nameof(Breadcrumb),
            typeof(string),
            typeof(WorkspaceLayoutView),
            string.Empty,
            propertyChanged: OnBreadcrumbChanged);

    public static readonly BindableProperty BodyProperty =
        BindableProperty.Create(
            nameof(Body),
            typeof(View),
            typeof(WorkspaceLayoutView),
            propertyChanged: OnBodyChanged);

    public string ActiveRoute
    {
        get => (string)GetValue(ActiveRouteProperty);
        set => SetValue(ActiveRouteProperty, value);
    }

    public string Breadcrumb
    {
        get => (string)GetValue(BreadcrumbProperty);
        set => SetValue(BreadcrumbProperty, value);
    }

    public View Body
    {
        get => (View)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public WorkspaceLayoutView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        UpdateProfile();
    }

    private void UpdateProfile()
    {
        var employee = (Shell.Current as AppShell)?.CurrentEmployee;
        if (employee is null)
        {
            ProfileNameLabel.Text = "Usuário";
            ProfileRoleLabel.Text = string.Empty;
            ProfileInitialsLabel.Text = "?";
            return;
        }

        ProfileNameLabel.Text = employee.Name;
        ProfileRoleLabel.Text = employee.Role;
        var initials = string.Join(
            string.Empty,
            employee.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0])));
        ProfileInitialsLabel.Text = initials;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        UpdateProfile();
        if (Shell.Current is not { } shell)
            return;

        shell.Navigated -= OnShellNavigated;
        shell.Navigated += OnShellNavigated;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (Shell.Current is { } shell)
            shell.Navigated -= OnShellNavigated;
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e) =>
        UpdateProfile();

    private void OnLayoutSizeChanged(object? sender, EventArgs e)
    {
        var isCompact = Width < 820;
        LayoutGrid.ColumnDefinitions[0].Width = isCompact ? new GridLength(0) : new GridLength(232);
        Sidebar.IsVisible = !isCompact;
        MobileNavButton.IsVisible = isCompact;
        ProfileDetails.IsVisible = !isCompact;
        HeaderGrid.Padding = isCompact ? new Thickness(12, 0) : new Thickness(24, 0);
        BodyHost.Padding = isCompact ? new Thickness(16, 16) : new Thickness(24, 20);
        ContentGrid.RowDefinitions[0].Height = isCompact ? 64 : 80;
    }

    private async void OnProfileClicked(object? sender, EventArgs e)
    {
        UpdateProfile();
        var action = await Shell.Current.DisplayActionSheetAsync(
            ProfileNameLabel.Text,
            "Cancelar",
            null,
            "Sair");
        if (action != "Sair")
            return;

        var shouldSignOut = await Shell.Current.DisplayAlertAsync(
            "Sair da conta",
            "Deseja encerrar sua sessão?",
            "Sair",
            "Continuar conectado");
        if (shouldSignOut && Shell.Current is AppShell shell)
            await shell.SignOutAsync();
    }

    private async void OnMobileNavigationClicked(object? sender, EventArgs e)
    {
        var employee = (Shell.Current as AppShell)?.CurrentEmployee;
        if (employee is null)
            return;

        var options = new List<(string Label, string Route)>
        {
            ("Vendas", employee.Role == lojavendas.Models.EmployeeRoles.Seller
                ? "//workspace/main/checkout"
                : "//workspace/main/sales"),
            ("Caixa", "//workspace/main/checkout")
        };
        if (employee.Role != lojavendas.Models.EmployeeRoles.Seller)
            options.Add(("Devedores", "//workspace/main/debts"));
        if (employee.Role == lojavendas.Models.EmployeeRoles.Administration)
        {
            options.Add(("Funcionários", "//workspace/main/employees"));
            options.Add(("Produtos", "//workspace/main/products"));
            options.Add(("Análise de vendas", "//workspace/main/analytics"));
        }

        var selected = await Shell.Current.DisplayActionSheetAsync(
            "Navegar",
            "Cancelar",
            null,
            options.Select(option => option.Label).ToArray());
        var destination = options.FirstOrDefault(option => option.Label == selected);
        if (destination.Route is not null)
            await Shell.Current.GoToAsync(destination.Route);
    }

    private static void OnActiveRouteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var layout = (WorkspaceLayoutView)bindable;
        if (layout.Sidebar is not null)
            layout.Sidebar.ActiveRoute = (string)newValue;
    }

    private static void OnBreadcrumbChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var layout = (WorkspaceLayoutView)bindable;
        if (layout.BreadcrumbLabel is not null)
            layout.BreadcrumbLabel.Text = (string)newValue;
    }

    private static void OnBodyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var layout = (WorkspaceLayoutView)bindable;
        if (layout.BodyHost is not null)
            layout.BodyHost.Content = (View)newValue;
    }
}
