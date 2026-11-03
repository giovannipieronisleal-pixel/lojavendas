using System.Collections.ObjectModel;
using lojavendas.Models;
using lojavendas.Services;
using Microsoft.Extensions.Logging;

namespace lojavendas.Pages;

public partial class EmployeesPage : ContentPage
{
    private readonly ILocalAuthenticationService _authenticationService;
    private readonly ILogger<EmployeesPage> _logger;
    private readonly ObservableCollection<EmployeeRecord> _employees = [];

    public EmployeesPage(
        ILocalAuthenticationService authenticationService,
        ILogger<EmployeesPage> logger)
    {
        InitializeComponent();
        _authenticationService = authenticationService;
        _logger = logger;
        Employees = _employees;
        BindingContext = this;
    }

    public ObservableCollection<EmployeeRecord> Employees { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadEmployeesAsync();
    }

    private async Task LoadEmployeesAsync()
    {
        try
        {
            var employees = await _authenticationService.GetEmployeesAsync();
            _employees.Clear();
            foreach (var employee in employees)
                _employees.Add(employee);

            TotalCountLabel.Text = _employees.Count.ToString();
            ActiveCountLabel.Text = _employees.Count(employee => employee.IsActive).ToString();
            InactiveCountLabel.Text = _employees.Count(employee => !employee.IsActive).ToString();
            ListCountLabel.Text = $"{_employees.Count} funcionários";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar funcionários do banco SQLite.");
            await DisplayAlertAsync(
                "Erro ao carregar funcionários",
                "Não foi possível carregar os funcionários. Verifique sua permissão e tente novamente.",
                "OK");
        }
    }

    private async void OnNewEmployeeClicked(object? sender, EventArgs e) =>
        await OpenEmployeeEditorAsync(null);

    private async void OnEditEmployeeClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: EmployeeRecord employee })
            await OpenEmployeeEditorAsync(employee);
    }

    private async Task OpenEmployeeEditorAsync(EmployeeRecord? employee)
    {
        var editor = new EmployeeEditorPage(employee);
        await Navigation.PushModalAsync(editor);
        var input = await editor.Completion;
        if (input is null)
            return;

        try
        {
            if (employee is null)
                await _authenticationService.CreateEmployeeAsync(input);
            else
                await _authenticationService.UpdateEmployeeAsync(input);

            await LoadEmployeesAsync();
        }
        catch (ArgumentException exception)
        {
            await DisplayAlertAsync("Dados inválidos", exception.Message, "OK");
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Não foi possível salvar", exception.Message, "OK");
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Usuário sem permissão tentou gerenciar funcionários.");
            await DisplayAlertAsync("Acesso negado", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao salvar funcionário no banco SQLite.");
            await DisplayAlertAsync(
                "Erro ao salvar funcionário",
                "O funcionário não foi salvo. Verifique o banco local e tente novamente.",
                "OK");
        }
    }
}
