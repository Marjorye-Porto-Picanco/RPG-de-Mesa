using Microsoft.UI.Xaml;
// Remova o using RPGdeMesa.Presentation se não for estritamente necessário aqui, 
// pois o MauiProgram já é chamado diretamente abaixo.

namespace RPGdeMesa.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => RPGdeMesa.MauiProgram.CreateMauiApp();
}