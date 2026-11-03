using lojavendas.Models;

namespace lojavendas.Pages;

public partial class EmployeeEditorPage : ContentPage
{
    private readonly TaskCompletionSource<EmployeeInput?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _isEditing;

    public EmployeeEditorPage(EmployeeRecord? employee = null)
    {
        InitializeComponent();
        _isEditing = employee is not null;
        TitleLabel.Text = _isEditing ? "Editar funcionário" : "Criar funcionário";
        PasswordLabel.Text = _isEditing ? "Nova senha (opcional)" : "Senha";
        PasswordEntry.Placeholder = _isEditing ? "Deixe em branco para não alterar" : "Crie uma senha";
        PasswordHint.IsVisible = _isEditing;
        var roles = EmployeeRoles.All.ToList();
        RolePicker.ItemsSource = roles;
        RolePicker.SelectedIndex = employee is null
            ? roles.IndexOf(EmployeeRoles.Seller)
            : roles.IndexOf(employee.Role);

        if (employee is not null)
        {
            NameEntry.Text = employee.Name;
            UsernameEntry.Text = employee.Username;
            UsernameEntry.IsEnabled = false;
            TeamEntry.Text = employee.Team;
            TeamEntry.IsEnabled = true;
            RolePicker.IsEnabled = true;
            ActiveSwitch.IsToggled = employee.IsActive;
            UpdateActiveStatusLabel();
        }

        ActiveSwitch.Toggled += (_, args) =>
            ActiveStatusLabel.Text = args.Value ? "Ativo" : "Inativo";
    }

    public Task<EmployeeInput?> Completion => _completion.Task;

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        if (string.IsNullOrWhiteSpace(NameEntry.Text) ||
            string.IsNullOrWhiteSpace(UsernameEntry.Text) ||
            string.IsNullOrWhiteSpace(TeamEntry.Text) ||
            RolePicker.SelectedItem is not string role ||
            (!_isEditing && string.IsNullOrWhiteSpace(PasswordEntry.Text)))
        {
            ErrorLabel.Text = "Preencha nome, usuário, equipe, cargo e senha.";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(PasswordEntry.Text) && PasswordEntry.Text.Length < 4)
        {
            ErrorLabel.Text = "A senha deve ter pelo menos 4 caracteres.";
            ErrorLabel.IsVisible = true;
            return;
        }

        _completion.TrySetResult(new EmployeeInput
        {
            Username = UsernameEntry.Text.Trim(),
            Name = NameEntry.Text.Trim(),
            Password = PasswordEntry.Text ?? string.Empty,
            Role = role,
            Team = TeamEntry.Text.Trim(),
            IsActive = ActiveSwitch.IsToggled
        });
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _completion.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    private void UpdateActiveStatusLabel() =>
        ActiveStatusLabel.Text = ActiveSwitch.IsToggled ? "Ativo" : "Inativo";
}
