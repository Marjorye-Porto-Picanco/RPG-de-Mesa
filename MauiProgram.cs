using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGdeMesa.Data;
using RPGdeMesa.Services;
using RPGdeMesa.ViewModel;

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
            // Registro do Banco de Dados
            builder.Services.AddDbContext<AppDbContext>(ServiceLifetime.Transient);

            // Registro dos Serviços 
            builder.Services.AddTransient<UsuarioService>();
            builder.Services.AddTransient<CampanhaService>();
            builder.Services.AddTransient<AuthService>();

            // Registro dos ViewModels
            builder.Services.AddTransient<LoginViewModel>(); 
            builder.Services.AddTransient<RegisterViewModel>();


            return builder.Build();
        }
    }
}