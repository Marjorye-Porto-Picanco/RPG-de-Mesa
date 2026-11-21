using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class SessaoIniciarViewModel
    {
        private const long TamanhoMaximoMapa = 5 * 1024 * 1024; // 5 MB
        private const int LadoMaximoFotoNpc = 512;
        private const long LimiteOriginalFotoNpc = 10 * 1024 * 1024; // 10 MB
        private const long LimiteFinalFotoNpc = 2 * 1024 * 1024; // 2 MB
        private static readonly string? ImagemPadraoNpc = new NPC().Imagem;

        private readonly CampanhaService _campanhaService;
        private readonly PersonagemService _personagemService;
        private readonly NpcService _npcService;
        private readonly ItemService _itemService;
        private readonly SessaoService _sessaoService;
        private readonly NavigationManager _navigation;

        public SessaoIniciarViewModel(
            CampanhaService campanhaService,
            PersonagemService personagemService,
            NpcService npcService,
            ItemService itemService,
            SessaoService sessaoService,
            NavigationManager navigation)
        {
            _campanhaService = campanhaService;
            _personagemService = personagemService;
            _npcService = npcService;
            _itemService = itemService;
            _sessaoService = sessaoService;
            _navigation = navigation;
        }

        public int IdCampanha { get; set; }
        public Campanha? CampanhaAtual { get; private set; }
        public List<Monstro> MonstrosDisponiveis { get; private set; } = new();
        public List<NPC> NpcsDisponiveis { get; private set; } = new();
        public List<Item> ItensDisponiveis { get; private set; } = new();
        public List<Mapa> MapasDisponiveis { get; private set; } = new();
        public string TermoBusca { get; set; } = string.Empty;

        public string AbaMenu { get; set; } = "iniciar";
        public string CategoriaAtiva { get; set; } = "todos";
        public string ModoVisualizacao { get; set; } = "vinculados";
        public string CategoriaSelecionada { get; set; } = "npc";

        // Histórico de Sessões
        public List<HistoricoSessaoDto> HistoricoSessoes { get; private set; } = new();
        public HistoricoSessaoDto? SessaoHistoricoSelecionada { get; private set; }
        public string TituloHistoricoEdicao { get; set; } = string.Empty;
        public string ResumoHistoricoEdicao { get; set; } = string.Empty;
        public bool SalvandoHistorico { get; private set; }
        public string MensagemFeedbackHistorico { get; private set; } = string.Empty;

        // Chat da Sessão no Histórico
        public bool ExibirChatHistorico { get; private set; }
        public bool CarregandoChatHistorico { get; private set; }
        public List<MensagemSessaoDto> ChatHistoricoMensagens { get; private set; } = new();
        private int _idSessaoChatCarregado;

        // Mapas da Campanha
        public string AbaMapas { get; set; } = "vinculados";
        public string TermoBuscaMapa { get; set; } = string.Empty;
        public Mapa? MapaSelecionadoParaVer { get; private set; }
        public string NovoMapaNome { get; set; } = string.Empty;
        public string NovoMapaEscala { get; set; } = string.Empty;
        public string? NovoMapaImagemBase64 { get; private set; }
        public string? NovoMapaNomeArquivo { get; private set; }
        public string MensagemFeedbackMapas { get; private set; } = string.Empty;

        // Fichas do Mestre
        public string FiltroFichasMestre { get; set; } = "pendentes";
        public Personagem? FichaInspecionada { get; private set; }
        public string AbaInspecao { get; set; } = "atributos";
        public string SecaoDiarioInspecao { get; set; } = "ideais";
        public string MensagemFeedbackFichas { get; private set; } = string.Empty;

        public int IdPersonagemParaRecusar { get; set; } = 0;
        public string TextoMotivoRecusa { get; set; } = string.Empty;

        // Form Fields - NPC
        public string NpcNome { get; set; } = string.Empty;
        public string NpcRaca { get; set; } = "Humano";
        public string NpcClasse { get; set; } = "Guerreiro";
        public string NpcTendencia { get; set; } = "Neutra";
        public string NpcAntecedente { get; set; } = string.Empty;
        public string NpcHistoria { get; set; } = string.Empty;
        public int NpcNivel { get; set; } = 1;
        public int NpcForca { get; set; } = 10;
        public int NpcDestreza { get; set; } = 10;
        public int NpcConstituicao { get; set; } = 10;
        public int NpcInteligencia { get; set; } = 10;
        public int NpcSabedoria { get; set; } = 10;
        public int NpcCarisma { get; set; } = 10;
        public string? NpcImagem { get; set; } = ImagemPadraoNpc;
        public string? ErroImagemNpc { get; private set; }
        public int ChaveInputFotoNpc { get; private set; }
        public bool TemFotoNpc => NpcImagem?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true;

        // Form Fields - Monstro
        public string MonstroNome { get; set; } = string.Empty;
        public string MonstroTipo { get; set; } = "Outro";
        public string MonstroTamanho { get; set; } = "Médio";
        public string MonstroAlinhamento { get; set; } = string.Empty;
        public string MonstroDeslocamento { get; set; } = "9 m";
        public string MonstroSentidos { get; set; } = string.Empty;
        public string MonstroResistencias { get; set; } = string.Empty;
        public string MonstroHabilidades { get; set; } = string.Empty;
        public string MonstroDescricao { get; set; } = string.Empty;
        public decimal MonstroNd { get; set; } = 1;
        public int MonstroDefesa { get; set; } = 12;
        public int MonstroVida { get; set; } = 15;

        // Form Fields - Item
        public string ItemNome { get; set; } = string.Empty;
        public string ItemTipo { get; set; } = string.Empty;
        public string ItemDescricao { get; set; } = string.Empty;
        public decimal? ItemPeso { get; set; } = null;
        public decimal? ItemPreco { get; set; } = null;
        public string? ItemDano { get; set; } = string.Empty;
        public string? ItemTipoDano { get; set; } = string.Empty;
        public string? ItemPropriedades { get; set; } = string.Empty;
        public int ItemCaBonus { get; set; } = 0;
        public string ItemRaridade { get; set; } = string.Empty;
        public string ItemSintonizacao { get; set; } = "Não";

        public string MensagemFeedback { get; private set; } = string.Empty;

        public event Action? OnChange;
        public void NotifyStateChanged() => OnChange?.Invoke();

        // Propriedades Calculadas (LINQ)
        public IEnumerable<PersonagemCampanha> FichasPendentes =>
            CampanhaAtual?.FichasPendentes ?? Enumerable.Empty<PersonagemCampanha>();

        public IEnumerable<PersonagemCampanha> FichasAceitas =>
            CampanhaAtual?.FichasAceitas ?? Enumerable.Empty<PersonagemCampanha>();

        public string StatusDaFichaInspecionada =>
            CampanhaAtual?.PersonagemCampanhas
                .FirstOrDefault(pc => pc.IdPersonagem == FichaInspecionada?.IdPersonagem)?.StatusAprovacao ?? "Pendente";

        public IEnumerable<Mapa> MapasVinculadosFiltrados =>
            (CampanhaAtual?.MapaCampanhas ?? Enumerable.Empty<MapaCampanha>())
                .Where(mc => mc.Mapa != null)
                .Select(mc => mc.Mapa!)
                .Where(m =>
                    string.IsNullOrWhiteSpace(TermoBuscaMapa) ||
                    m.NomeRegiao.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase) ||
                    (m.Escala != null && m.Escala.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<Mapa> MapasDisponiveisFiltrados =>
            MapasDisponiveis.Where(m =>
                string.IsNullOrWhiteSpace(TermoBuscaMapa) ||
                m.NomeRegiao.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase) ||
                (m.Escala != null && m.Escala.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<NPC> NpcsVinculadosFiltrados =>
            (CampanhaAtual?.Npcs ?? Array.Empty<NPC>()).Where(n =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                n.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (n.ClasseTexto != null && n.ClasseTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
                (n.AntecedenteTexto != null && n.AntecedenteTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
                (n.Ocupacao != null && n.Ocupacao.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<MonstroCampanha> MonstrosVinculadosFiltrados =>
            (CampanhaAtual?.MonstroCampanhas ?? Enumerable.Empty<MonstroCampanha>()).Where(mc =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                (mc.Monstro != null && (
                    mc.Monstro.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                    (mc.Monstro.TipoMonstro != null && mc.Monstro.TipoMonstro.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase))
                )));

        public IEnumerable<ItemCampanha> ItensVinculadosFiltrados =>
            (CampanhaAtual?.ItemCampanhas ?? Enumerable.Empty<ItemCampanha>()).Where(ic =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                (ic.Item != null && (
                    ic.Item.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                    (ic.Item.Tipo != null && ic.Item.Tipo.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase))
                )));

        public IEnumerable<NPC> NpcsDisponiveisFiltrados =>
            NpcsDisponiveis.Where(n =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                n.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (n.ClasseTexto != null && n.ClasseTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
                (n.AntecedenteTexto != null && n.AntecedenteTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
                (n.Ocupacao != null && n.Ocupacao.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<Monstro> MonstrosDisponiveisFiltrados =>
            MonstrosDisponiveis.Where(m =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                m.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (m.TipoMonstro != null && m.TipoMonstro.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<Item> ItensDisponiveisFiltrados =>
            ItensDisponiveis.Where(i =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                i.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (i.Tipo != null && i.Tipo.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public async Task CarregarDadosCampanhaAsync(int idCampanha)
        {
            IdCampanha = idCampanha;
            if (IdCampanha > 0)
            {
                CampanhaAtual = await _campanhaService.ObterDetalhesCampanhaAsync(IdCampanha);
                if (CampanhaAtual != null)
                {
                    var todosMonstros = await _campanhaService.ObterMonstrosParaSessaoAsync(CampanhaAtual.IdAccount);
                    var idsMonstrosVinculados = CampanhaAtual.MonstroCampanhas.Select(mc => mc.IdMonstro).ToList();
                    MonstrosDisponiveis = todosMonstros.Where(m => !idsMonstrosVinculados.Contains(m.IdMonstro)).ToList();

                    var todosNpcsUsuario = await _npcService.ObterNpcsPorUsuarioAsync(CampanhaAtual.IdAccount);
                    var idsNpcsVinculados = CampanhaAtual.NpcCampanhas.Select(nc => nc.IdNpc).ToList();
                    NpcsDisponiveis = todosNpcsUsuario.Where(n => !idsNpcsVinculados.Contains(n.IdNpc)).ToList();

                    var todosItens = await _campanhaService.ObterItensParaSessaoAsync(CampanhaAtual.IdAccount);
                    var idsItensVinculados = CampanhaAtual.ItemCampanhas.Select(ic => ic.IdItem).ToList();
                    ItensDisponiveis = todosItens.Where(i => !idsItensVinculados.Contains(i.IdItem)).ToList();

                    var todosMapasUsuario = await _campanhaService.ObterMapasDoUsuarioAsync(CampanhaAtual.IdAccount);
                    var idsMapasVinculados = CampanhaAtual.MapaCampanhas.Select(mc => mc.IdMapa).ToList();
                    MapasDisponiveis = todosMapasUsuario.Where(m => !idsMapasVinculados.Contains(m.IdMapa)).ToList();

                    if (MapaSelecionadoParaVer != null && !idsMapasVinculados.Contains(MapaSelecionadoParaVer.IdMapa))
                    {
                        MapaSelecionadoParaVer = null;
                    }

                    await CarregarHistoricoInternoAsync();
                }
            }
            NotifyStateChanged();
        }

        private async Task CarregarHistoricoInternoAsync()
        {
            int? idSelecionadoAntes = SessaoHistoricoSelecionada?.IdSessao;
            HistoricoSessoes = await _sessaoService.ObterHistoricoSessoesAsync(IdCampanha);

            if (idSelecionadoAntes.HasValue)
            {
                SessaoHistoricoSelecionada = HistoricoSessoes.FirstOrDefault(s => s.IdSessao == idSelecionadoAntes.Value)
                    ?? HistoricoSessoes.FirstOrDefault();
            }
            else
            {
                SessaoHistoricoSelecionada = HistoricoSessoes.FirstOrDefault();
            }

            if (SessaoHistoricoSelecionada != null)
            {
                TituloHistoricoEdicao = SessaoHistoricoSelecionada.Titulo;
                ResumoHistoricoEdicao = SessaoHistoricoSelecionada.Resumo ?? string.Empty;
            }
            else
            {
                TituloHistoricoEdicao = string.Empty;
                ResumoHistoricoEdicao = string.Empty;
            }

            if (SessaoHistoricoSelecionada?.IdSessao != _idSessaoChatCarregado)
            {
                ExibirChatHistorico = false;
                ChatHistoricoMensagens.Clear();
                _idSessaoChatCarregado = 0;
            }
        }

        public void SelecionarSessaoHistorico(HistoricoSessaoDto sessao)
        {
            SessaoHistoricoSelecionada = sessao;
            TituloHistoricoEdicao = sessao.Titulo;
            ResumoHistoricoEdicao = sessao.Resumo ?? string.Empty;
            MensagemFeedbackHistorico = string.Empty;
            ExibirChatHistorico = false;
            ChatHistoricoMensagens.Clear();
            _idSessaoChatCarregado = 0;
            NotifyStateChanged();
        }

        public async Task AlternarChatHistoricoAsync()
        {
            if (SessaoHistoricoSelecionada == null)
                return;

            if (ExibirChatHistorico)
            {
                ExibirChatHistorico = false;
                NotifyStateChanged();
                return;
            }

            ExibirChatHistorico = true;

            if (_idSessaoChatCarregado != SessaoHistoricoSelecionada.IdSessao)
            {
                CarregandoChatHistorico = true;
                NotifyStateChanged();

                try
                {
                    ChatHistoricoMensagens = await _sessaoService.ObterChatHistoricoSessaoAsync(SessaoHistoricoSelecionada.IdSessao);
                    _idSessaoChatCarregado = SessaoHistoricoSelecionada.IdSessao;
                }
                finally
                {
                    CarregandoChatHistorico = false;
                }
            }

            NotifyStateChanged();
        }

        public async Task SalvarResumoHistoricoAsync()
        {
            if (CampanhaAtual == null || SessaoHistoricoSelecionada == null || SalvandoHistorico)
                return;

            SalvandoHistorico = true;
            MensagemFeedbackHistorico = string.Empty;
            NotifyStateChanged();

            try
            {
                var (sucesso, mensagem) = await _sessaoService.SalvarResumoHistoricoAsync(
                    IdCampanha,
                    SessaoHistoricoSelecionada.IdSessao,
                    CampanhaAtual.IdAccount,
                    TituloHistoricoEdicao,
                    ResumoHistoricoEdicao);

                MensagemFeedbackHistorico = mensagem;
                if (sucesso)
                {
                    await CarregarHistoricoInternoAsync();
                }
            }
            finally
            {
                SalvandoHistorico = false;
                NotifyStateChanged();
            }
        }

        // --- FOTO DO NPC ---
        public async Task SelecionarFotoNpcAsync(InputFileChangeEventArgs evento)
        {
            ErroImagemNpc = null;
            var arquivo = evento.File;

            if (string.IsNullOrWhiteSpace(arquivo.ContentType) ||
                !arquivo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                ErroImagemNpc = "Selecione um arquivo de imagem válido.";
                NotifyStateChanged();
                return;
            }

            if (arquivo.Size > LimiteOriginalFotoNpc)
            {
                ErroImagemNpc = "A imagem deve ter no máximo 10 MB.";
                NotifyStateChanged();
                return;
            }

            try
            {
                IBrowserFile origem = arquivo;
                try
                {
                    var formato = arquivo.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                        ? "image/jpeg"
                        : "image/png";
                    origem = await arquivo.RequestImageFileAsync(formato, LadoMaximoFotoNpc, LadoMaximoFotoNpc);
                }
                catch (Exception) when (arquivo.Size <= LimiteFinalFotoNpc)
                {
                    origem = arquivo;
                }

                await using var leitura = origem.OpenReadStream(LimiteFinalFotoNpc);
                using var memoria = new MemoryStream();
                await leitura.CopyToAsync(memoria);

                NpcImagem = $"data:{origem.ContentType};base64,{Convert.ToBase64String(memoria.ToArray())}";
            }
            catch (Exception)
            {
                ErroImagemNpc = "Não foi possível carregar essa imagem. Tente outro arquivo (até 2 MB).";
            }
            finally
            {
                NotifyStateChanged();
            }
        }

        public void RemoverFotoNpc()
        {
            NpcImagem = ImagemPadraoNpc;
            ErroImagemNpc = null;
            ChaveInputFotoNpc++;
            NotifyStateChanged();
        }

        private void LimparFormularioNpc()
        {
            NpcNome = string.Empty;
            NpcRaca = "Humano";
            NpcClasse = "Guerreiro";
            NpcNivel = 1;
            NpcTendencia = "Neutra";
            NpcAntecedente = string.Empty;
            NpcForca = 10;
            NpcDestreza = 10;
            NpcConstituicao = 10;
            NpcInteligencia = 10;
            NpcSabedoria = 10;
            NpcCarisma = 10;
            NpcHistoria = string.Empty;
            NpcImagem = ImagemPadraoNpc;
            ErroImagemNpc = null;
            ChaveInputFotoNpc++;
        }

        // --- MÉTODOS DE MAPAS ---
        public void SelecionarMapaParaVisualizar(Mapa? mapa)
        {
            if (mapa != null && MapaSelecionadoParaVer?.IdMapa == mapa.IdMapa)
                MapaSelecionadoParaVer = null;
            else
                MapaSelecionadoParaVer = mapa;

            NotifyStateChanged();
        }

        public async Task SelecionarImagemNovoMapaAsync(InputFileChangeEventArgs e)
        {
            var arquivo = e.File;
            if (arquivo is null) return;

            try
            {
                using var stream = arquivo.OpenReadStream(TamanhoMaximoMapa);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var base64 = Convert.ToBase64String(ms.ToArray());

                NovoMapaImagemBase64 = $"data:{arquivo.ContentType};base64,{base64}";
                NovoMapaNomeArquivo = arquivo.Name;

                if (string.IsNullOrWhiteSpace(NovoMapaNome))
                    NovoMapaNome = Path.GetFileNameWithoutExtension(arquivo.Name);

                MensagemFeedbackMapas = string.Empty;
            }
            catch (Exception ex)
            {
                MensagemFeedbackMapas = $"Erro ao carregar imagem (máx. 5MB): {ex.Message}";
            }
            finally
            {
                NotifyStateChanged();
            }
        }

        public async Task CriarEVincularNovoMapaAsync()
        {
            if (CampanhaAtual == null) return;

            if (string.IsNullOrWhiteSpace(NovoMapaNome))
            {
                MensagemFeedbackMapas = "Informe o nome da região/mapa.";
                NotifyStateChanged();
                return;
            }

            if (string.IsNullOrWhiteSpace(NovoMapaImagemBase64))
            {
                MensagemFeedbackMapas = "Selecione uma imagem para o mapa.";
                NotifyStateChanged();
                return;
            }

            var novoMapa = new Mapa
            {
                IdAccount = CampanhaAtual.IdAccount,
                NomeRegiao = NovoMapaNome.Trim(),
                Escala = string.IsNullOrWhiteSpace(NovoMapaEscala) ? null : NovoMapaEscala.Trim(),
                ImagemUrl = NovoMapaImagemBase64
            };

            var resultado = await _campanhaService.CriarEVincularMapaNaCampanhaAsync(IdCampanha, novoMapa);
            MensagemFeedbackMapas = resultado.Mensagem;

            if (resultado.Sucesso)
            {
                NovoMapaNome = string.Empty;
                NovoMapaEscala = string.Empty;
                NovoMapaImagemBase64 = null;
                NovoMapaNomeArquivo = null;
                AbaMapas = "vinculados";
                await CarregarDadosCampanhaAsync(IdCampanha);
                MapaSelecionadoParaVer = CampanhaAtual?.MapaCampanhas
                    .Select(mc => mc.Mapa)
                    .FirstOrDefault(m => m != null && m.IdMapa == novoMapa.IdMapa);
                NotifyStateChanged();
            }
            else
            {
                NotifyStateChanged();
            }
        }

        public async Task VincularMapaExistenteAsync(int idMapa)
        {
            var resultado = await _campanhaService.AdicionarMapaNaCampanhaAsync(IdCampanha, idMapa);
            MensagemFeedbackMapas = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularMapaAsync(int idMapa)
        {
            var resultado = await _campanhaService.RemoverMapaDaCampanhaAsync(IdCampanha, idMapa);
            MensagemFeedbackMapas = resultado.Mensagem;
            if (MapaSelecionadoParaVer?.IdMapa == idMapa)
                MapaSelecionadoParaVer = null;

            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        // --- FICHAS ---
        public async Task InspecionarFichaNaAbaAsync(int idPersonagem)
        {
            AbaMenu = "fichas";
            FiltroFichasMestre = "aceitas";
            await CarregarFichaParaInspecaoAsync(idPersonagem);
        }

        public async Task CarregarFichaParaInspecaoAsync(int idPersonagem)
        {
            FichaInspecionada = await _personagemService.ObterPorIdAsync(idPersonagem);
            AbaInspecao = "atributos";
            SecaoDiarioInspecao = "ideais";
            NotifyStateChanged();
        }

        public async Task AprovarFichaJogadorAsync(int idPersonagem)
        {
            var resultado = await _campanhaService.AprovarFichaCampanhaAsync(IdCampanha, idPersonagem);
            MensagemFeedbackFichas = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
            if (FichaInspecionada?.IdPersonagem == idPersonagem)
            {
                FichaInspecionada = await _personagemService.ObterPorIdAsync(idPersonagem);
            }
            NotifyStateChanged();
        }

        public void AbrirModalRecusa(int idPersonagem)
        {
            IdPersonagemParaRecusar = idPersonagem;
            TextoMotivoRecusa = string.Empty;
            NotifyStateChanged();
        }

        public async Task ConfirmarRecusaComMotivoAsync()
        {
            if (IdPersonagemParaRecusar <= 0) return;

            var resultado = await _campanhaService.RecusarFichaCampanhaAsync(IdCampanha, IdPersonagemParaRecusar, TextoMotivoRecusa);
            MensagemFeedbackFichas = resultado.Mensagem;
            if (FichaInspecionada?.IdPersonagem == IdPersonagemParaRecusar)
            {
                FichaInspecionada = null;
            }
            IdPersonagemParaRecusar = 0;
            TextoMotivoRecusa = string.Empty;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        // --- CONFIGURAÇÕES (NPC, MONSTRO, ITEM) ---
        public async Task SalvarRegistroAsync()
        {
            if (CampanhaAtual == null) return;

            try
            {
                if (CategoriaSelecionada == "npc")
                {
                    var novoNpc = new NPC(
                        idAccount: CampanhaAtual.IdAccount,
                        nome: NpcNome,
                        racaTexto: NpcRaca,
                        classeTexto: NpcClasse,
                        nivel: Math.Clamp(NpcNivel, 1, 20),
                        antecedenteTexto: NpcAntecedente,
                        tendencia: NpcTendencia,
                        forca: Math.Clamp(NpcForca, 1, 30),
                        destreza: Math.Clamp(NpcDestreza, 1, 30),
                        constituicao: Math.Clamp(NpcConstituicao, 1, 30),
                        inteligencia: Math.Clamp(NpcInteligencia, 1, 30),
                        sabedoria: Math.Clamp(NpcSabedoria, 1, 30),
                        carisma: Math.Clamp(NpcCarisma, 1, 30),
                        historia: NpcHistoria,
                        imagem: NpcImagem,
                        idCampanha: IdCampanha
                    );

                    var resultado = await _npcService.CriarNpcAsync(CampanhaAtual.IdAccount, novoNpc, IdCampanha);
                    MensagemFeedback = resultado.Sucesso ? "NPC cadastrado e vinculado à campanha!" : resultado.Mensagem;
                    if (resultado.Sucesso)
                    {
                        LimparFormularioNpc();
                    }
                }
                else if (CategoriaSelecionada == "monstro")
                {
                    var novoMonstro = new Monstro
                    {
                        Nome = MonstroNome,
                        TipoMonstro = MonstroTipo,
                        NivelDesafio = MonstroNd,
                        PvMaximo = MonstroVida,
                        ClasseArmadura = MonstroDefesa,
                        Tamanho = MonstroTamanho,
                        Tendencia = MonstroAlinhamento,
                        Deslocamento = MonstroDeslocamento,
                        Sentidos = MonstroSentidos,
                        Resistencias = MonstroResistencias,
                        Habilidades = MonstroHabilidades,
                        Descricao = MonstroDescricao
                    };

                    var resultado = await _campanhaService.CriarE_VincularMonstroNaCampanhaAsync(IdCampanha, novoMonstro);
                    MensagemFeedback = resultado.Mensagem;
                    if (resultado.Sucesso) { MonstroNome = MonstroDescricao = string.Empty; }
                }
                else if (CategoriaSelecionada == "item")
                {
                    var novoItem = new Item(
                        idAccount: CampanhaAtual.IdAccount,
                        nome: ItemNome,
                        tipo: ItemTipo,
                        peso: ItemPeso,
                        preco: ItemPreco,
                        dano: string.IsNullOrWhiteSpace(ItemDano) ? null : ItemDano,
                        tipoDano: string.IsNullOrWhiteSpace(ItemTipoDano) ? null : ItemTipoDano,
                        propriedades: string.IsNullOrWhiteSpace(ItemPropriedades) ? null : ItemPropriedades,
                        caBonus: ItemCaBonus,
                        descricao: ItemDescricao,
                        raridade: string.IsNullOrEmpty(ItemRaridade) ? null : ItemRaridade,
                        sintonizacao: ItemSintonizacao
                    );

                    var resultadoCriacao = await _itemService.CriarItemAsync(CampanhaAtual.IdAccount, novoItem);
                    if (resultadoCriacao.Sucesso)
                    {
                        if (resultadoCriacao.IdItemCriado > 0)
                        {
                            await _campanhaService.AdicionarItemNaCampanhaAsync(IdCampanha, resultadoCriacao.IdItemCriado);
                        }

                        MensagemFeedback = "Item cadastrado e vinculado com sucesso!";
                        ItemNome = ItemTipo = ItemDescricao = string.Empty;
                        ItemDano = ItemTipoDano = ItemPropriedades = string.Empty;
                        ItemPeso = null;
                        ItemPreco = null;
                        ItemCaBonus = 0;
                        ItemRaridade = string.Empty;
                        ItemSintonizacao = "Não";
                    }
                    else
                    {
                        MensagemFeedback = resultadoCriacao.Mensagem;
                    }
                }

                await CarregarDadosCampanhaAsync(IdCampanha);
            }
            catch (Exception ex)
            {
                MensagemFeedback = $"Erro: {ex.Message}";
                NotifyStateChanged();
            }
        }

        public async Task VincularMonstro(int idMonstro)
        {
            var resultado = await _campanhaService.AdicionarMonstroNaCampanhaAsync(IdCampanha, idMonstro);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task VincularNpcExistente(int idNpc)
        {
            var resultado = await _campanhaService.AdicionarNpcNaCampanhaAsync(IdCampanha, idNpc);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task VincularItem(int idItem)
        {
            var resultado = await _campanhaService.AdicionarItemNaCampanhaAsync(IdCampanha, idItem);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularMonstro(int idMonstro)
        {
            var resultado = await _campanhaService.RemoverMonstroDaCampanhaAsync(IdCampanha, idMonstro);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularNpc(int idNpc)
        {
            var resultado = await _campanhaService.RemoverNpcDaCampanhaAsync(IdCampanha, idNpc);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularItem(int idItem)
        {
            var resultado = await _campanhaService.RemoverItemDaCampanhaAsync(IdCampanha, idItem);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public static string ObterModificador(int valor)
        {
            var modificador = (int)Math.Floor((valor - 10) / 2.0);
            return modificador >= 0 ? $"+{modificador}" : modificador.ToString();
        }

        public void AbrirIniciar() { AbaMenu = "iniciar"; NotifyStateChanged(); }
        public void AbrirConfig() { AbaMenu = "config"; NotifyStateChanged(); }
        public void AbrirMapas() { AbaMenu = "mapas"; NotifyStateChanged(); }
        public void AbrirFichas()
        {
            AbaMenu = "fichas";
            FiltroFichasMestre = FichasPendentes.Any() ? "pendentes" : "aceitas";
            NotifyStateChanged();
        }
        public async Task AbrirHistAsync()
        {
            AbaMenu = "hist";
            MensagemFeedbackHistorico = string.Empty;
            if (IdCampanha > 0)
            {
                await CarregarHistoricoInternoAsync();
            }
            NotifyStateChanged();
        }
    }
}