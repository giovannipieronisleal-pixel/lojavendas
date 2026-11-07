namespace lojavendas;

/// <summary>
/// Rotas absolutas de navegação do Shell. Cada página da área interna fica em sua própria seção
/// para que o Android não crie abas superiores automaticamente.
/// </summary>
public static class AppRoutes
{
    public const string Login = "//login/main/form";
    public const string Sales = "//workspace/sales";
    public const string Checkout = "//workspace/checkout";
    public const string Debtors = "//workspace/debts";
    public const string Employees = "//workspace/employees";
    public const string Products = "//workspace/products";
    public const string Analytics = "//workspace/analytics";
}
