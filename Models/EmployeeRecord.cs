using SQLite;

namespace lojavendas.Models;

public static class EmployeeRoles
{
    public const string Administration = "Administração";
    public const string Seller = "Vendedor";
    public const string Manager = "Gestor";

    public static IReadOnlyList<string> All { get; } =
        [Administration, Seller, Manager];
}

[Table("Employees")]
public sealed class EmployeeRecord
{
    [PrimaryKey, MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Role { get; set; } = EmployeeRoles.Seller;

    [MaxLength(80)]
    public string Team { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Ignore]
    public string StatusText => IsActive ? "● Ativo" : "● Inativo";
}

public sealed class EmployeeInput
{
    public string Username { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = EmployeeRoles.Seller;
    public string Team { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
