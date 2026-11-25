using Android.Bluetooth;
using Microsoft.Maui.ApplicationModel;

namespace RPGdeMesa.Presentation.Services;

internal static partial class ConexaoTabuleiro
{
    private static async Task LerBluetoothAsync(Func<Task> conectado, Func<EventoTabuleiro, Task> receber, CancellationToken token)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31) &&
            await Permissions.RequestAsync<PermissaoBluetoothTabuleiro>() != PermissionStatus.Granted)
            throw new InvalidOperationException("Permita o acesso a dispositivos próximos para usar o tabuleiro.");

        token.ThrowIfCancellationRequested();
        var manager = Android.App.Application.Context.GetSystemService(Android.Content.Context.BluetoothService) as BluetoothManager;
        if (manager?.Adapter is not { IsEnabled: true } adaptador)
            throw new InvalidOperationException("Ative o Bluetooth do celular.");

        var dispositivos = adaptador.BondedDevices?.Where(d => d.Name == "Tabuleiro_ESP32").ToList();
        if (dispositivos is not { Count: 1 })
            throw new InvalidOperationException("Pareie um único dispositivo chamado Tabuleiro_ESP32 nas configurações do celular.");

        using var uuid = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
        using var socket = dispositivos[0].CreateRfcommSocketToServiceRecord(uuid)
            ?? throw new IOException("O ESP32 não abriu a comunicação Bluetooth serial.");
        using var desconectar = token.Register(() =>
        {
            try { socket.Close(); }
            catch (Java.IO.IOException) { }
        });

        await Task.Run(() => socket.Connect(), token);
        await conectado();
        await LerFluxoAsync(socket.InputStream ?? throw new IOException("O ESP32 não enviou um fluxo de dados."), false, receber, token);
    }
}

public sealed class PermissaoBluetoothTabuleiro : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(Android.Manifest.Permission.BluetoothConnect, true)];
}
