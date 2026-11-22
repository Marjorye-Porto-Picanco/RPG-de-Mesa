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
        private static readonly TimeSpan ValidadeCache = TimeSpan.FromSeconds(45);

        private readonly UsuarioService _usuarioService;
        private readonly PersonagemService _personagemService;
        private readonly CampanhaService _campanhaService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly AppCacheService _cache;
        private readonly NavigationManager _navigation;
        private readonly ImagemPickerService _imagemPicker;

        public PerfilViewModel(
            UsuarioService usuarioService,
            PersonagemService personagemService,
            CampanhaService campanhaService,
            SessaoUsuarioService sessaoUsuario,
            AppCacheService cache,
            NavigationManager navigation,
            ImagemPickerService imagemPicker)
        {
            _usuarioService = usuarioService;
            _personagemService = personagemService;
            _campanhaService = campanhaService;
            _sessaoUsuario = sessaoUsuario;
            _cache = cache;
            _navigation = navigation;
            _imagemPicker = imagemPicker;
        }

        // Campos de dbo.Usuario e dbo.Perfil
        public string UsernameLogin { get; private set; } = string.Empty; // Usuario.usar_name (somente leitura)
        public string NomeExibicaoInput { get; set; } = string.Empty;     // Perfil.nome_exibicao (editável)
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
        public int ChaveInputAvatar { get; private set; }
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

        public string Iniciais
        {
            get
            {
                var partes = NomeExibicao.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 0) return "MM";
                if (partes.Length == 1) return partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant();
                return $"{char.ToUpperInvariant(partes[0][0])}{char.ToUpperInvariant(partes[1][0])}";
            }
        }

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
                var usuario = await _usuarioService.ObterPorIdAsync(idUsuario);
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

                var personagens = await _personagemService.ObterPersonagensPorUsuarioAsync(idUsuario);
                TotalPersonagens = personagens.Count;

                var monstros = await _campanhaService.ObterMonstrosParaSessaoAsync(idUsuario);
                TotalMonstros = monstros.Count(m => m.IdAccount == idUsuario);

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
            ChaveInputAvatar++;
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