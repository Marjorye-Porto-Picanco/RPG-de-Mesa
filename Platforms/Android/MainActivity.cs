using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace RPGdeMesa
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                               ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int PedidoSalvarRelatorio = 5102;
        private TaskCompletionSource<Android.Net.Uri?>? _destinoRelatorio;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Window?.DecorView is { } decorView)
            {
                // Fundo escuro nas barras do sistema (mesma cor da barra superior do app)
                decorView.SetBackgroundColor(Android.Graphics.Color.ParseColor("#18100C"));

                // Garante ícones claros na barra de status e de navegação
                var controller = WindowCompat.GetInsetsController(Window, decorView);
                if (controller is not null)
                {
                    controller.AppearanceLightStatusBars = false;
                    controller.AppearanceLightNavigationBars = false;
                }
            }

            var contentView = FindViewById(Android.Resource.Id.Content);
            if (contentView is not null)
            {
                ViewCompat.SetOnApplyWindowInsetsListener(contentView, new SafeAreaInsetsListener());
            }
        }

        internal async Task<Android.Net.Uri?> EscolherDestinoRelatorioAsync(string nome)
        {
            if (_destinoRelatorio is not null)
                return null;

            var pedido = new TaskCompletionSource<Android.Net.Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _destinoRelatorio = pedido;
            try
            {
                using var seletor = new Intent(Intent.ActionCreateDocument);
                seletor.AddCategory(Intent.CategoryOpenable);
                seletor.SetType("text/plain");
                seletor.PutExtra(Intent.ExtraTitle, nome);
                StartActivityForResult(seletor, PedidoSalvarRelatorio);
                return await pedido.Task;
            }
            finally
            {
                _destinoRelatorio = null;
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (requestCode == PedidoSalvarRelatorio)
                _destinoRelatorio?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
        }

        protected override void OnDestroy()
        {
            _destinoRelatorio?.TrySetResult(null);
            base.OnDestroy();
        }

        private sealed class SafeAreaInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View v, WindowInsetsCompat insets)
            {
                var bars = insets.GetInsets(
                    WindowInsetsCompat.Type.SystemBars() |
                    WindowInsetsCompat.Type.DisplayCutout() |
                    WindowInsetsCompat.Type.Ime());

                v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
                return WindowInsetsCompat.Consumed;
            }
        }
    }
}
