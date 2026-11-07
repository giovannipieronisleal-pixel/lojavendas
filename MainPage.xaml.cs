using lojavendas.Services;
using lojavendas.Models;
using Microsoft.Extensions.Logging;

namespace lojavendas
{
    public partial class MainPage : ContentPage
    {
        private bool _isPasswordVisible;
        private readonly ILocalAuthenticationService _authenticationService;
        private readonly ILogger<MainPage> _logger;

        public MainPage(
            ILocalAuthenticationService authenticationService,
            ILogger<MainPage> logger)
        {
            InitializeComponent();
            _authenticationService = authenticationService;
            _logger = logger;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ResetLoginForm();
        }

        private void ResetLoginForm()
        {
            LoginEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;
            _isPasswordVisible = false;
            PasswordEntry.IsPassword = true;
            PasswordVisibilityButton.Text = "Mostrar";
            SemanticProperties.SetDescription(PasswordVisibilityButton, "Mostrar senha");
        }

        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            if (Width <= 0)
                return;

            var isCompact = Width < 520;
            TitleLabel.FontSize = isCompact ? 32 : 44;
        }

        private void OnTogglePasswordVisibility(object? sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            PasswordEntry.IsPassword = !_isPasswordVisible;
            PasswordVisibilityButton.Text = _isPasswordVisible ? "Ocultar" : "Mostrar";
            SemanticProperties.SetDescription(
                PasswordVisibilityButton,
                _isPasswordVisible ? "Ocultar senha" : "Mostrar senha");
        }

        private void OnPasswordCompleted(object? sender, EventArgs e)
        {
            OnAccessClicked(sender, e);
        }

        private async void OnAccessClicked(object? sender, EventArgs e)
        {
            var username = LoginEntry.Text?.Trim();
            var password = PasswordEntry.Text;
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlertAsync(
                    "Campos obrigatórios",
                    "Informe seu login e sua senha para continuar.",
                    "OK");
                return;
            }

            AcessButton.IsEnabled = false;
            try
            {
                bool isAuthenticated;
                try
                {
                    isAuthenticated = await _authenticationService.AuthenticateAsync(username, password);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Falha ao autenticar usando o banco SQLite local.");
                    await DisplayAlertAsync(
                        "Erro de autenticação",
                        "Não foi possível acessar o banco de dados local. Tente novamente.",
                        "OK");
                    return;
                }

                if (!isAuthenticated)
                {
                    ResetLoginForm();
                    await DisplayAlertAsync(
                        "Login inválido",
                        "Confira seu login e senha e tente novamente.",
                        "OK");
                    LoginEntry.Focus();
                    return;
                }

                try
                {
                    if (Shell.Current is AppShell appShell)
                        appShell.UpdateWorkspaceAccess();

                    var destination = _authenticationService.CurrentEmployee?.Role == EmployeeRoles.Seller
                        ? AppRoutes.Checkout
                        : AppRoutes.Sales;
                    await Shell.Current.GoToAsync(destination);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Falha ao navegar para a tela de vendas após autenticação.");
                    await DisplayAlertAsync(
                        "Erro de navegação",
                        "O login foi validado, mas não foi possível abrir a tela de vendas.",
                        "OK");
                }
            }
            finally
            {
                AcessButton.IsEnabled = true;
            }
        }
    }
}
