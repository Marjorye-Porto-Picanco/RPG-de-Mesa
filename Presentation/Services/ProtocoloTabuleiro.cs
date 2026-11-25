using RPGdeMesa.Domain.Combate;

namespace RPGdeMesa.Presentation.Services;

internal readonly record struct EventoTabuleiro(bool Colocada, CasaTabuleiro Casa)
{
    private const string PrefixoEsp = "Recebido do Mega: ";

    internal static bool TentarLer(ReadOnlySpan<char> linha, out EventoTabuleiro evento)
    {
        evento = default;
        linha = linha.Trim();
        if (linha.StartsWith(PrefixoEsp, StringComparison.Ordinal))
            linha = linha[PrefixoEsp.Length..];

        if (linha.Length is < 5 or > 7 || linha[0] is not ('+' or '-') || linha[1] != ',')
            return false;

        int separador = linha[2..].IndexOf(',') + 2;
        if (separador < 3 || !LerCoordenada(linha[2..separador], out int l) || !LerCoordenada(linha[(separador + 1)..], out int c))
            return false;

        evento = new(linha[0] == '+', new(c - 1, l - 1));
        return true;
    }

    private static bool LerCoordenada(ReadOnlySpan<char> texto, out int valor)
    {
        valor = 0;
        if (texto.Length is < 1 or > 2)
            return false;
        foreach (char digito in texto)
        {
            if (digito is < '0' or > '9')
                return false;
            valor = valor * 10 + (digito - '0');
        }
        return valor is >= 1 and <= 20;
    }
}

internal sealed class ConfirmacaoRetornoTabuleiro
{
    private Guid? _retirada;

    internal void Reiniciar() => _retirada = null;

    internal bool Receber(MovimentoTabuleiro movimento, EventoTabuleiro evento)
    {
        if (!evento.Colocada && evento.Casa == movimento.Destino)
        {
            _retirada = movimento.Id;
            return false;
        }

        bool voltou = evento.Colocada && evento.Casa == movimento.Origem && _retirada == movimento.Id;
        Reiniciar();
        return voltou;
    }
}

internal sealed class LeitorEventosTabuleiro
{
    private readonly char[] _linha = new char[48];
    private int _tamanho;
    private bool _descartar;

    internal EventoTabuleiro? Receber(byte dado)
    {
        if (dado == '\n')
        {
            EventoTabuleiro? evento = !_descartar && EventoTabuleiro.TentarLer(_linha.AsSpan(0, _tamanho), out var lido) ? lido : null;
            _tamanho = 0;
            _descartar = false;
            return evento;
        }

        if (dado == '\r' || _descartar)
            return null;
        if (_tamanho >= _linha.Length || dado > 127)
            _descartar = true;
        else
            _linha[_tamanho++] = (char)dado;
        return null;
    }
}
