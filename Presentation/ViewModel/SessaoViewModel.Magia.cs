using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;
using System.Collections;
using System.Reflection;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    private static readonly Dictionary<(int IdSessao, int IdPersonagem), Dictionary<int, int>> EspacosUsadosPorSessao = new();
    private static readonly Dictionary<(int IdSessao, int IdPersonagem), TipoConjuracaoClasse> TipoConjuracaoPorPersonagem = new();

    public bool ExibirModalMagia { get; private set; }
    public bool CarregandoMagia { get; private set; }
    public bool ExecutandoMagia { get; private set; }
    public string? ErroModalMagia { get; private set; }
    public int EtapaMagia { get; private set; } = 1;
    public int TurnosRestantesConjuracao { get; private set; }
    public bool ConjuracaoEmCarregamento { get; private set; }
    private int _idMagiaEmCarregamento;
    private int _circuloEmCarregamento;

    public Personagem? PersonagemConjurador { get; private set; }
    public TipoConjuracaoClasse TipoConjuracaoAtual { get; private set; } = TipoConjuracaoClasse.Nenhum;
    public Dictionary<int, int> EspacosMaximosPorCirculo { get; private set; } = new();
    public int BonusAtaqueMagico { get; private set; }
    public int CdResistenciaMagia { get; private set; } = 10;
    public string AtributoConjuracao { get; private set; } = "INT";

    public List<MagiaSessaoItemDto> MagiasDisponiveisSessao { get; private set; } = new();
    public int IdMagiaSelecionada { get; private set; }
    public int CirculoConjuracaoSelecionado { get; private set; }
    public ModoResolucaoMagia ResolucaoMagiaAtual { get; private set; } = ModoResolucaoMagia.AtaqueMagico;
    public string ExpressaoDadoMagia { get; set; } = "1d8";
    public int BonusExtraMagia { get; set; }
    public string AtributoSalvaguardaMagia { get; set; } = "Destreza";
    public bool MeioDanoSePassarSalvaguarda { get; set; } = true;

    public string? TipoAlvoMagiaSelecionado { get; private set; }
    public int IdAlvoMagiaSelecionado { get; private set; }
    public ResultadoConjuracaoDto? ResultadoMagiaAtual { get; private set; }

    public MagiaSessaoItemDto? MagiaSelecionada =>
        MagiasDisponiveisSessao.FirstOrDefault(m => m.IdMagia == IdMagiaSelecionada);

    public bool ConjuracaoPronta => ConjuracaoEmCarregamento && TurnosRestantesConjuracao <= 0;
    public bool BloquearAtaquePorConjuracao => ConjuracaoEmCarregamento && !ConjuracaoPronta;

    public List<int> CirculosDisponiveisParaMagiaSelecionada
    {
        get
        {
            if (MagiaSelecionada is null || MagiaSelecionada.CirculoNivel <= 0)
                return new List<int>();

            return EspacosMaximosPorCirculo.Keys
                .Where(c => c >= MagiaSelecionada.CirculoNivel)
                .OrderBy(c => c)
                .ToList();
        }
    }

    public List<PinoMapaDto> AlvosParaMagia
    {
        get
        {
            if (MagiaSelecionada?.AlcanceInfo.Categoria == CategoriaAlcanceMagia.Pessoal)
            {
                return PinoJogadorAtual is not null
                    ? new List<PinoMapaDto> { PinoJogadorAtual }
                    : new List<PinoMapaDto>();
            }

            if (ResolucaoMagiaAtual is ModoResolucaoMagia.Cura or ModoResolucaoMagia.Efeito)
            {
                return PinosVisiveisNoMapa
                    .OrderBy(p => p.TipoEntidade == TiposPinoSessao.Personagem ? 0 : p.TipoEntidade == TiposPinoSessao.Npc ? 1 : 2)
                    .ThenBy(p => p.Nome)
                    .ToList();
            }

            return AlvosNoMapa;
        }
    }

    public PinoMapaDto? AlvoMagiaSelecionado =>
        AlvosParaMagia.FirstOrDefault(a =>
            a.TipoEntidade == TipoAlvoMagiaSelecionado &&
            a.IdPino == IdAlvoMagiaSelecionado);

    public bool PodeExecutarMagia
    {
        get
        {
            if (ExecutandoMagia || MagiaSelecionada is null)
                return false;

            if (MagiaSelecionada.CirculoNivel > 0 && ObterEspacosRestantesCirculo(CirculoConjuracaoSelecionado) <= 0 &&
                !(ConjuracaoPronta && MagiaSelecionada.IdMagia == _idMagiaEmCarregamento))
                return false;

            if (MagiaSelecionada.AlcanceInfo.Categoria == CategoriaAlcanceMagia.Pessoal)
                return true;

            if (AlvoMagiaSelecionado is null)
                return ResolucaoMagiaAtual == ModoResolucaoMagia.Efeito;

            if (!AlvoDentroAlcanceMagia(AlvoMagiaSelecionado))
                return false;

            if (ResolucaoMagiaAtual is ModoResolucaoMagia.AtaqueMagico or ModoResolucaoMagia.Salvaguarda)
                return (AlvoMagiaSelecionado.PvAtual ?? AlvoMagiaSelecionado.PvMaximo ?? 0) > 0;

            return true;
        }
    }

    public int ObterEspacosMaximosCirculo(int circulo) =>
        EspacosMaximosPorCirculo.TryGetValue(circulo, out int max) ? max : 0;

    public int ObterEspacosRestantesCirculo(int circulo)
    {
        int max = ObterEspacosMaximosCirculo(circulo);
        if (max <= 0 || !Estado.IdPersonagemLogado.HasValue) return 0;

        var usados = ObterMapaEspacosUsados(Estado.IdSessao, Estado.IdPersonagemLogado.Value);
        int gastos = usados.TryGetValue(circulo, out int g) ? g : 0;
        return Math.Max(0, max - gastos);
    }

    private static Dictionary<int, int> ObterMapaEspacosUsados(int idSessao, int idPersonagem)
    {
        var chave = (idSessao, idPersonagem);
        if (!EspacosUsadosPorSessao.TryGetValue(chave, out var mapa))
        {
            mapa = new Dictionary<int, int>();
            EspacosUsadosPorSessao[chave] = mapa;
        }
        return mapa;
    }

    private async Task ConsumirEspacoMagiaAsync(int circulo)
    {
        if (circulo <= 0 || !Estado.IdPersonagemLogado.HasValue) return;
        var usados = ObterMapaEspacosUsados(Estado.IdSessao, Estado.IdPersonagemLogado.Value);
        usados[circulo] = usados.TryGetValue(circulo, out int atual) ? atual + 1 : 1;
        if (PersonagemConjurador is not null)
        {
            var linha = PersonagemConjurador.CirculosMagicos.FirstOrDefault(s => s.CirculoNivel == circulo);
            if (linha is not null) linha.SlotsUsados = usados[circulo];
            await _sessaoService.SalvarCirculosMagicosAsync(PersonagemConjurador.IdPersonagem, PersonagemConjurador.CirculosMagicos);
        }
    }

    private async Task SincronizarSlotsPersonagemAsync(Personagem personagem)
    {
        var gastos = ObterMapaEspacosUsados(Estado.IdSessao, personagem.IdPersonagem);
        bool jaPossuiaSlotsPersistidos = personagem.CirculosMagicos.Count > 0;
        foreach (var (circulo, totalCalculado) in EspacosMaximosPorCirculo.ToList())
        {
            var salvo = personagem.CirculosMagicos.FirstOrDefault(s => s.CirculoNivel == circulo);
            if (salvo is null)
            {
                salvo = new PersonagemCirculoMagico
                {
                    IdPersonagem = personagem.IdPersonagem,
                    CirculoNivel = circulo,
                    TotalSlots = jaPossuiaSlotsPersistidos ? 0 : totalCalculado,
                    TotalSlotsPendente = jaPossuiaSlotsPersistidos ? totalCalculado : null
                };
                personagem.CirculosMagicos.Add(salvo);
            }
            else if (salvo.TotalSlots != totalCalculado)
                salvo.TotalSlotsPendente = totalCalculado;

            gastos[circulo] = salvo.SlotsUsados;
            EspacosMaximosPorCirculo[circulo] = salvo.TotalSlots;
        }
        await _sessaoService.SalvarCirculosMagicosAsync(personagem.IdPersonagem, personagem.CirculosMagicos);
    }

    public void CancelarConjuracaoAtual()
    {
        if (!ConjuracaoEmCarregamento) return;
        string nome = MagiasDisponiveisSessao.FirstOrDefault(m => m.IdMagia == _idMagiaEmCarregamento)?.Nome ?? "Magia";
        ConjuracaoEmCarregamento = false;
        TurnosRestantesConjuracao = 0;
        _idMagiaEmCarregamento = 0;
        _circuloEmCarregamento = 0;
        FeedbackAcao = $"Conjuração de {nome} cancelada.";
        ErroModalMagia = FeedbackAcao;
        Notificar();
    }

    private void AvancarTurnoConjuracao()
    {
        if (!ConjuracaoEmCarregamento || TurnosRestantesConjuracao <= 0) return;
        TurnosRestantesConjuracao--;
        FeedbackAcao = TurnosRestantesConjuracao == 0
            ? "A magia terminou de carregar. Abra Conjurar Magia para concluir a resolução."
            : $"Conjuração em andamento: faltam {TurnosRestantesConjuracao} turno(s).";
    }

    public async Task RegistrarDescansoDaMesaAsync(bool longo)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0) return;
        string textoDescanso = longo
            ? "🛏️ O Mestre iniciou um descanso longo da mesa."
            : "⛺ O Mestre iniciou um descanso curto da mesa.";
        var (sucesso, erro) = await _sessaoService.EnviarMensagemChatAsync(
            Estado.IdSessao, _sessaoUsuario.IdUsuarioLogado, textoDescanso);
        if (!sucesso)
        {
            FeedbackAcao = erro;
            Notificar();
            return;
        }
        await AplicarDescansoRecebido(longo);
        if (Estado.IdMapaAtivo.HasValue)
            await _sessaoService.RecuperarRecursos2014Async(Estado, _sessaoUsuario.IdUsuarioLogado, longo);
        FeedbackAcao = longo
            ? "Descanso longo concluído. Os espaços de magia foram restaurados para a mesa."
            : "Descanso curto concluído. Espaços de magia de Pacto restaurados.";
        Notificar();
        await SincronizarAgoraAsync();
    }

    public async Task AplicarDescansoRecebido(bool longo)
    {
        if (longo)
        {
            foreach (var chave in EspacosUsadosPorSessao.Keys.Where(k => k.IdSessao == Estado.IdSessao).ToList())
                EspacosUsadosPorSessao.Remove(chave);
            foreach (var jogador in Estado.Jogadores.Where(j => j.IdPersonagem.HasValue))
                await _sessaoService.AplicarDescansoCirculosMagicosAsync(jogador.IdPersonagem!.Value, true, Array.Empty<int>());
            if (PersonagemConjurador is not null)
            {
                foreach (var slot in PersonagemConjurador.CirculosMagicos) slot.AplicarDescanso();
                foreach (var slot in PersonagemConjurador.CirculosMagicos) EspacosMaximosPorCirculo[slot.CirculoNivel] = slot.TotalSlots;
                if (Estado.IdPersonagemLogado.HasValue) ObterMapaEspacosUsados(Estado.IdSessao, Estado.IdPersonagemLogado.Value).Clear();
            }
            return;
        }

        var pactuadores = TipoConjuracaoPorPersonagem
            .Where(kv => kv.Key.IdSessao == Estado.IdSessao && kv.Value == TipoConjuracaoClasse.Pacto)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var chave in pactuadores)
            if (EspacosUsadosPorSessao.TryGetValue(chave, out var usados)) usados.Clear();

        foreach (var jogador in Estado.Jogadores.Where(j => j.IdPersonagem.HasValue))
        {
            var ficha = await _sessaoService.ObterFichaCompletaAsync(jogador.IdPersonagem!.Value);
            if (ficha is null || RegrasMagiaCombate.IdentificarTipoConjuracao(ObterNomeClassePersonagem(ficha)) != TipoConjuracaoClasse.Pacto) continue;
            var circulosPacto = RegrasMagiaCombate.ObterEspacosPorCirculo(TipoConjuracaoClasse.Pacto, ficha.Nivel).Keys;
            await _sessaoService.AplicarDescansoCirculosMagicosAsync(ficha.IdPersonagem, false, circulosPacto);
        }
        if (PersonagemConjurador is not null && TipoConjuracaoAtual == TipoConjuracaoClasse.Pacto)
        {
            foreach (var slot in PersonagemConjurador.CirculosMagicos) slot.SlotsUsados = 0;
            if (Estado.IdPersonagemLogado.HasValue) ObterMapaEspacosUsados(Estado.IdSessao, Estado.IdPersonagemLogado.Value).Clear();
        }
    }

    public bool AlvoDentroAlcanceMagia(PinoMapaDto alvo)
    {
        if (MagiaSelecionada is null) return true;

        var alcance = MagiaSelecionada.AlcanceInfo;
        if (alcance.Categoria == CategoriaAlcanceMagia.Ilimitado || !alcance.MetrosMaximos.HasValue)
            return true;

        if (alcance.Categoria == CategoriaAlcanceMagia.Pessoal)
            return Estado.IdPersonagemLogado.HasValue &&
                   alvo.TipoEntidade == TiposPinoSessao.Personagem &&
                   alvo.IdEntidade == Estado.IdPersonagemLogado.Value;

        var dist = ObterDistanciaAlvoMetros(alvo);
        if (!dist.HasValue) return true;
        return dist.Value <= alcance.MetrosMaximos.Value;
    }

    public async Task AbrirModalMagiaAsync()
    {
        if (Estado.EhMestre)
        {
            FeedbackAcao = "A ação de Conjurar Magia usa as magias da ficha do jogador.";
            Notificar();
            return;
        }

        if (!Estado.IdPersonagemLogado.HasValue)
        {
            FeedbackAcao = "Você precisa ter uma ficha aprovada na sessão para conjurar magias.";
            Notificar();
            return;
        }

        if (!EhMeuTurno)
        {
            FeedbackAcao = "Aguarde o Mestre liberar o seu turno para conjurar magias.";
            Notificar();
            return;
        }

        if (ConjuracaoEmCarregamento && !ConjuracaoPronta)
        {
            FeedbackAcao = $"Você está carregando uma magia. Restam {TurnosRestantesConjuracao} turno(s).";
            Notificar();
            return;
        }

        FecharOutrosModaisCombate();

        ExibirModalMagia = true;
        CarregandoMagia = true;
        ErroModalMagia = null;
        EtapaMagia = 1;
        ResultadoMagiaAtual = null;
        MagiasDisponiveisSessao = new List<MagiaSessaoItemDto>();
        BonusExtraMagia = 0;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            PersonagemConjurador = await _sessaoService.ObterFichaCompletaAsync(Estado.IdPersonagemLogado.Value);
            if (PersonagemConjurador == null)
            {
                ErroModalMagia = "Não foi possível carregar sua ficha.";
                return;
            }

            string nomeClasse = ObterNomeClassePersonagem(PersonagemConjurador);
            TipoConjuracaoAtual = RegrasMagiaCombate.IdentificarTipoConjuracao(nomeClasse);
            TipoConjuracaoPorPersonagem[(Estado.IdSessao, PersonagemConjurador.IdPersonagem)] = TipoConjuracaoAtual;
            EspacosMaximosPorCirculo = RegrasMagiaCombate.ObterEspacosPorCirculo(TipoConjuracaoAtual, PersonagemConjurador.Nivel);
            await SincronizarSlotsPersonagemAsync(PersonagemConjurador);

            var (bonusAtk, cd, attr) = RegrasMagiaCombate.CalcularParametrosConjuracao(
                nomeClasse,
                PersonagemConjurador.Nivel,
                PersonagemConjurador.Inteligencia,
                PersonagemConjurador.Sabedoria,
                PersonagemConjurador.Carisma);

            BonusAtaqueMagico = bonusAtk;
            CdResistenciaMagia = cd;
            AtributoConjuracao = attr;

            MagiasDisponiveisSessao = ExtrairMagiasDaFicha(PersonagemConjurador);

            if (MagiasDisponiveisSessao.Count > 0)
            {
                int idInicial = ConjuracaoEmCarregamento ? _idMagiaEmCarregamento : MagiasDisponiveisSessao[0].IdMagia;
                SelecionarMagiaInterno(idInicial);
                if (ConjuracaoEmCarregamento)
                {
                    CirculoConjuracaoSelecionado = _circuloEmCarregamento;
                }
            }
        }
        catch (Exception ex)
        {
            ErroModalMagia = $"Erro ao carregar magias da ficha: {ex.Message}";
        }
        finally
        {
            CarregandoMagia = false;
            _dbLock.Release();
            Notificar();
        }
    }

    private static string ObterNomeClassePersonagem(Personagem personagem)
    {
        var tipo = personagem.GetType();
        foreach (string propNome in new[] { "Classe", "ClasseTexto", "NomeClasse" })
        {
            var prop = tipo.GetProperty(propNome, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.GetValue(personagem) is string texto && !string.IsNullOrWhiteSpace(texto))
                return texto;
        }

        foreach (string colNome in new[] { "Classes", "PersonagemClasses" })
        {
            var prop = tipo.GetProperty(colNome, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.GetValue(personagem) is IEnumerable colecao)
            {
                foreach (var item in colecao)
                {
                    if (item is null) continue;
                    var alvo = ObterPropriedadeObjeto(item, "Classe") ?? item;
                    string? nome = ObterPropriedadeString(alvo, "Nome", "NomeClasse");
                    if (!string.IsNullOrWhiteSpace(nome))
                        return nome;
                }
            }
        }

        return string.Empty;
    }

    private static List<MagiaSessaoItemDto> ExtrairMagiasDaFicha(Personagem personagem)
    {
        var lista = new List<MagiaSessaoItemDto>();
        var tipo = personagem.GetType();
        IEnumerable? colecao = null;

        foreach (string colNome in new[] { "Magias", "PersonagemMagias", "MagiasPersonagem", "ListaMagias" })
        {
            var prop = tipo.GetProperty(colNome, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.GetValue(personagem) is IEnumerable encontrada)
            {
                colecao = encontrada;
                break;
            }
        }

        if (colecao is null)
            return lista;

        foreach (var vinculo in colecao)
        {
            if (vinculo is null) continue;
            var magia = ObterPropriedadeObjeto(vinculo, "Magia") ?? vinculo;

            int idMagia = ObterPropriedadeInt(magia, "IdMagia", "Id");
            string? nome = ObterPropriedadeString(magia, "Nome");
            if (idMagia <= 0 || string.IsNullOrWhiteSpace(nome))
                continue;

            int circulo = ObterPropriedadeInt(magia, "CirculoNivel", "Circulo", "Nivel");
            string? escola = ObterPropriedadeString(magia, "Escola");
            string? tempo = ObterPropriedadeString(magia, "TempoConjuracao");
            string? alcance = ObterPropriedadeString(magia, "Alcance");
            string? componentes = ObterPropriedadeString(magia, "Componentes");
            string? duracao = ObterPropriedadeString(magia, "Duracao");
            string? danoEfeito = ObterPropriedadeString(magia, "DanoEfeito");

            lista.Add(new MagiaSessaoItemDto
            {
                IdMagia = idMagia,
                Nome = nome,
                CirculoNivel = circulo,
                Escola = escola,
                TempoConjuracao = tempo,
                Alcance = alcance,
                Componentes = componentes,
                Duracao = duracao,
                DanoEfeito = danoEfeito,
                AlcanceInfo = RegrasMagiaCombate.InterpretarAlcance(alcance),
                TempoInfo = RegrasMagiaCombate.InterpretarTempo(tempo),
                DuracaoInfo = RegrasMagiaCombate.InterpretarDuracao(duracao)
            });
        }

        return lista
            .GroupBy(m => m.IdMagia)
            .Select(g => g.First())
            .OrderBy(m => m.CirculoNivel)
            .ThenBy(m => m.Nome)
            .ToList();
    }

    private static object? ObterPropriedadeObjeto(object instancia, string nomePropriedade)
    {
        var prop = instancia.GetType().GetProperty(nomePropriedade, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(instancia);
    }

    private static string? ObterPropriedadeString(object instancia, params string[] nomes)
    {
        var tipo = instancia.GetType();
        foreach (string nome in nomes)
        {
            var prop = tipo.GetProperty(nome, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.GetValue(instancia) is string valor && !string.IsNullOrWhiteSpace(valor))
                return valor;
        }
        return null;
    }

    private static int ObterPropriedadeInt(object instancia, params string[] nomes)
    {
        var tipo = instancia.GetType();
        foreach (string nome in nomes)
        {
            var prop = tipo.GetProperty(nome, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var valor = prop?.GetValue(instancia);
            if (valor is int i) return i;
            if (valor is not null && int.TryParse(valor.ToString(), out int convertido)) return convertido;
        }
        return 0;
    }

    public void SelecionarMagiaSessao(int idMagia)
    {
        if (ConjuracaoEmCarregamento && idMagia != _idMagiaEmCarregamento)
        {
            ErroModalMagia = "Cancele a conjuração atual antes de selecionar outra magia.";
            Notificar();
            return;
        }
        SelecionarMagiaInterno(idMagia);
        Notificar();
    }

    private void SelecionarMagiaInterno(int idMagia)
    {
        IdMagiaSelecionada = idMagia;
        ErroModalMagia = null;

        var magia = MagiaSelecionada;
        if (magia is null) return;

        ResolucaoMagiaAtual = RegrasMagiaCombate.InferirModoResolucao(magia);
        string dadoExtraido = RegrasMagiaCombate.ExtrairPrimeiraExpressaoDado(magia.DanoEfeito);
        ExpressaoDadoMagia = !string.IsNullOrWhiteSpace(dadoExtraido)
            ? dadoExtraido
            : (ResolucaoMagiaAtual == ModoResolucaoMagia.Efeito ? string.Empty : "1d8");
        MeioDanoSePassarSalvaguarda = magia.CirculoNivel > 0;

        AtualizarCirculoPadraoMagia();
        SelecionarPrimeiroAlvoMagiaValido();
    }

    private void AtualizarCirculoPadraoMagia()
    {
        if (MagiaSelecionada is null || MagiaSelecionada.CirculoNivel <= 0)
        {
            CirculoConjuracaoSelecionado = 0;
            return;
        }

        var livres = CirculosDisponiveisParaMagiaSelecionada
            .Where(c => ObterEspacosRestantesCirculo(c) > 0)
            .ToList();

        CirculoConjuracaoSelecionado = livres.Count > 0
            ? livres[0]
            : MagiaSelecionada.CirculoNivel;
    }

    public void SelecionarCirculoConjuracao(int circulo)
    {
        if (ConjuracaoEmCarregamento) return;
        if (ObterEspacosRestantesCirculo(circulo) <= 0) return;
        CirculoConjuracaoSelecionado = circulo;
        ErroModalMagia = null;
        Notificar();
    }

    public void SelecionarModoResolucaoMagia(ModoResolucaoMagia modo)
    {
        ResolucaoMagiaAtual = modo;
        if (modo != ModoResolucaoMagia.Efeito && string.IsNullOrWhiteSpace(ExpressaoDadoMagia))
            ExpressaoDadoMagia = "1d8";
        SelecionarPrimeiroAlvoMagiaValido();
        Notificar();
    }

    public void SelecionarAlvoMagia(string tipoEntidade, int idPino)
    {
        var alvo = AlvosParaMagia.FirstOrDefault(a =>
            a.TipoEntidade == tipoEntidade &&
            a.IdPino == idPino);

        if (alvo is null || !AlvoDentroAlcanceMagia(alvo))
            return;

        if (ResolucaoMagiaAtual is ModoResolucaoMagia.AtaqueMagico or ModoResolucaoMagia.Salvaguarda &&
            (alvo.PvAtual ?? alvo.PvMaximo ?? 0) <= 0)
            return;

        TipoAlvoMagiaSelecionado = tipoEntidade;
        IdAlvoMagiaSelecionado = idPino;
        ErroModalMagia = null;
        Notificar();
    }

    private void SelecionarPrimeiroAlvoMagiaValido()
    {
        var candidato = AlvosParaMagia.FirstOrDefault(a =>
            AlvoDentroAlcanceMagia(a) &&
            (ResolucaoMagiaAtual is ModoResolucaoMagia.Cura or ModoResolucaoMagia.Efeito ||
             (a.PvAtual ?? a.PvMaximo ?? 0) > 0));

        TipoAlvoMagiaSelecionado = candidato?.TipoEntidade;
        IdAlvoMagiaSelecionado = candidato?.IdPino ?? 0;
    }

    public async Task ExecutarEtapa1MagiaAsync()
    {
        if (!PodeExecutarMagia || MagiaSelecionada is null || Estado.IdSessao <= 0)
            return;

        if (!EhMeuTurno)
        {
            ErroModalMagia = "Aguarde o Mestre liberar o seu turno para conjurar.";
            Notificar();
            return;
        }

        var magia = MagiaSelecionada;
        var alvo = AlvoMagiaSelecionado;
        bool concluindoCarregamento = ConjuracaoPronta && magia.IdMagia == _idMagiaEmCarregamento;
        if (ConjuracaoEmCarregamento && !concluindoCarregamento)
        {
            ErroModalMagia = $"A magia ainda está carregando ({TurnosRestantesConjuracao} turno(s)).";
            Notificar();
            return;
        }

        int circuloUsado = magia.CirculoNivel > 0 ? CirculoConjuracaoSelecionado : 0;

        if (!concluindoCarregamento && circuloUsado > 0 && ObterEspacosRestantesCirculo(circuloUsado) <= 0)
        {
            ErroModalMagia = $"Sem espaços disponíveis no {circuloUsado}º círculo.";
            Notificar();
            return;
        }

        if (!concluindoCarregamento && magia.TempoInfo.TurnosCarregamento > 0)
        {
            if (!ConsumirAcaoDoTurno())
            {
                ErroModalMagia = FeedbackAcao;
                return;
            }

            await ConsumirEspacoMagiaAsync(circuloUsado);
            _idMagiaEmCarregamento = magia.IdMagia;
            _circuloEmCarregamento = circuloUsado;
            TurnosRestantesConjuracao = magia.TempoInfo.TurnosCarregamento;
            ConjuracaoEmCarregamento = true;
            FeedbackAcao = $"Conjuração de {magia.Nome} iniciada; faltam {TurnosRestantesConjuracao} turno(s).";
            Notificar();
            return;
        }

        if (!concluindoCarregamento && !ConsumirAcaoDoTurno())
        {
            ErroModalMagia = FeedbackAcao;
            return;
        }

        if (concluindoCarregamento)
        {
            ConjuracaoEmCarregamento = false;
            TurnosRestantesConjuracao = 0;
            _idMagiaEmCarregamento = 0;
            _circuloEmCarregamento = 0;
        }

        ErroModalMagia = null;

        if (ResolucaoMagiaAtual == ModoResolucaoMagia.AtaqueMagico)
        {
            if (alvo is null)
            {
                ErroModalMagia = "Selecione um alvo válido dentro do alcance.";
                Notificar();
                return;
            }

            int pvMax = alvo.PvMaximo ?? 1;
            int pvAtual = alvo.PvAtual ?? pvMax;
            int caAlvo = alvo.ClasseArmadura ?? 10;
            bool esquivando = alvo.Esquivando;

            int d1 = Random.Shared.Next(1, 21);
            int d2 = esquivando ? Random.Shared.Next(1, 21) : d1;
            int d20 = esquivando ? Math.Min(d1, d2) : d1;
            int modAtk = BonusAtaqueMagico + Math.Clamp(BonusExtraMagia, -50, 50);
            int totalAtk = d20 + modAtk;

            bool falhaCritica = d20 == 1;
            bool critico = d20 == 20;
            bool acertou = !falhaCritica && (critico || totalAtk >= caAlvo);

            await ConsumirEspacoMagiaAsync(circuloUsado);

            ResultadoMagiaAtual = new ResultadoConjuracaoDto
            {
                NomeConjurador = PersonagemConjurador?.Nome ?? _sessaoUsuario.NomeUsuario,
                NomeMagia = magia.Nome,
                CirculoBase = magia.CirculoNivel,
                CirculoUsado = circuloUsado,
                Modo = ModoResolucaoMagia.AtaqueMagico,
                DuracaoTexto = magia.DuracaoInfo.RotuloPtBr,
                Concentracao = magia.DuracaoInfo.SugereConcentracao,
                TipoAlvo = alvo.TipoEntidade,
                IdEntidadeAlvo = alvo.IdEntidade,
                IdPinoAlvo = alvo.IdPino,
                NomeAlvo = alvo.Nome,
                CaAlvo = caAlvo,
                AlvoEsquivando = esquivando,
                DadoD201 = d1,
                DadoD202 = d2,
                DadoD20 = d20,
                ModificadorRolagem = modAtk,
                TotalRolagem = totalAtk,
                EhCritico = critico,
                EhFalhaCritica = falhaCritica,
                Acertou = acertou,
                ExpressaoDado = string.IsNullOrWhiteSpace(ExpressaoDadoMagia) ? "1d8" : ExpressaoDadoMagia.Trim(),
                PvAnterior = pvAtual,
                PvNovo = pvAtual,
                PvMaximo = pvMax
            };

            if (acertou)
            {
                EtapaMagia = 2;
                Notificar();
                return;
            }

            await RegistrarAtaqueMagicoNoServidorAsync();
            return;
        }

        ExecutandoMagia = true;
        Notificar();

        try
        {
            await ConsumirEspacoMagiaAsync(circuloUsado);

            string nomeConjurador = PersonagemConjurador?.Nome ?? _sessaoUsuario.NomeUsuario;
            string nomeAlvo = alvo?.Nome ?? (magia.AlcanceInfo.Categoria == CategoriaAlcanceMagia.Pessoal ? nomeConjurador : "Área");
            string tagCirculo = circuloUsado == 0 ? "Truque" : $"{circuloUsado}º círculo";
            string tagConcentracao = magia.DuracaoInfo.SugereConcentracao ? " · Concentração" : "";

            if (ResolucaoMagiaAtual == ModoResolucaoMagia.Salvaguarda)
            {
                int dadoAlvo = Random.Shared.Next(1, 21);
                int modSaveAlvo = 0;
                int totalSave = dadoAlvo + modSaveAlvo;
                bool alvoPassou = totalSave >= CdResistenciaMagia;

                string expr = string.IsNullOrWhiteSpace(ExpressaoDadoMagia) ? "1d8" : ExpressaoDadoMagia.Trim();
                var (qtd, faces, bonusFormula) = InterpretarExpressaoDado(expr);
                var rolagens = new List<int>(qtd);
                for (int i = 0; i < qtd; i++)
                    rolagens.Add(Random.Shared.Next(1, faces + 1));

                int danoBase = Math.Max(0, rolagens.Sum() + bonusFormula + Math.Clamp(BonusExtraMagia, -50, 50));
                int danoFinal = alvoPassou
                    ? (MeioDanoSePassarSalvaguarda ? danoBase / 2 : 0)
                    : danoBase;

                int pvMax = alvo?.PvMaximo ?? 1;
                int pvAnt = alvo?.PvAtual ?? pvMax;
                int pvNovo = Math.Max(0, pvAnt - danoFinal);

                ResultadoMagiaAtual = new ResultadoConjuracaoDto
                {
                    NomeConjurador = nomeConjurador,
                    NomeMagia = magia.Nome,
                    CirculoBase = magia.CirculoNivel,
                    CirculoUsado = circuloUsado,
                    Modo = ModoResolucaoMagia.Salvaguarda,
                    DuracaoTexto = magia.DuracaoInfo.RotuloPtBr,
                    Concentracao = magia.DuracaoInfo.SugereConcentracao,
                    TipoAlvo = alvo?.TipoEntidade,
                    IdEntidadeAlvo = alvo?.IdEntidade ?? 0,
                    IdPinoAlvo = alvo?.IdPino,
                    NomeAlvo = nomeAlvo,
                    DadoD20 = dadoAlvo,
                    ModificadorRolagem = modSaveAlvo,
                    TotalRolagem = totalSave,
                    CdAlvo = CdResistenciaMagia,
                    AtributoSalvaguarda = AtributoSalvaguardaMagia,
                    AlvoPassouSalvaguarda = alvoPassou,
                    MeioDanoSePassar = MeioDanoSePassarSalvaguarda,
                    Acertou = danoFinal > 0,
                    ExpressaoDado = expr,
                    ResultadosDado = rolagens,
                    BonusExtra = bonusFormula + BonusExtraMagia,
                    ValorTotal = danoFinal,
                    PvAnterior = pvAnt,
                    PvNovo = pvNovo,
                    PvMaximo = pvMax
                };

                await _dbLock.WaitAsync();
                try
                {
                    if (alvo is not null && danoFinal > 0)
                    {
                        var dtoAtaque = new RegistroAtaqueDto
                        {
                            NomeAtacante = nomeConjurador,
                            NomeArma = $"✧ {magia.Nome} ({tagCirculo} · Salv. {AtributoSalvaguardaMagia} CD {CdResistenciaMagia})",
                            ExpressaoDano = expr,
                            TipoDano = "Mágico",
                            TipoAlvo = alvo.TipoEntidade,
                            IdEntidadeAlvo = alvo.IdEntidade,
                            IdPinoAlvo = alvo.IdPino,
                            NomeAlvo = alvo.Nome,
                            CaAlvo = CdResistenciaMagia,
                            DadoAtaque1 = dadoAlvo,
                            DadoAtaque2 = dadoAlvo,
                            DadoAtaque = dadoAlvo,
                            ModificadorAtaque = modSaveAlvo,
                            TotalAtaque = totalSave,
                            Acertou = true,
                            ResultadosDano = rolagens,
                            ModificadorDano = bonusFormula + BonusExtraMagia,
                            DanoTotal = danoFinal,
                            PvAnterior = pvAnt,
                            PvNovo = pvNovo,
                            PvMaximo = pvMax
                        };

                        var (ok, erro) = await _sessaoService.RegistrarAtaqueAsync(
                            Estado.IdSessao,
                            Estado.IdMapaAtivo,
                            _sessaoUsuario.IdUsuarioLogado,
                            dtoAtaque, Estado);

                        if (!ok) ErroModalMagia = erro;
                    }

                    string statusSave = alvoPassou
                        ? (MeioDanoSePassarSalvaguarda ? $"passou (metade do dano: -{danoFinal} PV)" : "passou (sem dano)")
                        : $"falhou (-{danoFinal} PV)";

                    await _sessaoService.EnviarMensagemChatAsync(
                        Estado.IdSessao,
                        _sessaoUsuario.IdUsuarioLogado,
                        $"✧ {nomeConjurador} conjurou {magia.Nome} ({tagCirculo}) em {nomeAlvo}. Salvaguarda de {AtributoSalvaguardaMagia} (CD {CdResistenciaMagia}): d20({dadoAlvo}) → {statusSave}. Duração: {magia.DuracaoInfo.RotuloPtBr}{tagConcentracao}.");

                    var atualizado = await _sessaoService.SincronizarSessaoAsync(
                        Estado.IdCampanha,
                        Estado.IdSessao,
                        _sessaoUsuario.IdUsuarioLogado);

                    if (atualizado != null)
                        Estado = atualizado;
                }
                finally
                {
                    _dbLock.Release();
                }

                EtapaMagia = 3;
                return;
            }

            if (ResolucaoMagiaAtual == ModoResolucaoMagia.Cura)
            {
                string expr = string.IsNullOrWhiteSpace(ExpressaoDadoMagia) ? "1d8" : ExpressaoDadoMagia.Trim();
                var (qtd, faces, bonusFormula) = InterpretarExpressaoDado(expr);
                var rolagens = new List<int>(qtd);
                for (int i = 0; i < qtd; i++)
                    rolagens.Add(Random.Shared.Next(1, faces + 1));

                int modConjuracao = BonusAtaqueMagico - RegrasMagiaCombate.CalcularBonusProficiencia(PersonagemConjurador?.Nivel ?? 1);
                int bonusTotal = modConjuracao + bonusFormula + Math.Clamp(BonusExtraMagia, -50, 50);
                int curaTotal = Math.Max(1, rolagens.Sum() + bonusTotal);

                int pvMax = alvo?.PvMaximo ?? 1;
                int pvAnt = alvo?.PvAtual ?? pvMax;
                int pvNovo = Math.Min(pvMax, pvAnt + curaTotal);

                ResultadoMagiaAtual = new ResultadoConjuracaoDto
                {
                    NomeConjurador = nomeConjurador,
                    NomeMagia = magia.Nome,
                    CirculoBase = magia.CirculoNivel,
                    CirculoUsado = circuloUsado,
                    Modo = ModoResolucaoMagia.Cura,
                    DuracaoTexto = magia.DuracaoInfo.RotuloPtBr,
                    Concentracao = magia.DuracaoInfo.SugereConcentracao,
                    NomeAlvo = nomeAlvo,
                    Acertou = true,
                    ExpressaoDado = expr,
                    ResultadosDado = rolagens,
                    BonusExtra = bonusTotal,
                    ValorTotal = curaTotal,
                    PvAnterior = pvAnt,
                    PvNovo = pvNovo,
                    PvMaximo = pvMax
                };

                await _dbLock.WaitAsync();
                try
                {
                    await _sessaoService.EnviarMensagemChatAsync(
                        Estado.IdSessao,
                        _sessaoUsuario.IdUsuarioLogado,
                        $"💚 {nomeConjurador} conjurou {magia.Nome} ({tagCirculo}) em {nomeAlvo}: +{curaTotal} PV ({expr} [{string.Join("+", rolagens)}] {(bonusTotal >= 0 ? $"+ {bonusTotal}" : $"− {Math.Abs(bonusTotal)}")}). Duração: {magia.DuracaoInfo.RotuloPtBr}{tagConcentracao}.");

                    var atualizado = await _sessaoService.SincronizarSessaoAsync(
                        Estado.IdCampanha,
                        Estado.IdSessao,
                        _sessaoUsuario.IdUsuarioLogado);

                    if (atualizado != null)
                        Estado = atualizado;
                }
                finally
                {
                    _dbLock.Release();
                }

                EtapaMagia = 3;
                return;
            }

            ResultadoMagiaAtual = new ResultadoConjuracaoDto
            {
                NomeConjurador = nomeConjurador,
                NomeMagia = magia.Nome,
                CirculoBase = magia.CirculoNivel,
                CirculoUsado = circuloUsado,
                Modo = ModoResolucaoMagia.Efeito,
                DuracaoTexto = magia.DuracaoInfo.RotuloPtBr,
                Concentracao = magia.DuracaoInfo.SugereConcentracao,
                NomeAlvo = nomeAlvo,
                Acertou = true
            };

            await _dbLock.WaitAsync();
            try
            {
                await _sessaoService.EnviarMensagemChatAsync(
                    Estado.IdSessao,
                    _sessaoUsuario.IdUsuarioLogado,
                    $"✧ {nomeConjurador} conjurou {magia.Nome} ({tagCirculo}) em {nomeAlvo}. Alcance: {magia.AlcanceInfo.RotuloPtBr} · Duração: {magia.DuracaoInfo.RotuloPtBr}{tagConcentracao}.");

                var atualizado = await _sessaoService.SincronizarSessaoAsync(
                    Estado.IdCampanha,
                    Estado.IdSessao,
                    _sessaoUsuario.IdUsuarioLogado);

                if (atualizado != null)
                    Estado = atualizado;
            }
            finally
            {
                _dbLock.Release();
            }

            EtapaMagia = 3;
        }
        catch (Exception ex)
        {
            ErroModalMagia = $"Erro ao conjurar magia: {ex.Message}";
        }
        finally
        {
            ExecutandoMagia = false;
            Notificar();
        }
    }

    public async Task RolarDanoMagiaEAplicarAsync()
    {
        if (ExecutandoMagia || ResultadoMagiaAtual is null || !ResultadoMagiaAtual.Acertou)
            return;

        var (qtd, faces, bonusFormula) = InterpretarExpressaoDado(ResultadoMagiaAtual.ExpressaoDado);
        int qtdDados = ResultadoMagiaAtual.EhCritico ? qtd * 2 : qtd;

        var rolagens = new List<int>(qtdDados);
        for (int i = 0; i < qtdDados; i++)
            rolagens.Add(Random.Shared.Next(1, faces + 1));

        int modTotal = bonusFormula + Math.Clamp(BonusExtraMagia, -50, 50);
        int danoTotal = Math.Max(0, rolagens.Sum() + modTotal);
        int pvNovo = Math.Max(0, ResultadoMagiaAtual.PvAnterior - danoTotal);

        ResultadoMagiaAtual.ResultadosDado = rolagens;
        ResultadoMagiaAtual.BonusExtra = modTotal;
        ResultadoMagiaAtual.ValorTotal = danoTotal;
        ResultadoMagiaAtual.PvNovo = pvNovo;

        await RegistrarAtaqueMagicoNoServidorAsync();
    }

    private async Task RegistrarAtaqueMagicoNoServidorAsync()
    {
        if (ResultadoMagiaAtual is null) return;

        ExecutandoMagia = true;
        ErroModalMagia = null;
        Notificar();

        var r = ResultadoMagiaAtual;
        string tagCirculo = r.CirculoUsado == 0 ? "Truque" : $"{r.CirculoUsado}º círculo";

        var dtoAtaque = new RegistroAtaqueDto
        {
            NomeAtacante = r.NomeConjurador,
            NomeArma = $"✧ {r.NomeMagia} ({tagCirculo})",
            ExpressaoDano = r.ExpressaoDado,
            TipoDano = "Mágico",
            TipoAlvo = r.TipoAlvo ?? TiposPinoSessao.Monstro,
            IdEntidadeAlvo = r.IdEntidadeAlvo,
            IdPinoAlvo = r.IdPinoAlvo,
            NomeAlvo = r.NomeAlvo,
            CaAlvo = r.CaAlvo,
            AlvoEsquivando = r.AlvoEsquivando,
            DadoAtaque1 = r.DadoD201,
            DadoAtaque2 = r.DadoD202,
            DadoAtaque = r.DadoD20,
            ModificadorAtaque = r.ModificadorRolagem,
            TotalAtaque = r.TotalRolagem,
            EhCritico = r.EhCritico,
            Acertou = r.Acertou,
            ResultadosDano = r.ResultadosDado,
            ModificadorDano = r.BonusExtra,
            DanoTotal = r.ValorTotal,
            PvAnterior = r.PvAnterior,
            PvNovo = r.PvNovo,
            PvMaximo = r.PvMaximo
        };

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.RegistrarAtaqueAsync(
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                dtoAtaque, Estado);

            if (!sucesso)
                ErroModalMagia = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            EtapaMagia = 3;
        }
        finally
        {
            ExecutandoMagia = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public void NovaConjuracao()
    {
        EtapaMagia = 1;
        ResultadoMagiaAtual = null;
        ErroModalMagia = null;
        AtualizarCirculoPadraoMagia();
        SelecionarPrimeiroAlvoMagiaValido();
        Notificar();
    }

    public void FecharModalMagia()
    {
        if (ExecutandoMagia) return;
        ExibirModalMagia = false;
        ErroModalMagia = null;
        EtapaMagia = 1;
        ResultadoMagiaAtual = null;
        Notificar();
    }
}
