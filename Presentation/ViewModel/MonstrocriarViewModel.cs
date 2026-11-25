using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class MonstroCriarViewModel
    {
        private readonly MonstroService _monstroService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;
        private readonly AppCacheService _appCache;
        private readonly Dictionary<MonstroAtaque, int> _origemCatalogo = new();

        public MonstroCriarViewModel(
            MonstroService monstroService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation,
            AppCacheService appCache)
        {
            _monstroService = monstroService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            _appCache = appCache;
            LimparFormulario();
        }

        public Monstro MonstroAtual { get; set; } = new();
        public List<MonstroAtaque> Ataques { get; private set; } = new();
        public MonstroAtaque NovoAtaque { get; private set; } = new();
        public MonstroAtaque? AtaqueEmVisualizacao { get; private set; }
        public bool ExibirFormularioNovoAtaque { get; private set; }
        public string ErroNovoAtaque { get; private set; } = string.Empty;
        public List<Monstro> CatalogoMonstrosAtaques { get; private set; } = new();
        public HashSet<int> AtaquesCatalogoAdicionados { get; } = new();
        public string BuscaAtaqueCatalogo { get; set; } = string.Empty;

        public IEnumerable<(Monstro Monstro, MonstroAtaque Ataque)> AtaquesCatalogoFiltrados => CatalogoMonstrosAtaques
            .Where(m => m.IdMonstro != MonstroAtual.IdMonstro)
            .SelectMany(m => m.Ataques.Select(a => (Monstro: m, Ataque: a)))
            .Where(x => !AtaquesCatalogoAdicionados.Contains(x.Ataque.IdMonstroAtaque))
            .Where(x =>
                string.IsNullOrWhiteSpace(BuscaAtaqueCatalogo) ||
                x.Ataque.Nome.Contains(BuscaAtaqueCatalogo, StringComparison.OrdinalIgnoreCase) ||
                x.Monstro.Nome.Contains(BuscaAtaqueCatalogo, StringComparison.OrdinalIgnoreCase) ||
                (x.Ataque.Dano?.Contains(BuscaAtaqueCatalogo, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Ataque.TipoDano?.Contains(BuscaAtaqueCatalogo, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Ataque.Alcance?.Contains(BuscaAtaqueCatalogo, StringComparison.OrdinalIgnoreCase) ?? false));

        public bool ModoEdicao => MonstroAtual.IdMonstro > 0;
        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        // Controle de saída (NavigationLock)
        public bool ExibirAvisoSaida { get; private set; }
        public bool LiberarProximaNavegacao { get; private set; }
        public string? DestinoPendente { get; private set; }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public void LimparFormulario()
        {
            MonstroAtual = new Monstro
            {
                Tamanho = "Médio",
                Nd = 1,
                ClasseArmadura = 13,
                PvMaximo = 24,
                TipoMonstro = "Outro",
            };
            Ataques = new();
            NovoAtaque = new();
            AtaqueEmVisualizacao = null;
            ExibirFormularioNovoAtaque = false;
            ErroNovoAtaque = string.Empty;
            AtaquesCatalogoAdicionados.Clear();
            _origemCatalogo.Clear();
            BuscaAtaqueCatalogo = string.Empty;
            MensagemFeedback = "";
            Sucesso = false;
            IsProcessing = false;
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = false;
            DestinoPendente = null;
        }

        public bool FormularioIntocado =>
            string.IsNullOrWhiteSpace(MonstroAtual.Nome) &&
            MonstroAtual.PvMaximo == 24 &&
            Ataques.Count == 0;

        public async Task SalvarMonstroAsync()
        {
            if (IsProcessing) return;

            if (string.IsNullOrWhiteSpace(MonstroAtual.Nome))
            {
                Sucesso = false;
                MensagemFeedback = "O nome da criatura é obrigatório!";
                NotifyStateChanged();
                return;
            }

            int idUsuario = _sessaoUsuario.IdUsuarioLogado;
            if (idUsuario <= 0)
            {
                Sucesso = false;
                MensagemFeedback = "Usuário não identificado. Faça login novamente.";
                NotifyStateChanged();
                return;
            }

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Forjando monstro...";
                NotifyStateChanged();

                MonstroAtual.Ataques = Ataques.Where(a => !string.IsNullOrWhiteSpace(a.Nome)).ToList();
                var resultado = ModoEdicao
                    ? await _monstroService.AtualizarMonstroAsync(idUsuario, MonstroAtual)
                    : await _monstroService.CriarMonstroAsync(idUsuario, MonstroAtual);

                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso)
                {
                    // Write-through: grava na Biblioteca em memória antes de redirecionar
                    if (ModoEdicao)
                        _appCache.UpsertMonstro(idUsuario, MonstroAtual);
                    else
                        _appCache.InvalidarBiblioteca();

                    LiberarProximaNavegacao = true;
                    NotifyStateChanged();

                    await Task.Delay(500);
                    _navigation.NavigateTo("/BibliotecaGeral?categoria=monstros");
                }
                else
                {
                    NotifyStateChanged();
                }
            }
            catch (Exception ex)
            {
                Sucesso = false;
                MensagemFeedback = $"Erro ao salvar: {ex.Message}";
                NotifyStateChanged();
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        public void AlternarFormularioNovoAtaque()
        {
            if (ExibirFormularioNovoAtaque)
            {
                CancelarNovoAtaque();
                return;
            }

            AbrirFormularioNovoAtaque();
        }

        public void AbrirFormularioNovoAtaque()
        {
            NovoAtaque = new MonstroAtaque();
            ErroNovoAtaque = string.Empty;
            ExibirFormularioNovoAtaque = true;
            NotifyStateChanged();
        }

        public void CancelarNovoAtaque()
        {
            NovoAtaque = new MonstroAtaque();
            ExibirFormularioNovoAtaque = false;
            ErroNovoAtaque = string.Empty;
            NotifyStateChanged();
        }

        public void VincularNovoAtaque()
        {
            if (string.IsNullOrWhiteSpace(NovoAtaque.Nome))
            {
                ErroNovoAtaque = "Informe o nome do ataque antes de vinculá-lo.";
                NotifyStateChanged();
                return;
            }

            NovoAtaque.Nome = NovoAtaque.Nome.Trim();
            NovoAtaque.Dano = NovoAtaque.Dano?.Trim();
            NovoAtaque.TipoDano = NovoAtaque.TipoDano?.Trim();
            NovoAtaque.Alcance = NovoAtaque.Alcance?.Trim();
            NovoAtaque.Descricao = NovoAtaque.Descricao?.Trim();

            if (ModoEdicao)
                NovoAtaque.IdMonstro = MonstroAtual.IdMonstro;

            Ataques.Add(NovoAtaque);
            MonstroAtual.Ataques = Ataques.ToList();
            AtaqueEmVisualizacao = NovoAtaque;
            NovoAtaque = new MonstroAtaque();
            ExibirFormularioNovoAtaque = false;
            ErroNovoAtaque = string.Empty;
            NotifyStateChanged();
        }

        public void RemoverAtaque(MonstroAtaque ataque)
        {
            Ataques.Remove(ataque);
            MonstroAtual.Ataques = Ataques.ToList();

            if (_origemCatalogo.Remove(ataque, out int idCatalogo))
                AtaquesCatalogoAdicionados.Remove(idCatalogo);

            if (AtaqueEmVisualizacao == ataque)
                AtaqueEmVisualizacao = Ataques.FirstOrDefault();

            NotifyStateChanged();
        }

        public void SelecionarAtaque(MonstroAtaque? ataque)
        {
            AtaqueEmVisualizacao = ataque;
            NotifyStateChanged();
        }

        public void VisualizarAtaqueCatalogo(MonstroAtaque ataque)
        {
            AtaqueEmVisualizacao = ataque;
            NotifyStateChanged();
        }

        public async Task CarregarCatalogoAtaquesAsync()
        {
            int idUsuario = _sessaoUsuario.IdUsuarioLogado;
            if (idUsuario > 0)
                CatalogoMonstrosAtaques = await _monstroService.ObterMonstrosPorUsuarioAsync(idUsuario);

            NotifyStateChanged();
        }

        public void VincularAtaqueDoCatalogo(Monstro monstro, MonstroAtaque ataque)
        {
            var novoAtaque = new MonstroAtaque
            {
                Nome = ataque.Nome,
                BonusAtaque = ataque.BonusAtaque,
                Dano = ataque.Dano,
                TipoDano = ataque.TipoDano,
                Alcance = ataque.Alcance,
                Descricao = ataque.Descricao
            };

            if (ModoEdicao)
                novoAtaque.IdMonstro = MonstroAtual.IdMonstro;

            Ataques.Add(novoAtaque);
            MonstroAtual.Ataques = Ataques.ToList();
            AtaqueEmVisualizacao = novoAtaque;
            AtaquesCatalogoAdicionados.Add(ataque.IdMonstroAtaque);
            _origemCatalogo[novoAtaque] = ataque.IdMonstroAtaque;
            NotifyStateChanged();
        }

        public async Task CarregarMonstroAsync(int idMonstro)
        {
            var monstro = await _monstroService.ObterPorIdAsync(idMonstro);
            if (monstro is null || monstro.IdAccount != _sessaoUsuario.IdUsuarioLogado)
            {
                MensagemFeedback = "Monstro não encontrado ou sem permissão para editá-lo.";
                Sucesso = false;
                NotifyStateChanged();
                return;
            }

            MonstroAtual = monstro;
            Ataques = monstro.Ataques.Select(a => new MonstroAtaque
            {
                Nome = a.Nome,
                BonusAtaque = a.BonusAtaque,
                Dano = a.Dano,
                TipoDano = a.TipoDano,
                Alcance = a.Alcance,
                Descricao = a.Descricao
            }).ToList();

            AtaqueEmVisualizacao = Ataques.FirstOrDefault();
            NovoAtaque = new MonstroAtaque();
            ExibirFormularioNovoAtaque = false;
            ErroNovoAtaque = string.Empty;
            AtaquesCatalogoAdicionados.Clear();
            _origemCatalogo.Clear();
            BuscaAtaqueCatalogo = string.Empty;
            NotifyStateChanged();
        }

        public void CancelarCriacao()
        {
            LiberarProximaNavegacao = true;
            _navigation.NavigateTo("/BibliotecaGeral?categoria=monstros");
        }

        public void AbrirAvisoSaida()
        {
            if (FormularioIntocado || Sucesso)
            {
                LiberarProximaNavegacao = true;
                _navigation.NavigateTo("/BibliotecaGeral?categoria=monstros");
                return;
            }

            ExibirAvisoSaida = true;
            NotifyStateChanged();
        }

        public void ConfirmarNavegacao(LocationChangingContext contexto)
        {
            if (Sucesso || LiberarProximaNavegacao || FormularioIntocado)
            {
                LiberarProximaNavegacao = false;
                return;
            }

            contexto.PreventNavigation();
            DestinoPendente = contexto.TargetLocation;
            ExibirAvisoSaida = true;
            NotifyStateChanged();
        }

        public void FecharAvisoSaida()
        {
            ExibirAvisoSaida = false;
            DestinoPendente = null;
            NotifyStateChanged();
        }

        public void SairDaCriacao()
        {
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = true;
            _navigation.NavigateTo(DestinoPendente ?? "/BibliotecaGeral?categoria=monstros");
        }
    }
}