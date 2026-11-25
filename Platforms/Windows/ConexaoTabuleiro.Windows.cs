using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RPGdeMesa.Presentation.Services;

internal static partial class ConexaoTabuleiro
{
    private static Task LerCaboAsync(string porta, Func<Task> conectado, Func<EventoTabuleiro, Task> receber, CancellationToken token)
    {
        if (!PortasCabo().Contains(porta, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selecione a porta COM da placa ligada por USB.");

        return Task.Run(async () =>
        {
            using var handle = AbrirPorta(@"\\.\" + porta, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Não foi possível abrir {porta}. Confira se o Monitor Serial está fechado.");

            var configuracao = new Dcb { Tamanho = (uint)Marshal.SizeOf<Dcb>() };
            if (!GetCommState(handle, ref configuracao))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            configuracao.BaudRate = 115200;
            configuracao.Flags = 0x11;
            configuracao.Bits = 8;
            configuracao.Paridade = 0;
            configuracao.BitsParada = 0;
            var espera = new CommTimeouts { IntervaloLeitura = uint.MaxValue, MultiplicadorLeitura = uint.MaxValue, LimiteLeitura = 250 };
            if (!SetCommState(handle, ref configuracao) || !SetCommTimeouts(handle, ref espera))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            using var fluxo = new FileStream(handle, FileAccess.ReadWrite, bufferSize: 1, isAsync: false);
            await conectado();
            await LerFluxoAsync(fluxo, true, receber, token);
        }, token);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dcb
    {
        public uint Tamanho, BaudRate, Flags;
        public ushort Reservado, LimiteXon, LimiteXoff;
        public byte Bits, Paridade, BitsParada, Xon, Xoff, Erro, Eof, Evento;
        public ushort Reservado1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CommTimeouts
    {
        public uint IntervaloLeitura, MultiplicadorLeitura, LimiteLeitura, MultiplicadorEscrita, LimiteEscrita;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle AbrirPorta(string nome, uint acesso, uint compartilhamento, IntPtr seguranca,
        uint modo, uint flags, IntPtr modelo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetCommState(SafeFileHandle porta, ref Dcb configuracao);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetCommState(SafeFileHandle porta, ref Dcb configuracao);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetCommTimeouts(SafeFileHandle porta, ref CommTimeouts espera);
}
