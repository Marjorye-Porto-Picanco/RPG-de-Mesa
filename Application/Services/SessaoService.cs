using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public class SessaoService
{
    private readonly ISessaoRepository _sessaoRepo;

    public SessaoService(ISessaoRepository sessaoRepo)
    {
        _sessaoRepo = sessaoRepo;
    }

    public async Task<(bool Sucesso, string Mensagem, SessaoEstadoDto? Estado)> EntrarNaSessaoAsync(
        int idCampanha,
        int idAccount,
        string nomeUsuario,
        int? idPersonagemSelecionado = null)
    {
        try
        {
            var campanha = await _sessaoRepo.ObterCampanhaParaSessaoAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.", null);

            bool ehMestre = campanha.IdAccount == idAccount;
            bool participa = ehMestre
                || campanha.UsuarioCampanhas.Any(uc => uc.IdAccount == idAccount)
                || campanha.PersonagemCampanhas.Any(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccount);

            if (!participa)
                return (false, "Você não tem permissão para entrar nesta sessão.", null);

            int? idMapaInicial = campanha.MapaCampanhas
                .Select(mc => (int?)mc.IdMapa)
                .FirstOrDefault();

            var sessao = await _sessaoRepo.ObterOuCriarSessaoPorCampanhaAsync(idCampanha, idMapaInicial);

            int? idPersonagemJogador = null;
            if (!ehMestre)
            {
                var fichasAceitasConta = campanha.FichasAceitas
                    .Where(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccount)
                    .ToList();

                if (idPersonagemSelecionado.HasValue &&
                    fichasAceitasConta.Any(pc => pc.IdPersonagem == idPersonagemSelecionado.Value))
                {
                    idPersonagemJogador = idPersonagemSelecionado.Value;
                }
                else
                {
                    idPersonagemJogador = fichasAceitasConta
                        .Select(pc => (int?)pc.IdPersonagem)
                        .FirstOrDefault();
                }

                if (!idPersonagemJogador.HasValue)
                    return (false, "Selecione uma ficha aprovada pelo Mestre antes de conectar na sessão.", null);
            }

            bool precisaAnunciarEntrada = await _sessaoRepo.ConectarParticipanteAsync(
                sessao.IdSessao, idAccount, idPersonagemJogador);

            if (precisaAnunciarEntrada)
            {
                string apelido = string.IsNullOrWhiteSpace(nomeUsuario) ? $"Usuário #{idAccount}" : nomeUsuario;
                await _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(
                    sessao.IdSessao,
                    idAccount: null,
                    CanaisSessao.Chat,
                    $"{apelido} conectou-se à sessão."));
            }

            var estado = await MontarEstadoSessaoAsync(campanha, sessao.IdSessao, idAccount);
            return (true, "Conectado à sessão.", estado);
        }
        catch (DomainException dex)
        {
            return (false, dex.Message, null);
        }
        catch (Exception ex)
        {
            string detalhe = ex.InnerException?.Message ?? ex.Message;
            return (false, $"Erro ao entrar na sessão: {detalhe}", null);
        }
    }

    public async Task<SessaoEstadoDto?> SincronizarSessaoAsync(int idCampanha, int idSessao, int idAccount)
    {
        await _sessaoRepo.AtualizarHeartbeatAsync(idSessao, idAccount);

        var campanha = await _sessaoRepo.ObterCampanhaParaSessaoAsync(idCampanha);
        if (campanha == null) return null;

        return await MontarEstadoSessaoAsync(campanha, idSessao, idAccount);
    }

    public async Task SairDaSessaoAsync(int idSessao, int idAccount, string nomeUsuario)
    {
        try
        {
            bool estavaConectado = await _sessaoRepo.DesconectarParticipanteAsync(idSessao, idAccount);
            if (estavaConectado)
            {
                string apelido = string.IsNullOrWhiteSpace(nomeUsuario) ? $"Usuário #{idAccount}" : nomeUsuario;
                await _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(
                    idSessao,
                    idAccount: null,
                    CanaisSessao.Chat,
                    $"{apelido} desconectou-se da sessão."));
            }
        }
        catch
        {
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AlterarMapaAtivoAsync(
        int idCampanha,
        int idSessao,
        int idAccount,
        int? idMapa)
    {
        try
        {
            var campanha = await _sessaoRepo.ObterCampanhaParaSessaoAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccount)
                return (false, "Apenas o Mestre pode alterar o mapa da sessão.");

            if (idMapa.HasValue && !campanha.MapaCampanhas.Any(mc => mc.IdMapa == idMapa.Value))
                return (false, "O mapa selecionado não está vinculado a esta campanha.");

            await _sessaoRepo.AtualizarMapaAtivoAsync(idSessao, idMapa);
            return (true, "Mapa atualizado para toda a mesa.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao alterar mapa: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> EnviarMensagemChatAsync(
        int idSessao,
        int idAccount,
        string conteudo)
    {
        try
        {
            var msg = new SessaoMensagem(idSessao, idAccount, CanaisSessao.Chat, conteudo);
            await _sessaoRepo.AdicionarMensagemAsync(msg);
            return (true, string.Empty);
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao enviar mensagem: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RegistrarRolagemDadosAsync(
        int idSessao,
        string nomeExibicao,
        ResultadoRolagemDto rolagem)
    {
        try
        {
            string dadosIndividuais = string.Join(" + ", rolagem.ResultadosIndividuais);
            string modTexto = rolagem.Modificador > 0
                ? $" + {rolagem.Modificador}"
                : rolagem.Modificador < 0
                    ? $" - {Math.Abs(rolagem.Modificador)}"
                    : string.Empty;

            string textoSistema =
                $"{nomeExibicao} rolou {rolagem.Expressao} ({rolagem.Contexto}): Total {rolagem.Total} [{dadosIndividuais}]{modTexto}";

            var msg = new SessaoMensagem(idSessao, idAccount: null, CanaisSessao.Registro, textoSistema);
            await _sessaoRepo.AdicionarMensagemAsync(msg);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao registrar rolagem: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem) =>
        _sessaoRepo.ObterFichaCompletaAsync(idPersonagem);

    private async Task<SessaoEstadoDto> MontarEstadoSessaoAsync(Campanha campanha, int idSessao, int idAccountLogado)
    {
        var sessaoCompleta = await _sessaoRepo.ObterSessaoCompletaAsync(idSessao);
        var mensagens = await _sessaoRepo.ListarMensagensAsync(idSessao);

        var mapasDisponiveis = campanha.MapaCampanhas
            .Where(mc => mc.Mapa != null)
            .Select(mc => new MapaSessaoDto
            {
                IdMapa = mc.Mapa.IdMapa,
                NomeRegiao = mc.Mapa.NomeRegiao,
                Escala = mc.Mapa.Escala,
                ImagemUrl = mc.Mapa.ImagemUrl
            })
            .OrderBy(m => m.NomeRegiao)
            .ToList();

        int? idMapaAtivo = sessaoCompleta?.IdMapaAtivo;
        var mapaAtivo = mapasDisponiveis.FirstOrDefault(m => m.IdMapa == idMapaAtivo);

        var presencaPorConta = (sessaoCompleta?.Participantes ?? Array.Empty<SessaoParticipante>())
            .ToDictionary(p => p.IdAccount, p => p);

        presencaPorConta.TryGetValue(campanha.IdAccount, out var presencaMestre);
        var mestreDto = new ParticipanteSessaoDto
        {
            IdAccount = campanha.IdAccount,
            Username = campanha.UsuarioMestre?.UsarName ?? "Mestre",
            EhMestre = true,
            SubtituloLinha1 = "Mestre",
            SubtituloLinha2 = string.Empty,
            AvatarUrl = campanha.UsuarioMestre?.Perfil?.AvatarUrl,
            Online = presencaMestre?.EstaConectadoAgora() ?? false
        };

        var fichasAceitasPorConta = campanha.FichasAceitas
            .Where(pc => pc.Personagem != null)
            .GroupBy(pc => pc.Personagem!.IdAccount)
            .ToDictionary(g => g.Key, g => g.First().Personagem!);

        var contasJogadores = new Dictionary<int, (string Username, string? AvatarPerfil)>();

        foreach (var uc in campanha.UsuarioCampanhas.Where(uc => uc.IdAccount != campanha.IdAccount))
        {
            contasJogadores[uc.IdAccount] = (
                uc.Usuario?.UsarName ?? $"Jogador #{uc.IdAccount}",
                uc.Usuario?.Perfil?.AvatarUrl);
        }

        foreach (var kv in fichasAceitasPorConta.Where(kv => kv.Key != campanha.IdAccount))
        {
            if (!contasJogadores.ContainsKey(kv.Key))
            {
                contasJogadores[kv.Key] = (
                    kv.Value.Usuario?.UsarName ?? $"Jogador #{kv.Key}",
                    kv.Value.Usuario?.Perfil?.AvatarUrl);
            }
        }

        var jogadoresDto = new List<ParticipanteSessaoDto>();
        foreach (var (idConta, infoUsuario) in contasJogadores.OrderBy(x => x.Value.Username))
        {
            presencaPorConta.TryGetValue(idConta, out var presenca);
            fichasAceitasPorConta.TryGetValue(idConta, out var ficha);

            var fichaAtiva = presenca?.Personagem ?? ficha;
            string nomeFicha = fichaAtiva?.Nome ?? "Sem ficha aceita";
            string classeNivel = fichaAtiva != null ? FormatarClasseENivel(fichaAtiva) : string.Empty;

            jogadoresDto.Add(new ParticipanteSessaoDto
            {
                IdAccount = idConta,
                IdPersonagem = fichaAtiva?.IdPersonagem,
                Username = infoUsuario.Username,
                EhMestre = false,
                NomePersonagem = fichaAtiva?.Nome,
                SubtituloLinha1 = nomeFicha,
                SubtituloLinha2 = classeNivel,
                AvatarUrl = !string.IsNullOrWhiteSpace(fichaAtiva?.Imagem)
                    ? fichaAtiva.Imagem
                    : infoUsuario.AvatarPerfil,
                Online = presenca?.EstaConectadoAgora() ?? false
            });
        }

        int? idPersonagemAtual = jogadoresDto
            .FirstOrDefault(j => j.IdAccount == idAccountLogado)?.IdPersonagem;

        var mensagensDto = mensagens.Select(m => new MensagemSessaoDto
        {
            IdMensagem = m.IdMensagem,
            Canal = m.Canal,
            EhSistema = m.IdAccount == null,
            Autor = m.IdAccount == null
                ? "Sistema"
                : (m.Usuario?.UsarName ?? $"Usuário #{m.IdAccount}"),
            Conteudo = m.Conteudo,
            DataEnvioLocal = DateTime.SpecifyKind(m.DataEnvio, DateTimeKind.Utc).ToLocalTime()
        }).ToList();

        return new SessaoEstadoDto
        {
            IdSessao = idSessao,
            IdCampanha = campanha.IdCampanha,
            NomeCampanha = campanha.Nome,
            EhMestre = campanha.IdAccount == idAccountLogado,
            IdPersonagemLogado = idPersonagemAtual,
            IdMapaAtivo = idMapaAtivo,
            MapaAtivo = mapaAtivo,
            MapasDisponiveis = mapasDisponiveis,
            Mestre = mestreDto,
            Jogadores = jogadoresDto,
            Mensagens = mensagensDto
        };
    }

    private static string FormatarClasseENivel(Personagem personagem)
    {
        if (personagem.PersonagemClasses != null && personagem.PersonagemClasses.Any(pc => pc.Classe != null))
        {
            var partes = personagem.PersonagemClasses
                .Where(pc => pc.Classe != null)
                .Select(pc => $"{pc.Classe!.Nome} {pc.NivelNaClasse}");

            int nivelTotal = Math.Max(personagem.Nivel, personagem.PersonagemClasses.Sum(pc => pc.NivelNaClasse));
            return $"{string.Join(" / ", partes)} • Nv. {nivelTotal}";
        }

        string classe = string.IsNullOrWhiteSpace(personagem.ClasseTexto) ? "Aventureiro" : personagem.ClasseTexto;
        return $"{classe} • Nv. {personagem.Nivel}";
    }
}

public class SessaoEstadoDto
{
    public int IdSessao { get; set; }
    public int IdCampanha { get; set; }
    public string NomeCampanha { get; set; } = string.Empty;
    public bool EhMestre { get; set; }
    public int? IdPersonagemLogado { get; set; }
    public int? IdMapaAtivo { get; set; }
    public MapaSessaoDto? MapaAtivo { get; set; }
    public List<MapaSessaoDto> MapasDisponiveis { get; set; } = new();
    public ParticipanteSessaoDto Mestre { get; set; } = new();
    public List<ParticipanteSessaoDto> Jogadores { get; set; } = new();
    public List<MensagemSessaoDto> Mensagens { get; set; } = new();
}

public class MapaSessaoDto
{
    public int IdMapa { get; set; }
    public string NomeRegiao { get; set; } = string.Empty;
    public string? Escala { get; set; }
    public string? ImagemUrl { get; set; }
}

public class ParticipanteSessaoDto
{
    public int IdAccount { get; set; }
    public int? IdPersonagem { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool EhMestre { get; set; }
    public string? NomePersonagem { get; set; }
    public string SubtituloLinha1 { get; set; } = string.Empty;
    public string SubtituloLinha2 { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool Online { get; set; }
}

public class MensagemSessaoDto
{
    public int IdMensagem { get; set; }
    public string Canal { get; set; } = CanaisSessao.Chat;
    public bool EhSistema { get; set; }
    public string Autor { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;
    public DateTime DataEnvioLocal { get; set; }
}

public class ResultadoRolagemDto
{
    public string Contexto { get; set; } = "Rolagem da mesa";
    public int Faces { get; set; } = 20;
    public int Quantidade { get; set; } = 1;
    public int Modificador { get; set; }
    public List<int> ResultadosIndividuais { get; set; } = new();
    public int Total { get; set; }
    public string Expressao { get; set; } = string.Empty;
}