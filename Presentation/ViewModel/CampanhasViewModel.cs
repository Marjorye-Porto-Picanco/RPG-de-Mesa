using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel;

public sealed record RemocaoCampanhaPendente(int IdCampanha, string Nome, bool ComoJogador);

public class CampanhasViewModel
{
    private static readonly TimeSpan ValidadeCache = TimeSpan.FromSeconds(45);

    private readonly CampanhaService _campanhaService;
    private readonly SessaoUsuarioService _sessaoUsuario;
    private readonly AppCacheService _cache;
    private readonly HomeCacheService _homeCache;

    public CampanhasViewModel(
        CampanhaService campanhaService,
        SessaoUsuarioService sessaoUsuario,
        AppCacheService cache,
        HomeCacheService homeCache)
    {
        _campanhaService = campanhaService;
        _sessaoUsuario = sessaoUsuario;
        _cache = cache;
        _homeCache = homeCache;
    }

    public List<Campanha> CampanhasMestre { get; private set; } = new();
    public List<Campanha> CampanhasJogador { get; private set; } = new();

    public string AbaAtiva { get; private set; } = "mestre";
    public bool ExibirModalEntrar { get; private set; }

    // Estado de Remoção / Saída
    public RemocaoCampanhaPendente? CampanhaPendente { get; private set; }
    public bool ProcessandoRemocao { get; private set; }
    public string? ErroRemocaoCampanha { get; private set; }

    public string TituloConfirmacaoCampanha => CampanhaPendente?.ComoJogador == true
        ? "Sair da campanha?"
        : "Excluir campanha?";

    public string MensagemConfirmacaoCampanha => CampanhaPendente?.ComoJogador == true
        ? $"Sua participação e seus personagens vinculados serão removidos de {CampanhaPendente.Nome}."
        : $"A campanha {CampanhaPendente?.Nome} e seus vínculos serão excluídos. Esta ação não pode ser desfeita.";

    public void SelecionarAba(string aba) => AbaAtiva = aba;

    public void AbrirModalEntrar() => ExibirModalEntrar = true;
    public void FecharModalEntrar() => ExibirModalEntrar = false;

    public Task CarregarCampanhasAsync() => CarregarCampanhasAsync(false);

    public async Task CarregarCampanhasAsync(bool forcarAtualizacao)
    {
        int idUsuario = _sessaoUsuario.IdUsuarioLogado;
        if (idUsuario <= 0) return;

        var snapshot = _cache.ObterCampanhas(idUsuario);
        if (snapshot is not null)
        {
            CampanhasMestre = snapshot.CampanhasMestre;
            CampanhasJogador = snapshot.CampanhasJogador;

            if (!forcarAtualizacao && !_cache.CampanhasExpiradas(idUsuario, ValidadeCache))
                return;
        }

        CampanhasMestre = await _campanhaService.ObterCampanhasPorUsuarioAsync(idUsuario);
        CampanhasJogador = await _campanhaService.ObterCampanhasComoJogadorAsync(idUsuario);

        _cache.SalvarCampanhas(new CampanhasCacheSnapshot
        {
            IdUsuario = idUsuario,
            AtualizadoEmUtc = DateTime.UtcNow,
            CampanhasMestre = CampanhasMestre,
            CampanhasJogador = CampanhasJogador
        });
    }

    public async Task CampanhaEntradaSucessoAsync()
    {
        ExibirModalEntrar = false;
        AbaAtiva = "jogador";
        _cache.InvalidarCampanhas();
        _homeCache.Limpar();
        await CarregarCampanhasAsync(forcarAtualizacao: true);
    }

    public void PedirRemocaoCampanha(Campanha campanha, bool comoJogador)
    {
        ErroRemocaoCampanha = null;
        CampanhaPendente = new RemocaoCampanhaPendente(campanha.IdCampanha, campanha.Nome, comoJogador);
    }

    public void CancelarRemocaoCampanha()
    {
        if (ProcessandoRemocao) return;
        CampanhaPendente = null;
        ErroRemocaoCampanha = null;
    }

    public async Task ConfirmarRemocaoCampanhaAsync()
    {
        if (CampanhaPendente is null || ProcessandoRemocao) return;

        ProcessandoRemocao = true;
        ErroRemocaoCampanha = null;
        try
        {
            var pendente = CampanhaPendente;
            var idUsuario = _sessaoUsuario.IdUsuarioLogado;

            var resultado = pendente.ComoJogador
                ? await _campanhaService.SairDaCampanhaAsync(idUsuario, pendente.IdCampanha)
                : await _campanhaService.ExcluirCampanhaAsync(idUsuario, pendente.IdCampanha);

            if (!resultado.Sucesso)
            {
                ErroRemocaoCampanha = resultado.Mensagem;
                return;
            }

            CampanhaPendente = null;
            _cache.InvalidarCampanhas();
            _homeCache.Limpar();
            await CarregarCampanhasAsync(forcarAtualizacao: true);
        }
        catch (Exception ex)
        {
            ErroRemocaoCampanha = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            ProcessandoRemocao = false;
        }
    }
}