using Microsoft.Extensions.Logging;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Infrastructure.Persistence;
using RPGdeMesa.Infrastructure.Repositories;
using RPGdeMesa.Presentation.Services; // AuthService nativo do MAUI na pasta Presentation/Services
using RPGdeMesa.Presentation.ViewModel; // ViewModels na pasta Presentation/ViewModel

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

            // 1. CAMADA DE INFRASTRUCTURE (Banco de Dados e Repositórios)
            builder.Services.AddDbContext<AppDbContext>(ServiceLifetime.Transient);
            builder.Services.AddTransient<IUsuarioRepository, UsuarioRepository>();
            builder.Services.AddTransient<ICampanhaRepository, CampanhaRepository>();
            builder.Services.AddScoped<IMonstroRepository, MonstroRepository>();
            builder.Services.AddScoped<IPersonagemRepository, PersonagemRepository>();

            // 2. CAMADA DE APPLICATION (Casos de Uso / Application Services)
            builder.Services.AddTransient<UsuarioService>();
            builder.Services.AddTransient<CampanhaService>();
            builder.Services.AddSingleton<SessaoUsuarioService>();
            builder.Services.AddScoped<MonstroService>();
            builder.Services.AddScoped<PersonagemService>();

            // 3. CAMADA DE PRESENTATION / UI (Auth Nativo MAUI e ViewModels)
            builder.Services.AddTransient<AuthService>();
            builder.Services.AddTransient<CriarCampanhaViewModel>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddScoped<MonstroCriarViewModel>();
            builder.Services.AddScoped<PersonagemCriarViewModel>();
            builder.Services.AddScoped<PersonagemEditarViewModel>();

            return builder.Build();
        }
    }
}