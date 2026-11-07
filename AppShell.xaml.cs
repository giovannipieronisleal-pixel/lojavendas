using lojavendas.Models;
using lojavendas.Pages;
using lojavendas.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace lojavendas
{
    public partial class AppShell : Shell
    {
        private readonly ILocalAuthenticationService _authenticationService;
        private readonly CommerceService _commerceService;
        private readonly ILogger<AppShell> _logger;
        private bool _isRedirecting;
        private bool _dailyBackupStarted;

        public EmployeeRecord? CurrentEmployee => _authenticationService.CurrentEmployee;

        public AppShell(
            MainPage mainPage,
            ILocalAuthenticationService authenticationService,
            IServiceProvider serviceProvider,
            CommerceService commerceService,
            ILogger<AppShell> logger)
        {
            InitializeComponent();
            _authenticationService = authenticationService;
            _commerceService = commerceService;
            _logger = logger;
            MainShellContent.Content = mainPage;
            SalesShellContent.ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<SalesPage>());
            EmployeesShellContent.ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<EmployeesPage>());
            ProductsShellContent.ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<ProductsPage>());
            AnalyticsShellContent.ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<AnalyticsPage>());
            CheckoutShellContent.ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<CheckoutPage>());
            DebtorsShellContent.ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<DebtorsPage>());
            UpdateWorkspaceAccess();
            Navigating += OnShellNavigating;
        }

        public string GetLandingRoute() =>
            CurrentEmployee?.Role == EmployeeRoles.Seller
                ? AppRoutes.Checkout
                : AppRoutes.Sales;

        public async Task SignOutAsync()
        {
            _authenticationService.SignOut();
            UpdateWorkspaceAccess();
            await GoToAsync(AppRoutes.Login);
        }

        public void UpdateWorkspaceAccess()
        {
            var employee = CurrentEmployee;
            var isActive = employee?.IsActive == true;
            var isAdministrator = isActive && employee?.Role == EmployeeRoles.Administration;
            var canAccessSales = isActive &&
                                 employee?.Role is EmployeeRoles.Administration or EmployeeRoles.Manager;
            var canAccessDebtors = canAccessSales;

            SalesShellContent.IsVisible = canAccessSales;
            EmployeesShellContent.IsVisible = isAdministrator;
            ProductsShellContent.IsVisible = isAdministrator;
            AnalyticsShellContent.IsVisible = isAdministrator;
            CheckoutShellContent.IsVisible = isActive;
            DebtorsShellContent.IsVisible = canAccessDebtors;
        }

        private void OnShellNavigating(object? sender, ShellNavigatingEventArgs args)
        {
            var target = args.Target.Location.OriginalString;
            if (!IsWorkspaceRoute(target) ||
                CanAccessRoute(target))
            {
                if (!_dailyBackupStarted &&
                    IsWorkspaceRoute(target) &&
                    CurrentEmployee is not null)
                {
                    _dailyBackupStarted = true;
                    _ = CreateDailyBackupAsync();
                }

                return;
            }

            args.Cancel();
            if (_isRedirecting)
                return;

            _isRedirecting = true;
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    if (CurrentEmployee is null)
                        await GoToAsync(AppRoutes.Login);
                    else
                        await GoToAsync(GetLandingRoute());
                }
                finally
                {
                    _isRedirecting = false;
                }
            });
        }

        private bool CanAccessRoute(string route)
        {
            var employee = CurrentEmployee;
            if (employee is null || !employee.IsActive)
                return false;
            if (employee.Role == EmployeeRoles.Administration)
                return true;

            return employee.Role switch
            {
                EmployeeRoles.Manager => IsAllowedWorkspacePage(route, "sales", "checkout", "debts"),
                EmployeeRoles.Seller => IsAllowedWorkspacePage(route, "checkout"),
                _ => false
            };
        }

        private static bool IsWorkspaceRoute(string route) =>
            GetRouteSegments(route).Any(segment =>
                string.Equals(segment, "workspace", StringComparison.OrdinalIgnoreCase));

        private static bool IsAllowedWorkspacePage(string route, params string[] allowedPages)
        {
            var segments = GetRouteSegments(route);
            var page = segments.LastOrDefault();
            return page is not null && allowedPages.Contains(page, StringComparer.OrdinalIgnoreCase);
        }

        private static string[] GetRouteSegments(string route) =>
            route.Split(['/', '?', '#'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private async Task CreateDailyBackupAsync()
        {
            try
            {
                await _commerceService.CreateDailyBackupIfDueAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Falha ao criar o backup diário ao iniciar uma sessão.");
            }
        }
    }
}
