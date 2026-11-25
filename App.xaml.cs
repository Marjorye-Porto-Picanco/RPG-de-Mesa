using Microsoft.Maui.Controls;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa;

public partial class App : Microsoft.Maui.Controls.Application
{
    private Window? _janela;

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var relatorio = FalhasApp.LerPendente();
        Page pagina;
        try
        {
            pagina = relatorio is null ? new MainPage() : new TelaErroPage(relatorio);
        }
        catch (Exception erro) when (relatorio is null)
        {
            pagina = new TelaErroPage(FalhasApp.Registrar(erro, "Abertura da janela"));
        }

        var window = new Window(pagina) { Title = "RPGdeMesa" };
        _janela = window;
        window.Destroying += (_, _) =>
        {
            if (ReferenceEquals(_janela, window))
                _janela = null;
        };

#if WINDOWS
        window.Created += (s, e) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            }
        };
#endif

        return window;
    }

    internal bool MostrarFalha(RelatorioFalha relatorio)
    {
        if (_janela is not { } janela)
            return false;

        return janela.Dispatcher.Dispatch(() =>
        {
            try
            {
                if (janela.Page is not TelaErroPage)
                    janela.Page = new TelaErroPage(relatorio);
            }
            catch
            {
                // O TXT já foi registrado antes de agendar a troca de página.
            }
        });
    }
}
