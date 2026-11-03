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
            Navigating += OnShellNavigating;
        }

        public string GetLandingRoute() =>
            CurrentEmployee?.Role == EmployeeRoles.Seller
                ? "//workspace/main/checkout"
                : "//workspace/main/sales";

        public async Task SignOutAsync()
        {
            _authenticationService.SignOut();
            await GoToAsync("//login/main/form");
        }

        private void OnShellNavigating(object? sender, ShellNavigatingEventArgs args)
        {
            var target = args.Target.Location.OriginalString;
            if (!target.Contains("/workspace/", StringComparison.OrdinalIgnoreCase) ||
                CanAccessRoute(target))
            {
                if (!_dailyBackupStarted &&
                    target.Contains("/workspace/", StringComparison.OrdinalIgnoreCase) &&
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
                        await GoToAsync("//login/main/form");
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

            var isSalesArea = route.Contains("/sales", StringComparison.OrdinalIgnoreCase) ||
                              route.Contains("/checkout", StringComparison.OrdinalIgnoreCase) ||
                              route.Contains("/debts", StringComparison.OrdinalIgnoreCase);
            return employee.Role switch
            {
                EmployeeRoles.Manager => isSalesArea,
                EmployeeRoles.Seller => route.Contains("/checkout", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

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
