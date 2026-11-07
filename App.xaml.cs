namespace lojavendas
{
    public partial class App : Application
    {
        private readonly AppShell _appShell;

        public App(AppShell appShell)
        {
            InitializeComponent();
            // O visual do app foi desenhado só para o tema claro. Sem isso, celulares em modo escuro
            // trocam as cores padrão (texto branco em campos brancos, fundo preto etc.).
            UserAppTheme = AppTheme.Light;
            _appShell = appShell;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_appShell)
            {
                Title = "Loja Vendas",
                // Tamanho mínimo no Windows para a interface não se desmontar em janelas minúsculas.
                MinimumWidth = 360,
                MinimumHeight = 560
            };
            return window;
        }
    }
}
