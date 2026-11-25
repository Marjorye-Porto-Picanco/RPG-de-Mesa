using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class PerfilViewModel
    {
        private static readonly TimeSpan ValidadeCache = TimeSpan.FromMinutes(2);

        private readonly UsuarioService _usuarioService;
        private readonly PersonagemService _personagemService;
        private readonly CampanhaService _campanhaService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly AppCacheService _cache;
        private readonly ImagemPickerService _imagemPicker;
        private readonly NavigationManager _navigation;

        public PerfilViewModel(
            UsuarioService usuarioService,
            PersonagemService personagemService,
            CampanhaService campanhaService,
            SessaoUsuarioService sessaoUsuario,
            AppCacheService cache,
            ImagemPickerService imagemPicker,
            NavigationManager navigation)
        {
            _usuarioService = usuarioService;
            _personagemService = personagemService;
            _campanhaService = campanhaService;
            _sessaoUsuario = sessaoUsuario;
            _cache = cache;
            _imagemPicker = imagemPicker;
            _navigation = navigation;
        }

        // Campos de dbo.Usuario e dbo.Perfil
        public string UsernameLogin { get; private set; } = string.Empty;
        public string NomeExibicaoInput { get; set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string StatusConta { get; private set; } = "ativo";
        public DateTime? DataCadastro { get; private set; }
        public string Biografia { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        // Métricas
        public int TotalPersonagens { get; private set; }
        public int TotalMonstros { get; private set; }

        // Estado da tela
        public bool Carregando { get; private set; }
        public bool CarregandoAvatar { get; private set; }
        public bool Salvando { get; private set; }
        public bool ExibirAvisoSaida { get; private set; }
        public string StatusUpload { get; private set; } = string.Empty;
        public string MensagemStatus { get; private set; } = string.Empty;
        public bool TemErro { get; private set; }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public string NomeExibicao =>
            !string.IsNullOrWhiteSpace(NomeExibicaoInput)
                ? NomeExibicaoInput.Trim()
                : (!string.IsNullOrWhiteSpace(UsernameLogin) ? UsernameLogin : "Mestre da mesa");

        public bool TemAvatar =>
            !string.IsNullOrWhiteSpace(AvatarUrl);

        public async Task CarregarPerfilAsync(bool forcarAtualizacao = false)
        {
            if (!_sessaoUsuario.EstaLogado)
            {
                _navigation.NavigateTo("/login");
                return;
            }

            int idUsuario = _sessaoUsuario.IdUsuarioLogado;
            TemErro = false;
            MensagemStatus = string.Empty;

            var snapshot = _cache.ObterPerfil(idUsuario);
            if (snapshot is not null)
            {
                AplicarSnapshot(snapshot);
                NotifyStateChanged();

                if (!forcarAtualizacao && !_cache.PerfilExpirado(idUsuario, ValidadeCache))
                    return;
            }

            Carregando = snapshot is null;
            if (Carregando) NotifyStateChanged();

            try
            {
                var persSnap = !_appCachePersonagensDisponivel(idUsuario, forcarAtualizacao, out var persCache);
                var bibSnap = !_appCacheBibliotecaDisponivel(idUsuario, forcarAtualizacao, out var bibCache);

                var tarefaUsuario = _usuarioService.ObterPorIdAsync(idUsuario);
                var tarefaPersonagens = persCache is not null
                    ? Task.FromResult(persCache.Personagens.Count)
                    : ObterTotalPersonagensAsync(idUsuario);
                var tarefaMonstros = bibCache is not null
                    ? Task.FromResult(bibCache.Monstros.Count(m => m.IdAccount == idUsuario))
                    : ObterTotalMonstrosAsync(idUsuario);

                await Task.WhenAll(tarefaUsuario, tarefaPersonagens, tarefaMonstros);

                var usuario = tarefaUsuario.Result;
                if (usuario is not null)
                {
                    UsernameLogin = usuario.UsarName;
                    NomeExibicaoInput = usuario.NomeParaExibicao;
                    Email = usuario.EMail;
                    StatusConta = usuario.StatusConta;
                    DataCadastro = usuario.DataCadastro;
                    Biografia = usuario.Perfil?.Biografia ?? string.Empty;
                    AvatarUrl = usuario.Perfil?.AvatarUrl;

                    _sessaoUsuario.AtualizarNomeUsuario(NomeExibicao);
                    _sessaoUsuario.AtualizarAvatar(AvatarUrl);
                }
                else
                {
                    NomeExibicaoInput = _sessaoUsuario.NomeUsuario ?? "Mestre da mesa";
                }

                TotalPersonagens = tarefaPersonagens.Result;
                TotalMonstros = tarefaMonstros.Result;

                SalvarSnapshotNoCache(idUsuario);
            }
            catch (Exception ex)
            {
                TemErro = true;
                MensagemStatus = $"Não foi possível carregar o perfil: {ex.Message}";
            }
            finally
            {
                Carregando = false;
                NotifyStateChanged();
            }
        }

        private bool _appCachePersonagensDisponivel(int idUsuario, bool forcarAtualizacao, out PersonagensLibCacheSnapshot? snap)
        {
            snap = !forcarAtualizacao && !_cache.PersonagensLibExpirados(idUsuario, ValidadeCache)
                ? _cache.ObterPersonagensLib(idUsuario)
                : null;
            return snap is not null;
        }

        private bool _appCacheBibliotecaDisponivel(int idUsuario, bool forcarAtualizacao, out BibliotecaCacheSnapshot? snap)
        {
            snap = !forcarAtualizacao && !_cache.BibliotecaExpirada(idUsuario, ValidadeCache)
                ? _cache.ObterBiblioteca(idUsuario)
                : null;
            return snap is not null;
        }

        private async Task<int> ObterTotalPersonagensAsync(int idUsuario)
        {
            var personagens = await _personagemService.ObterPersonagensPorUsuarioAsync(idUsuario);
            return personagens.Count;
        }

        private async Task<int> ObterTotalMonstrosAsync(int idUsuario)
        {
            var monstros = await _campanhaService.ObterMonstrosParaSessaoAsync(idUsuario);
            return monstros.Count(m => m.IdAccount == idUsuario);
        }

        public async Task SelecionarAvatarAsync()
        {
            if (CarregandoAvatar) return;
            CarregandoAvatar = true;
            StatusUpload = "Carregando foto...";
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarUmaAsync(ladoMaximo: 512);
                if (res.Cancelado)
                {
                    StatusUpload = string.Empty;
                    return;
                }

                if (res.Erro is not null)
                {
                    StatusUpload = res.Erro;
                    return;
                }

                AvatarUrl = res.Imagem!.DataUrl;
                StatusUpload = "Foto carregada. Clique em 'Salvar perfil' para confirmar.";
            }
            finally
            {
                CarregandoAvatar = false;
                NotifyStateChanged();
            }
        }

        public void RemoverAvatar()
        {
            AvatarUrl = null;
            StatusUpload = "Foto removida. Clique em 'Salvar perfil' para confirmar.";
            NotifyStateChanged();
        }

        public async Task SalvarPerfilAsync()
        {
            if (!_sessaoUsuario.EstaLogado || Salvando) return;

            Salvando = true;
            TemErro = false;
            MensagemStatus = "Salvando alterações...";
            NotifyStateChanged();

            try
            {
                int idUsuario = _sessaoUsuario.IdUsuarioLogado;
                string nomeLimpo = NomeExibicaoInput.Trim();

                var resultado = await _usuarioService.AtualizarPerfilAsync(
                    idUsuario,
                    nomeLimpo,
                    Biografia,
                    AvatarUrl);

                TemErro = !resultado.Sucesso;
                MensagemStatus = resultado.Mensagem;

                if (resultado.Sucesso)
                {
                    NomeExibicaoInput = nomeLimpo;
                    StatusUpload = string.Empty;
                    _sessaoUsuario.AtualizarNomeUsuario(NomeExibicao);
                    _sessaoUsuario.AtualizarAvatar(AvatarUrl);
                    SalvarSnapshotNoCache(idUsuario);
                    _cache.AtualizarPerfilBasico(idUsuario, nomeLimpo, Biografia, AvatarUrl);
                }
            }
            catch (Exception ex)
            {
                TemErro = true;
                MensagemStatus = $"Erro ao salvar perfil: {ex.Message}";
            }
            finally
            {
                Salvando = false;
                NotifyStateChanged();
            }
        }

        private void AplicarSnapshot(PerfilCacheSnapshot s)
        {
            UsernameLogin = s.NomeUsuario;
            NomeExibicaoInput = string.IsNullOrWhiteSpace(s.NomeExibicao) ? s.NomeUsuario : s.NomeExibicao;
            Email = s.Email;
            StatusConta = s.StatusConta;
            DataCadastro = s.DataCadastro;
            Biografia = s.Biografia;
            AvatarUrl = s.AvatarUrl;
            TotalPersonagens = s.TotalPersonagens;
            TotalMonstros = s.TotalMonstros;
        }

        private void SalvarSnapshotNoCache(int idUsuario)
        {
            _cache.SalvarPerfil(new PerfilCacheSnapshot
            {
                IdUsuario = idUsuario,
                AtualizadoEmUtc = DateTime.UtcNow,
                NomeUsuario = UsernameLogin,
                NomeExibicao = NomeExibicaoInput,
                Email = Email,
                StatusConta = StatusConta,
                DataCadastro = DataCadastro,
                Biografia = Biografia,
                AvatarUrl = AvatarUrl,
                TotalPersonagens = TotalPersonagens,
                TotalMonstros = TotalMonstros
            });
        }

        public void AbrirAvisoSaida() { ExibirAvisoSaida = true; NotifyStateChanged(); }
        public void FecharAvisoSaida() { ExibirAvisoSaida = false; NotifyStateChanged(); }
        public void ConfirmarSaida()
        {
            ExibirAvisoSaida = false;
            _navigation.NavigateTo("/home");
        }
    }
}
