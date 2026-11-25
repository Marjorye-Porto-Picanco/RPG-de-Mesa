using System.Text;
using Microsoft.Maui.Accessibility;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa;

internal sealed class TelaErroPage : ContentPage
{
    private readonly RelatorioFalha _relatorio;
    private readonly Button _salvar;
    private readonly Button _fechar;
    private readonly Label _status;

    internal TelaErroPage(RelatorioFalha relatorio)
    {
        _relatorio = relatorio;
        Title = "Erro";
        BackgroundColor = Color.FromArgb("#20150F");

        var imagem = new Image
        {
            Source = "mascote_erro.png",
            Aspect = Aspect.AspectFit,
            HeightRequest = 270,
            HorizontalOptions = LayoutOptions.Fill
        };

        var titulo = new Label
        {
            Text = "ERRO",
            FontSize = 52,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF7470"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        SemanticProperties.SetHeadingLevel(titulo, SemanticHeadingLevel.Level1);

        _salvar = new Button
        {
            Text = "Salvar log (.txt)",
            MinimumHeightRequest = 48,
            BackgroundColor = Color.FromArgb("#E8C481"),
            TextColor = Color.FromArgb("#23170D"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 17,
            CornerRadius = 9
        };
        _salvar.Clicked += SalvarAsync;

        _fechar = new Button
        {
#if IOS
            Text = "Concluir",
#else
            Text = "Fechar aplicativo",
#endif
            MinimumHeightRequest = 48,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#F3E5D1"),
            BorderColor = Color.FromArgb("#A38463"),
            BorderWidth = 1,
            FontSize = 16,
            CornerRadius = 9
        };
        _fechar.Clicked += Fechar;

        _status = new Label
        {
            Text = relatorio.SalvoLocalmente
                ? "Salve uma cópia do relatório para ajudar na correção."
                : "O relatório está disponível nesta tela. Salve uma cópia antes de fechar.",
            FontSize = 15,
            TextColor = Color.FromArgb("#F3E5D1"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var conteudo = new VerticalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 380,
            Children = { imagem, titulo, _salvar, _fechar, _status }
        };
        var centro = new Grid
        {
            Padding = new Thickness(22, 18),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        centro.Add(conteudo, 0, 1);
        var rolagem = new ScrollView { Content = centro };
        rolagem.SizeChanged += (_, _) =>
        {
            if (rolagem.Height <= 0)
                return;

            centro.MinimumHeightRequest = rolagem.Height;
            imagem.HeightRequest = Math.Clamp(rolagem.Height * 0.42, 90, 300);
        };
        Content = rolagem;
    }

    protected override bool OnBackButtonPressed() => true;

    private async void SalvarAsync(object? sender, EventArgs args)
    {
        if (!_salvar.IsEnabled)
            return;

        _salvar.IsEnabled = _fechar.IsEnabled = false;
        try
        {
            bool? salvo = await ExportarAsync();
            _status.Text = salvo switch
            {
                true => "Relatório salvo.",
                false => "Salvamento cancelado. Você pode tentar novamente.",
                null => "Use a opção de salvar na janela do sistema."
            };
        }
        catch
        {
            _status.Text = "Não foi possível salvar nesse local. Tente escolher outro.";
        }
        finally
        {
            _salvar.IsEnabled = _fechar.IsEnabled = true;
            SemanticScreenReader.Default.Announce(_status.Text);
        }
    }

    private void Fechar(object? sender, EventArgs args)
    {
        FalhasApp.Concluir(_relatorio);
#if IOS
        _status.Text = "Relatório concluído. Feche o aplicativo pelo sistema.";
#else
        Microsoft.Maui.Controls.Application.Current?.Quit();
#endif
    }

    private async Task<bool?> ExportarAsync()
    {
#if WINDOWS
        if (Window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window janela)
            throw new InvalidOperationException("Janela indisponível para salvar o relatório.");

        var seletor = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(_relatorio.NomeArquivo),
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        seletor.FileTypeChoices.Add("Arquivo de texto", [".txt"]);
        WinRT.Interop.InitializeWithWindow.Initialize(seletor, WinRT.Interop.WindowNative.GetWindowHandle(janela));
        var destino = await seletor.PickSaveFileAsync();
        if (destino is null)
            return false;

        await Windows.Storage.FileIO.WriteTextAsync(destino, _relatorio.Texto);
        return true;
#elif ANDROID
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not MainActivity atividade)
            throw new InvalidOperationException("Atividade indisponível para salvar o relatório.");

        var destino = await atividade.EscolherDestinoRelatorioAsync(_relatorio.NomeArquivo);
        if (destino is null)
            return false;

        await using var fluxo = atividade.ContentResolver?.OpenOutputStream(destino, "wt")
            ?? throw new IOException("O destino do relatório está indisponível.");
        await using var escritor = new StreamWriter(fluxo, Encoding.UTF8);
        await escritor.WriteAsync(_relatorio.Texto);
        return true;
#else
        string pasta = Path.Combine(FileSystem.CacheDirectory, "relatorios");
        Directory.CreateDirectory(pasta);
        string caminho = Path.Combine(pasta, _relatorio.NomeArquivo);
        await File.WriteAllTextAsync(caminho, _relatorio.Texto, Encoding.UTF8);
        await Share.Default.RequestAsync(new ShareFileRequest("Salvar relatório de erro", new ShareFile(caminho, "text/plain")));
        return null;
#endif
    }
}
