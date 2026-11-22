using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
#if WINDOWS
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
#endif

namespace RPGdeMesa.Presentation.Services;

public sealed record ImagemSelecionada(string DataUrl, string NomeArquivo);
public sealed record ResultadoImagem(bool Cancelado, ImagemSelecionada? Imagem, string? Erro);
public sealed record ResultadoMultiplasImagens(bool Cancelado, List<ImagemSelecionada> Imagens, string? Erro);

public class ImagemPickerService
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly PickOptions Opcoes = new()
    {
        PickerTitle = "Selecione uma imagem",
        FileTypes = FilePickerFileType.Images
    };

    public bool EstaSelecionando => _lock.CurrentCount == 0;

    public async Task<ResultadoImagem> SelecionarUmaAsync(int ladoMaximo = 512, long limiteBytes = 10 * 1024 * 1024)
    {
        if (!await _lock.WaitAsync(0))
            return new(true, null, null);

        try
        {
            var arquivo = await MainThread.InvokeOnMainThreadAsync(() =>
                FilePicker.Default.PickAsync(Opcoes));

            if (arquivo is null)
                return new(true, null, null);

            return await ProcessarArquivoAsync(arquivo, ladoMaximo, limiteBytes);
        }
        catch (Exception ex)
        {
            return new(false, null, $"Erro ao selecionar imagem: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ResultadoMultiplasImagens> SelecionarVariasAsync(
        int maxArquivos = 10,
        int ladoMaximo = 1600,
        long limiteBytes = 10 * 1024 * 1024)
    {
        if (!await _lock.WaitAsync(0))
            return new(true, new(), null);

        try
        {
            var arquivos = await MainThread.InvokeOnMainThreadAsync(() =>
                FilePicker.Default.PickMultipleAsync(Opcoes));

            if (arquivos is null)
                return new(true, new(), null);

            var lista = new List<ImagemSelecionada>();
            string? ultimoErro = null;
            int count = 0;

            foreach (var arquivo in arquivos)
            {
                if (count >= maxArquivos) break;
                count++;

                var res = await ProcessarArquivoAsync(arquivo, ladoMaximo, limiteBytes);
                if (res.Imagem is not null)
                    lista.Add(res.Imagem);
                else if (!string.IsNullOrWhiteSpace(res.Erro))
                    ultimoErro = res.Erro;
            }

            return new(lista.Count == 0 && ultimoErro is null, lista, ultimoErro);
        }
        catch (Exception ex)
        {
            return new(false, new(), $"Erro ao selecionar imagens: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<ResultadoImagem> ProcessarArquivoAsync(FileResult arquivo, int ladoMaximo, long limiteBytes)
    {
        await using var stream = await arquivo.OpenReadAsync();
        using var msOriginal = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int lidos;

        while ((lidos = await stream.ReadAsync(buffer)) > 0)
        {
            total += lidos;
            if (total > limiteBytes)
                return new(false, null, $"A imagem deve ter no máximo {limiteBytes / (1024 * 1024)} MB.");
            msOriginal.Write(buffer, 0, lidos);
        }

        var bytes = msOriginal.ToArray();
        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

        var dataUrl = await Task.Run(() => RedimensionarParaDataUrlAsync(bytes, ext, ladoMaximo));
        return new(false, new ImagemSelecionada(dataUrl, arquivo.FileName), null);
    }

    private static async Task<string> RedimensionarParaDataUrlAsync(byte[] bytes, string ext, int ladoMaximo)
    {
        var mimeOriginal = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };

#if WINDOWS
        try
        {
            using var entrada = new InMemoryRandomAccessStream();
            await entrada.AsStreamForWrite().WriteAsync(bytes);
            await entrada.FlushAsync();
            entrada.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(entrada);
            uint w = decoder.PixelWidth;
            uint h = decoder.PixelHeight;

            if (w <= (uint)ladoMaximo && h <= (uint)ladoMaximo && bytes.Length <= 350_000)
                return $"data:{mimeOriginal};base64,{Convert.ToBase64String(bytes)}";

            double escala = Math.Min(1.0, (double)ladoMaximo / Math.Max(w, h));
            uint nw = Math.Max(1, (uint)Math.Round(w * escala));
            uint nh = Math.Max(1, (uint)Math.Round(h * escala));

            bool ehPng = ext == ".png";
            using var saida = new InMemoryRandomAccessStream();
            BitmapEncoder encoder;

            if (ehPng)
            {
                encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, saida);
            }
            else
            {
                var props = new BitmapPropertySet
                {
                    { "ImageQuality", new BitmapTypedValue(0.82f, PropertyType.Single) }
                };
                encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, saida, props);
            }

            var alpha = ehPng ? BitmapAlphaMode.Straight : BitmapAlphaMode.Ignore;
            var pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                alpha,
                new BitmapTransform
                {
                    ScaledWidth = nw,
                    ScaledHeight = nh,
                    InterpolationMode = BitmapInterpolationMode.Fant
                },
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                alpha,
                nw,
                nh,
                decoder.DpiX,
                decoder.DpiY,
                pixels.DetachPixelData());

            await encoder.FlushAsync();
            saida.Seek(0);

            using var msFinal = new MemoryStream();
            await saida.AsStreamForRead().CopyToAsync(msFinal);

            var mimeFinal = ehPng ? "image/png" : "image/jpeg";
            return $"data:{mimeFinal};base64,{Convert.ToBase64String(msFinal.ToArray())}";
        }
        catch
        {
            // Fallback se o formato não puder ser decodificado pelo WIC
        }
#endif
        return $"data:{mimeOriginal};base64,{Convert.ToBase64String(bytes)}";
    }
}