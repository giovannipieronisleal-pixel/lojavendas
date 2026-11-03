using lojavendas.Services;
using lojavendas.Pages;
using Microsoft.Extensions.Logging;
using ScottPlot.Maui;

namespace lojavendas
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseScottPlot()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("Inter.ttf", "Inter");
                });

            builder.Services.AddSingleton<ILocalAuthenticationService, LocalAuthenticationService>();
            builder.Services.AddSingleton<CommerceService>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<EmployeesPage>();
            builder.Services.AddTransient<SalesPage>();
            builder.Services.AddTransient<ProductsPage>();
            builder.Services.AddTransient<ProductEditorPage>();
            builder.Services.AddTransient<AnalyticsPage>();
            builder.Services.AddTransient<CheckoutPage>();
            builder.Services.AddTransient<DebtorsPage>();
            builder.Services.AddSingleton<AppShell>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
