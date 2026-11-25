using Android.App;
using Android.Runtime;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            AndroidEnvironment.UnhandledExceptionRaiser += (_, evento) =>
            {
                evento.Handled = FalhasApp.Tratar(evento.Exception, "Interface Android");
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
