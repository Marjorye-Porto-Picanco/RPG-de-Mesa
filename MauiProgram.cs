using Microsoft.Extensions.Logging;
using RPGdeMesa.Data;
using RPGdeMesa.Services;
using Microsoft.EntityFrameworkCore;

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

            // Registro dos Serviços (A MÁGICA ACONTECE AQUI)
            builder.Services.AddTransient<UsuarioService>();
            builder.Services.AddTransient<CampanhaService>(); // <--- ADICIONAMOS ESTA LINHA!

            return builder.Build();
        }
    }
}