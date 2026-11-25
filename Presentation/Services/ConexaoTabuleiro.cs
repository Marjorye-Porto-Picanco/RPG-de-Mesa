namespace RPGdeMesa.Presentation.Services;

internal enum ModoTabuleiro { CaboMega, CaboEsp, BluetoothEsp }

internal static partial class ConexaoTabuleiro
{
    internal static bool Disponivel => OperatingSystem.IsWindows() || OperatingSystem.IsAndroid();

    internal static string[] PortasCabo()
    {
#if WINDOWS
        using var chave = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
        return chave?.GetValueNames().Select(nome => chave.GetValue(nome) as string)
            .OfType<string>().Distinct().Order().ToArray() ?? [];
#else
        return [];
#endif
    }

    internal static async Task LerAsync(ModoTabuleiro modo, string porta, Func<Task> conectado,
        Func<EventoTabuleiro, Task> receber, CancellationToken token)
    {
#if ANDROID
        if (modo != ModoTabuleiro.BluetoothEsp)
            throw new InvalidOperationException("Use a conexão Bluetooth no Android.");
        await LerBluetoothAsync(conectado, receber, token);
#elif WINDOWS
        if (modo == ModoTabuleiro.BluetoothEsp)
            throw new InvalidOperationException("Use uma porta USB no Windows.");
        await LerCaboAsync(porta, conectado, receber, token);
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Conecte o tabuleiro pelo Windows ou Android.");
#endif
    }

    private static async Task LerFluxoAsync(Stream fluxo, bool expiraLeitura, Func<EventoTabuleiro, Task> receber, CancellationToken token)
    {
        var parser = new LeitorEventosTabuleiro();
        var dados = new byte[128];
        while (!token.IsCancellationRequested)
        {
            int quantidade = expiraLeitura ? fluxo.Read(dados) : await fluxo.ReadAsync(dados.AsMemory(), token);
            if (quantidade == 0 && !expiraLeitura)
                throw new IOException("A conexão do tabuleiro foi encerrada.");
            for (int i = 0; i < quantidade && !token.IsCancellationRequested; i++)
            {
                if (parser.Receber(dados[i]) is { } evento)
                    await receber(evento);
            }
        }
    }
}
