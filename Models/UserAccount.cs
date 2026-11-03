using SQLite;

namespace lojavendas.Models;

[Table("Users")]
public sealed class UserAccount
{
    [PrimaryKey, MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(32)]
    public string PasswordSalt { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PasswordHash { get; set; } = string.Empty;
}
