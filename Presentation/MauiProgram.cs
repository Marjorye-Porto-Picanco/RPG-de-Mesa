using Microsoft.Extensions.Logging;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Infrastructure.Persistence;
using RPGdeMesa.Infrastructure.Repositories;
using RPGdeMesa.Presentation.Services;
using RPGdeMesa.Presentation.ViewModel;

namespace RPGdeMesa
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // 1. CAMADA DE INFRASTRUCTURE (Banco de Dados e Repositórios como Transient)
            builder.Services.AddDbContext<AppDbContext>(ServiceLifetime.Transient);
            builder.Services.AddTransient<IUsuarioRepository, UsuarioRepository>();
            builder.Services.AddTransient<ICampanhaRepository, CampanhaRepository>();
            builder.Services.AddTransient<IMonstroRepository, MonstroRepository>();
            builder.Services.AddTransient<IPersonagemRepository, PersonagemRepository>();
            builder.Services.AddTransient<INpcRepository, NpcRepository>();
            builder.Services.AddTransient<IItemRepository, ItemRepository>();

            // 2. CAMADA DE APPLICATION (Casos de Uso / Services 100% Transient para evitar conflito no EF Core)
            builder.Services.AddTransient<UsuarioService>();
            builder.Services.AddTransient<CampanhaService>();
            builder.Services.AddSingleton<SessaoUsuarioService>(); // Singleton pois gerencia sessão ativa do usuário
            builder.Services.AddTransient<MonstroService>();
            builder.Services.AddTransient<PersonagemService>();
            builder.Services.AddTransient<NpcService>();
            builder.Services.AddTransient<ItemService>();

            // 3. CAMADA DE PRESENTATION / UI (Auth, Storage de Sessão e ViewModels)
            builder.Services.AddScoped<SessaoCampanhaStorageService>(); // Scoped/Singleton atende bem gerenciamento de storage local
            builder.Services.AddTransient<AuthService>();

            // ViewModels (Todas Transient para que cada abertura de página recrie o estado limpo)
            builder.Services.AddTransient<CriarCampanhaViewModel>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<MonstroCriarViewModel>();
            builder.Services.AddTransient<PersonagemCriarViewModel>();
            builder.Services.AddTransient<PersonagemEditarViewModel>();
            builder.Services.AddTransient<BibliotecaGeralViewModel>();
            builder.Services.AddTransient<CampanhasViewModel>();
            builder.Services.AddTransient<SessaoEntrarViewModel>();
            builder.Services.AddTransient<SessaoIniciarViewModel>();

            return builder.Build();
        }
    }
}