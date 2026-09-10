namespace MauiRpgMesa;

public sealed class EstadoMesaService
{
    public bool UsuarioEntrou { get; private set; }
    public string NomeUsuario { get; private set; } = string.Empty;
    public int UltimoDado { get; private set; } = 20;

    public void Entrar(string nomeUsuario)
    {
        // Guardando só o necessário por enquanto, porque a aventura começa simples.
        NomeUsuario = nomeUsuario.Trim();
        UsuarioEntrou = true;
    }

    public void Sair()
    {
        // Sair limpa a sessão local, do jeitinho que a gente espera de um botão Sair.
        NomeUsuario = string.Empty;
        UsuarioEntrou = false;
    }

    public int RolarDado()
    {
        // Até o Arduino entrar em cena, este d20 quebra o galho para os testes.
        UltimoDado = Random.Shared.Next(1, 21);
        return UltimoDado;
    }
}
