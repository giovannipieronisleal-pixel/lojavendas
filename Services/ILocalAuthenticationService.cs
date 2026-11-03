using lojavendas.Models;

namespace lojavendas.Services;

public interface ILocalAuthenticationService
{
    EmployeeRecord? CurrentEmployee { get; }
    Task<bool> AuthenticateAsync(string username, string password);
    void SignOut();
    Task<IReadOnlyList<EmployeeRecord>> GetEmployeesAsync();
    Task CreateEmployeeAsync(EmployeeInput employee);
    Task UpdateEmployeeAsync(EmployeeInput employee);
}
