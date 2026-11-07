using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace lojavendas
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = Android.Views.SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ApplyLightSystemBars();
        }

        protected override void OnResume()
        {
            base.OnResume();
            ApplyLightSystemBars();
        }

        /// <summary>
        /// O fundo do app é claro; deixa os ícones da barra de status e de navegação escuros para
        /// continuarem visíveis.
        /// </summary>
        private void ApplyLightSystemBars()
        {
            if (Window is null)
                return;

            var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
            if (controller is null)
                return;
            controller.AppearanceLightStatusBars = true;
            controller.AppearanceLightNavigationBars = true;
        }
    }
}
