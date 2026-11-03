using System.Security.Cryptography;
using lojavendas.Models;
using SQLite;

namespace lojavendas.Services;

public sealed class LocalAuthenticationService : ILocalAuthenticationService
{
    private const int PasswordHashIterations = 210_000;
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public EmployeeRecord? CurrentEmployee { get; private set; }

    public LocalAuthenticationService()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "lojavendas.db3");
        _database = new SQLiteAsyncConnection(databasePath);
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        await EnsureInitializedAsync();

        var normalizedUsername = username.Trim();
        var account = await _database.FindAsync<UserAccount>(normalizedUsername);
        if (account is null)
        {
            CurrentEmployee = null;
            return false;
        }

        var employee = await _database.FindAsync<EmployeeRecord>(normalizedUsername);
        if (employee is null || !employee.IsActive)
        {
            CurrentEmployee = null;
            return false;
        }

        var salt = Convert.FromBase64String(account.PasswordSalt);
        var expectedHash = Convert.FromBase64String(account.PasswordHash);
        var actualHash = HashPassword(password, salt);

        var isAuthenticated = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        CurrentEmployee = isAuthenticated ? employee : null;
        return isAuthenticated;
    }

    public void SignOut() => CurrentEmployee = null;

    public async Task<IReadOnlyList<EmployeeRecord>> GetEmployeesAsync()
    {
        await EnsureInitializedAsync();
        EnsureAdministrativeAccess();
        return await _database.Table<EmployeeRecord>()
            .OrderBy(employee => employee.Name)
            .ToListAsync();
    }

    public async Task CreateEmployeeAsync(EmployeeInput input)
    {
        await EnsureInitializedAsync();
        EnsureAdministrativeAccess();
        ValidateEmployee(input, requirePassword: true);

        var salt = RandomNumberGenerator.GetBytes(16);
        var account = new UserAccount
        {
            Username = input.Username.Trim(),
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(HashPassword(input.Password, salt))
        };
        var employee = ToEmployeeRecord(input);

        await _database.RunInTransactionAsync(connection =>
        {
            if (connection.Find<UserAccount>(account.Username) is not null ||
                connection.Find<EmployeeRecord>(employee.Username) is not null)
            {
                throw new InvalidOperationException("Já existe um funcionário com esse usuário de acesso.");
            }

            connection.Insert(account);
            connection.Insert(employee);
        });
    }

    public async Task UpdateEmployeeAsync(EmployeeInput input)
    {
        await EnsureInitializedAsync();
        EnsureAdministrativeAccess();
        ValidateEmployee(input, requirePassword: false);

        await _database.RunInTransactionAsync(connection =>
        {
            var employee = connection.Find<EmployeeRecord>(input.Username.Trim())
                ?? throw new InvalidOperationException("O funcionário não foi encontrado.");
            var account = connection.Find<UserAccount>(employee.Username)
                ?? throw new InvalidOperationException("A conta de acesso do funcionário não foi encontrada.");

            if (CurrentEmployee?.Username == employee.Username &&
                (!input.IsActive || input.Role != EmployeeRoles.Administration))
            {
                throw new InvalidOperationException("Não é possível remover seu próprio acesso administrativo durante esta sessão.");
            }

            if (employee.Role == EmployeeRoles.Administration &&
                employee.IsActive &&
                (input.Role != EmployeeRoles.Administration || !input.IsActive))
            {
                var activeAdministrators = connection.Table<EmployeeRecord>()
                    .Where(item => item.Role == EmployeeRoles.Administration && item.IsActive)
                    .Count();
                if (activeAdministrators <= 1)
                    throw new InvalidOperationException("Não é possível desativar ou rebaixar o último funcionário administrativo ativo.");
            }

            employee.Name = input.Name.Trim();
            employee.Role = input.Role;
            employee.Team = input.Team.Trim();
            employee.IsActive = input.IsActive;
            connection.Update(employee);

            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                var salt = RandomNumberGenerator.GetBytes(16);
                account.PasswordSalt = Convert.ToBase64String(salt);
                account.PasswordHash = Convert.ToBase64String(HashPassword(input.Password, salt));
                connection.Update(account);
            }

            if (CurrentEmployee?.Username == employee.Username)
                CurrentEmployee = employee;
        });
    }

    private static void ValidateEmployee(EmployeeInput input, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(input.Username) || input.Username.Trim().Length > 64)
            throw new ArgumentException("Informe um usuário de acesso com até 64 caracteres.");
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120)
            throw new ArgumentException("Informe o nome do funcionário com até 120 caracteres.");
        if (string.IsNullOrWhiteSpace(input.Role) || !EmployeeRoles.All.Contains(input.Role))
            throw new ArgumentException("Selecione um cargo válido.");
        if (string.IsNullOrWhiteSpace(input.Team) || input.Team.Trim().Length > 80)
            throw new ArgumentException("Informe a equipe com até 80 caracteres.");
        if (requirePassword && string.IsNullOrWhiteSpace(input.Password))
            throw new ArgumentException("Informe uma senha para o funcionário.");
        if (!string.IsNullOrEmpty(input.Password) && input.Password.Length < 4)
            throw new ArgumentException("A senha deve ter pelo menos 4 caracteres.");
    }

    private void EnsureAdministrativeAccess()
    {
        if (CurrentEmployee?.Role != EmployeeRoles.Administration || !CurrentEmployee.IsActive)
            throw new UnauthorizedAccessException("Somente funcionários administrativos ativos podem gerenciar funcionários.");
    }

    private static EmployeeRecord ToEmployeeRecord(EmployeeInput input) => new()
    {
        Username = input.Username.Trim(),
        Name = input.Name.Trim(),
        Role = input.Role,
        Team = input.Team.Trim(),
        IsActive = input.IsActive
    };

    private async Task EnsureAdministratorEmployeeAsync()
    {
        var employee = await _database.FindAsync<EmployeeRecord>("admin");
        if (employee is not null)
            return;

        await _database.InsertAsync(new EmployeeRecord
        {
            Username = "admin",
            Name = "Administrador",
            Role = EmployeeRoles.Administration,
            Team = "Gestão",
            IsActive = true
        });
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            await _database.CreateTableAsync<UserAccount>();
            await _database.CreateTableAsync<EmployeeRecord>();

            var admin = await _database.FindAsync<UserAccount>("admin");
            if (admin is null)
            {
                var salt = RandomNumberGenerator.GetBytes(16);
                var account = new UserAccount
                {
                    Username = "admin",
                    PasswordSalt = Convert.ToBase64String(salt),
                    PasswordHash = Convert.ToBase64String(HashPassword("admin", salt))
                };
                await _database.InsertAsync(account);
            }

            await EnsureAdministratorEmployeeAsync();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            32);
    }
}
