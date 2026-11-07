using lojavendas.Models;

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
        MobileSidebar.ItemSelected += (_, _) => MobileDrawerOverlay.IsVisible = false;
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

        Sidebar.UpdateSelection();
        MobileSidebar.UpdateSelection();
        UpdateResponsiveLayout();
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

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        MobileDrawerOverlay.IsVisible = false;
        UpdateProfile();
    }

    private void OnLayoutSizeChanged(object? sender, EventArgs e) =>
        UpdateResponsiveLayout();

    private void UpdateResponsiveLayout()
    {
        if (Width <= 0)
            return;

        var isCompact = Width < 860;
        var isSeller = (Shell.Current as AppShell)?.CurrentEmployee?.Role == EmployeeRoles.Seller;
        var showFixedSidebar = !isCompact && !isSeller;

        LayoutGrid.ColumnDefinitions[0].Width = showFixedSidebar ? new GridLength(240) : new GridLength(0);
        Sidebar.IsVisible = showFixedSidebar;
        MobileNavButton.IsVisible = isCompact && !isSeller;
        if (!isCompact || isSeller)
            MobileDrawerOverlay.IsVisible = false;

        AreaLabel.IsVisible = !isCompact;
        BreadcrumbSeparator.IsVisible = !isCompact;
        ProfileDetails.IsVisible = !isCompact;
        HeaderGrid.Padding = isCompact ? new Thickness(12, 0) : new Thickness(24, 0);
        BodyHost.Padding = isCompact ? new Thickness(14, 14, 14, 28) : new Thickness(28, 22, 28, 32);
        ContentGrid.RowDefinitions[0].Height = isCompact ? 56 : 68;
    }

    private async void OnProfileClicked(object? sender, EventArgs e)
    {
        UpdateProfile();
        var action = await Shell.Current.DisplayActionSheetAsync(
            $"{ProfileNameLabel.Text} ({ProfileRoleLabel.Text})",
            "Cancelar",
            null,
            "Sair da conta");
        if (action != "Sair da conta")
            return;

        var shouldSignOut = await Shell.Current.DisplayAlertAsync(
            "Sair da conta",
            "Deseja encerrar sua sessão?",
            "Sair",
            "Continuar conectado");
        if (shouldSignOut && Shell.Current is AppShell shell)
            await shell.SignOutAsync();
    }

    private void OnMobileNavigationClicked(object? sender, EventArgs e)
    {
        MobileSidebar.UpdateSelection();
        MobileDrawerOverlay.IsVisible = !MobileDrawerOverlay.IsVisible;
    }

    private void OnDrawerBackdropTapped(object? sender, TappedEventArgs e) =>
        MobileDrawerOverlay.IsVisible = false;

    private static void OnActiveRouteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var layout = (WorkspaceLayoutView)bindable;
        var route = (string)newValue;
        if (layout.Sidebar is not null)
            layout.Sidebar.ActiveRoute = route;
        if (layout.MobileSidebar is not null)
            layout.MobileSidebar.ActiveRoute = route;
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
