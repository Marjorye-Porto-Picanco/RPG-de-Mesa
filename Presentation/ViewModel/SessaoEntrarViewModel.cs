using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class ClasseCampanhaEntry
    {
        public string NomeClasse { get; set; } = "Guerreiro";
        public int Nivel { get; set; } = 1;
    }

    public class MagiaCampanhaEntry
    {
        public string Nome { get; set; } = string.Empty;
        public string Circulo { get; set; } = "Truque";
        public string Efeito { get; set; } = string.Empty;
        public string AlcanceNotas { get; set; } = string.Empty;
    }

    public class SessaoEntrarViewModel
    {
        private readonly CampanhaService _campanhaService;
        private readonly PersonagemService _personagemService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;

        public SessaoEntrarViewModel(
            CampanhaService campanhaService,
            PersonagemService personagemService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation)
        {
            _campanhaService = campanhaService;
            _personagemService = personagemService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
        }

        public int EtapaAtual { get; set; } = 1;
        public string Mensagem { get; set; } = string.Empty;
        public bool TemErro { get; set; }
        public bool Carregando { get; set; }
        public bool ModoAdicionarNovaFicha { get; set; }
        public bool ModoReenviarRecusada { get; set; }

        public Campanha? CampanhaAtual { get; private set; }
        public List<Personagem> PersonagensDisponiveis { get; private set; } = new();
        public List<PersonagemCampanha> VinculosDoJogador { get; private set; } = new();

        public List<Item> ItensDaCampanha =>
            CampanhaAtual?.Itens.OrderBy(i => i.Nome).ToList() ?? new List<Item>();

        public List<PersonagemCampanha> FichasAceitasDoJogador =>
            VinculosDoJogador
                .Where(v => string.IsNullOrEmpty(v.StatusAprovacao) ||
                            v.StatusAprovacao.Equals("Aceita", StringComparison.OrdinalIgnoreCase))
                .ToList();

        public bool PossuiAlgumaFichaAceita => FichasAceitasDoJogador.Any();

        public List<Personagem> PersonagensNaoEnviados =>
            PersonagensDisponiveis
                .Where(p => !VinculosDoJogador.Any(v =>
                    v.IdPersonagem == p.IdPersonagem ||
                    (v.Personagem != null && v.Personagem.IdPersonagemOrigem == p.IdPersonagem)))
                .ToList();

        public PersonagemCampanha? VinculoSelecionado { get; private set; }
        public Personagem? FichaCompletaVinculada { get; private set; }
        public int NovoPersonagemParaEnviarId { get; set; }
        public Personagem? PersonagemEmPreparacao { get; private set; }
        public string StatusFichaAtual => VinculoSelecionado?.StatusAprovacao ?? string.Empty;
        public string AbaFichaPreview { get; set; } = "atributos";
        public string AbaEdicaoRecusada { get; set; } = "atributos";

        public IReadOnlyList<string> ListaOpcoesClasses { get; } =
        [
            "Bárbaro", "Bardo", "Bruxo", "Clérigo", "Druida", "Feiticeiro",
            "Guerreiro", "Ladino", "Mago", "Monge", "Paladino", "Patrulheiro", "Outra"
        ];

        public IReadOnlyList<string> ListaSalvaguardas { get; } =
        [
            "Força", "Destreza", "Constituição", "Inteligência", "Sabedoria", "Carisma"
        ];

        public IReadOnlyList<string> ListaPericias { get; } =
        [
            "Acrobacia", "Adestrar Animais", "Arcanismo", "Atletismo", "Atuação", "Enganação",
            "Furtividade", "História", "Intimidação", "Intuição", "Investigação", "Medicina",
            "Natureza", "Percepção", "Persuasão", "Prestidigitação", "Religião", "Sobrevivência"
        ];

        public List<ClasseCampanhaEntry> ClassesEdicao { get; set; } = new();
        public HashSet<string> SalvaguardasEdicao { get; set; } = new();
        public HashSet<string> PericiasEdicao { get; set; } = new();
        public List<MagiaCampanhaEntry> MagiasEdicao { get; set; } = new();

        public List<ItemInventarioVinculado> ItensInventarioEdicao { get; set; } = new();
        public int MoedasPC { get; set; } = 0;
        public int MoedasPP { get; set; } = 0;
        public int MoedasPE { get; set; } = 0;
        public int MoedasPO { get; set; } = 10;
        public int MoedasPL { get; set; } = 0;
        public string NotasInventario { get; set; } = string.Empty;

        public decimal PesoTotalEdicao =>
            ItensInventarioEdicao.Where(i => i.IdItem > 0).Sum(i => i.PesoUnitario * Math.Max(1, i.Quantidade));

        public decimal CapacidadeCargaMaximaEdicao =>
            Math.Round(Math.Max(1, PersonagemEmPreparacao?.Forca ?? 10) * 7.5m, 1);

        public event Action? OnChange;
        public void NotifyStateChanged() => OnChange?.Invoke();

        public async Task InicializarAsync(int idCampanhaParam, int? campanhaIdQuery)
        {
            if (!_sessaoUsuario.EstaLogado)
            {
                _navigation.NavigateTo("/login");
                return;
            }

            await CarregarContextoJogadorAsync(idCampanhaParam, campanhaIdQuery);
        }

        public async Task CarregarContextoJogadorAsync(int idCampanhaParam, int? campanhaIdQuery)
        {
            Carregando = true;
            LimparMensagem();
            NotifyStateChanged();
            try
            {
                int idUsuario = _sessaoUsuario.IdUsuarioLogado;
                PersonagensDisponiveis = await _personagemService.ObterPersonagensPorUsuarioAsync(idUsuario);

                int idAlvo = idCampanhaParam > 0 ? idCampanhaParam : (campanhaIdQuery ?? 0);
                if (idAlvo <= 0)
                {
                    var campanhasDoJogador = await _campanhaService.ObterCampanhasComoJogadorAsync(idUsuario);
                    if (campanhasDoJogador.Any())
                        idAlvo = campanhasDoJogador[0].IdCampanha;
                }

                if (idAlvo > 0)
                {
                    CampanhaAtual = await _campanhaService.ObterDetalhesCampanhaAsync(idAlvo);
                    await SincronizarVinculosDoJogadorAsync();
                }
            }
            catch (Exception ex)
            {
                DefinirErro($"Erro ao carregar dados da sessão: {ex.Message}");
            }
            finally
            {
                Carregando = false;
                NotifyStateChanged();
            }
        }

        public async Task SincronizarVinculosDoJogadorAsync(int? idPersonagemPreferencial = null)
        {
            VinculosDoJogador.Clear();
            VinculoSelecionado = null;
            FichaCompletaVinculada = null;

            if (CampanhaAtual is null) return;

            int idUsuario = _sessaoUsuario.IdUsuarioLogado;
            VinculosDoJogador = CampanhaAtual.PersonagemCampanhas
                .Where(pc => pc.Personagem != null && pc.Personagem.IdAccount == idUsuario)
                .OrderByDescending(pc => pc.StatusAprovacao == "Aceita")
                .ThenByDescending(pc => pc.StatusAprovacao == "Pendente")
                .ToList();

            if (VinculosDoJogador.Any())
            {
                var escolhido = (idPersonagemPreferencial.HasValue
                    ? VinculosDoJogador.FirstOrDefault(v => v.IdPersonagem == idPersonagemPreferencial.Value)
                    : null)
                    ?? FichasAceitasDoJogador.FirstOrDefault()
                    ?? VinculosDoJogador.First();

                await CarregarVinculoSelecionadoInternoAsync(escolhido);
            }
            else if (PersonagensNaoEnviados.Count == 1)
            {
                await AoEscolherPersonagemParaEnviarAsync(PersonagensNaoEnviados[0].IdPersonagem);
            }
        }

        public async Task SelecionarVinculoExistenteAsync(PersonagemCampanha vinculo)
        {
            if (Carregando) return;
            Carregando = true;
            NotifyStateChanged();
            try
            {
                await CarregarVinculoSelecionadoInternoAsync(vinculo);
            }
            finally
            {
                Carregando = false;
                NotifyStateChanged();
            }
        }

        private async Task CarregarVinculoSelecionadoInternoAsync(PersonagemCampanha vinculo)
        {
            VinculoSelecionado = vinculo;
            FichaCompletaVinculada = await _personagemService.ObterPorIdAsync(vinculo.IdPersonagem) ?? vinculo.Personagem;
        }

        public async Task AoEscolherPersonagemParaEnviarAsync(int idPersonagem)
        {
            NovoPersonagemParaEnviarId = idPersonagem;
            PersonagemEmPreparacao = await _personagemService.ObterPorIdAsync(idPersonagem)
                                   ?? PersonagensDisponiveis.FirstOrDefault(p => p.IdPersonagem == idPersonagem);
            PreencherEditorCompletoComDadosDoPersonagem(PersonagemEmPreparacao);
            NotifyStateChanged();
        }

        private void PreencherEditorCompletoComDadosDoPersonagem(Personagem? p)
        {
            ClassesEdicao.Clear();
            SalvaguardasEdicao.Clear();
            PericiasEdicao.Clear();
            MagiasEdicao.Clear();
            ItensInventarioEdicao.Clear();

            MoedasPC = 0;
            MoedasPP = 0;
            MoedasPE = 0;
            MoedasPO = 10;
            MoedasPL = 0;
            NotasInventario = string.Empty;

            if (p == null) return;

            if (p.PersonagemClasses != null && p.PersonagemClasses.Any())
            {
                foreach (var pc in p.PersonagemClasses)
                {
                    if (pc.Classe != null)
                    {
                        ClassesEdicao.Add(new ClasseCampanhaEntry
                        {
                            NomeClasse = pc.Classe.Nome,
                            Nivel = pc.NivelNaClasse
                        });
                    }
                }
            }

            if (!ClassesEdicao.Any())
            {
                ClassesEdicao.Add(new ClasseCampanhaEntry
                {
                    NomeClasse = string.IsNullOrWhiteSpace(p.ClasseTexto) ? "Guerreiro" : p.ClasseTexto.Split('/')[0].Trim(),
                    Nivel = Math.Max(1, p.Nivel)
                });
            }

            foreach (var s in p.Salvaguardas)
                SalvaguardasEdicao.Add(s.Atributo);

            foreach (var per in p.Pericias)
                PericiasEdicao.Add(per.NomePericia);

            foreach (var pm in p.PersonagemMagias)
            {
                if (pm.Magia != null)
                {
                    MagiasEdicao.Add(new MagiaCampanhaEntry
                    {
                        Nome = pm.Magia.Nome,
                        Circulo = pm.Magia.CirculoNivel == 0 ? "Truque" : $"{pm.Magia.CirculoNivel}º Círculo",
                        Efeito = pm.Magia.DanoEfeito ?? string.Empty,
                        AlcanceNotas = pm.Magia.Alcance ?? string.Empty
                    });
                }
            }
            if (!MagiasEdicao.Any())
                MagiasEdicao.Add(new MagiaCampanhaEntry());

            if (p.Inventario != null)
            {
                MoedasPC = p.Inventario.MoedasPC;
                MoedasPP = p.Inventario.MoedasPP;
                MoedasPE = p.Inventario.MoedasPE;
                MoedasPO = p.Inventario.MoedasPO;
                MoedasPL = p.Inventario.MoedasPL;
                NotasInventario = p.Inventario.Notas ?? string.Empty;

                var idsPermitidosNaCampanha = ItensDaCampanha.Select(i => i.IdItem).ToHashSet();

                foreach (var ii in p.Inventario.Itens)
                {
                    if (!idsPermitidosNaCampanha.Contains(ii.IdItem)) continue;

                    var itemRef = ItensDaCampanha.FirstOrDefault(x => x.IdItem == ii.IdItem) ?? ii.Item;
                    if (itemRef != null)
                    {
                        ItensInventarioEdicao.Add(new ItemInventarioVinculado
                        {
                            Categoria = PersonagemCriarViewModel.ClassificarCategoriaItem(itemRef),
                            IdItem = itemRef.IdItem,
                            Nome = itemRef.Nome,
                            Quantidade = ii.Quantidade,
                            PesoUnitario = itemRef.Peso ?? 0m,
                            PrecoUnitario = itemRef.Preco ?? 0m
                        });
                    }
                }
            }
        }

        public async Task AbrirModoEnviarNovaFichaAsync()
        {
            LimparMensagem();
            ModoReenviarRecusada = false;
            ModoAdicionarNovaFicha = true;
            int idInicial = PersonagensNaoEnviados.FirstOrDefault()?.IdPersonagem ?? 0;
            if (idInicial > 0)
                await AoEscolherPersonagemParaEnviarAsync(idInicial);
            else
                PersonagemEmPreparacao = null;
            NotifyStateChanged();
        }

        public async Task AbrirReenvioDeFichaRecusadaAsync()
        {
            if (FichaCompletaVinculada == null || StatusFichaAtual != "Recusada") return;
            LimparMensagem();
            ModoAdicionarNovaFicha = false;
            ModoReenviarRecusada = true;
            AbaEdicaoRecusada = "atributos";
            NovoPersonagemParaEnviarId = FichaCompletaVinculada.IdPersonagem;
            PersonagemEmPreparacao = await _personagemService.ObterPorIdAsync(FichaCompletaVinculada.IdPersonagem) ?? FichaCompletaVinculada;
            PreencherEditorCompletoComDadosDoPersonagem(PersonagemEmPreparacao);
            NotifyStateChanged();
        }

        public void CancelarModoEdicaoOuAdicao()
        {
            ModoAdicionarNovaFicha = false;
            ModoReenviarRecusada = false;
            PersonagemEmPreparacao = null;
            LimparMensagem();
            NotifyStateChanged();
        }

        public void AlternarSalvaguardaEdicao(string atributo, bool marcado)
        {
            if (marcado) SalvaguardasEdicao.Add(atributo);
            else SalvaguardasEdicao.Remove(atributo);
            NotifyStateChanged();
        }

        public void AlternarPericiaEdicao(string pericia, bool marcado)
        {
            if (marcado) PericiasEdicao.Add(pericia);
            else PericiasEdicao.Remove(pericia);
            NotifyStateChanged();
        }

        public void AdicionarClasseEdicao()
        {
            ClassesEdicao.Add(new ClasseCampanhaEntry { NomeClasse = "Guerreiro", Nivel = 1 });
            SincronizarClassesENivelEdicao();
            NotifyStateChanged();
        }

        public void RemoverClasseEdicao(ClasseCampanhaEntry item)
        {
            if (ClassesEdicao.Count > 1)
            {
                ClassesEdicao.Remove(item);
                SincronizarClassesENivelEdicao();
                NotifyStateChanged();
            }
        }

        public void AlterarNomeClasseEdicao(ClasseCampanhaEntry cl, ChangeEventArgs e)
        {
            cl.NomeClasse = e.Value?.ToString() ?? "Guerreiro";
            SincronizarClassesENivelEdicao();
            NotifyStateChanged();
        }

        public void AlterarNivelClasseEdicao(ClasseCampanhaEntry cl, ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int nv))
            {
                cl.Nivel = Math.Clamp(nv, 1, 20);
                SincronizarClassesENivelEdicao();
                NotifyStateChanged();
            }
        }

        private void SincronizarClassesENivelEdicao()
        {
            if (PersonagemEmPreparacao == null) return;
            var validas = ClassesEdicao.Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse)).ToList();
            if (!validas.Any()) return;

            PersonagemEmPreparacao.Nivel = Math.Clamp(validas.Sum(c => Math.Max(1, c.Nivel)), 1, 20);
            PersonagemEmPreparacao.ClasseTexto = validas.Count == 1
                ? validas[0].NomeClasse
                : string.Join(" / ", validas.Select(c => $"{c.NomeClasse} {c.Nivel}"));
        }

        public void AdicionarMagiaEdicao() { MagiasEdicao.Add(new MagiaCampanhaEntry()); NotifyStateChanged(); }
        public void RemoverMagiaEdicao(MagiaCampanhaEntry magia) { MagiasEdicao.Remove(magia); NotifyStateChanged(); }

        private static int ExtrairCirculoMagia(string circuloTexto)
        {
            if (string.IsNullOrWhiteSpace(circuloTexto) || circuloTexto.Contains("Truque", StringComparison.OrdinalIgnoreCase))
                return 0;
            var digito = new string(circuloTexto.Where(char.IsDigit).ToArray());
            return int.TryParse(digito, out int c) ? c : 1;
        }

        public IEnumerable<Item> ObterItensCampanhaPorCategoria(string categoria)
        {
            if (!ItensDaCampanha.Any()) return Enumerable.Empty<Item>();
            var filtrados = ItensDaCampanha.Where(i => PersonagemCriarViewModel.ClassificarCategoriaItem(i) == categoria);
            return filtrados.Any() ? filtrados : ItensDaCampanha;
        }

        public void AdicionarSlotItem(string categoria)
        {
            ItensInventarioEdicao.Add(new ItemInventarioVinculado
            {
                Categoria = categoria,
                Quantidade = 1
            });
            NotifyStateChanged();
        }

        public void AlterarItemSlot(ItemInventarioVinculado slot, ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int idItem))
            {
                slot.IdItem = idItem;
                var itemCampanha = ItensDaCampanha.FirstOrDefault(i => i.IdItem == idItem);
                if (itemCampanha != null)
                {
                    slot.Nome = itemCampanha.Nome;
                    slot.PesoUnitario = itemCampanha.Peso ?? 0m;
                    slot.PrecoUnitario = itemCampanha.Preco ?? 0m;
                }
                else
                {
                    slot.Nome = string.Empty;
                    slot.PesoUnitario = 0m;
                    slot.PrecoUnitario = 0m;
                }
                NotifyStateChanged();
            }
        }

        public void RemoverSlotItem(ItemInventarioVinculado slot)
        {
            ItensInventarioEdicao.Remove(slot);
            NotifyStateChanged();
        }

        public async Task EnviarFichaComInventarioAoMestreAsync()
        {
            if (CampanhaAtual is null || PersonagemEmPreparacao is null)
            {
                DefinirErro("Selecione um personagem para enviar ao Mestre.");
                return;
            }

            int idBaseOuInstancia = PersonagemEmPreparacao.IdPersonagem;

            var vinculoExistente = VinculosDoJogador.FirstOrDefault(v =>
                v.IdPersonagem == idBaseOuInstancia ||
                (v.Personagem != null && v.Personagem.IdPersonagemOrigem == idBaseOuInstancia));

            if (vinculoExistente != null && !string.Equals(vinculoExistente.StatusAprovacao, "Recusada", StringComparison.OrdinalIgnoreCase))
            {
                DefinirErro("Esta ficha já foi enviada ao Mestre nesta campanha e não pode mais ser editada por aqui.");
                return;
            }

            Carregando = true;
            LimparMensagem();
            NotifyStateChanged();
            try
            {
                var dadosInventario = new Inventario
                {
                    CapacidadeCarga = CapacidadeCargaMaximaEdicao,
                    PesoAtual = PesoTotalEdicao,
                    Dinheiro = MoedasPO,
                    MoedasPC = MoedasPC,
                    MoedasPP = MoedasPP,
                    MoedasPE = MoedasPE,
                    MoedasPO = MoedasPO,
                    MoedasPL = MoedasPL,
                    Notas = NotasInventario
                };

                var listaItens = ItensInventarioEdicao
                    .Where(i => i.IdItem > 0)
                    .Select(i => (i.IdItem, i.Quantidade))
                    .ToList();

                int idInstanciaCampanha;

                if (ModoReenviarRecusada && vinculoExistente != null)
                {
                    SincronizarClassesENivelEdicao();
                    int modCon = (int)Math.Floor((PersonagemEmPreparacao.Constituicao - 10) / 2.0);
                    PersonagemEmPreparacao.PvMaximo = Math.Max(1, (10 + modCon) + ((PersonagemEmPreparacao.Nivel - 1) * Math.Max(1, 6 + modCon)));
                    PersonagemEmPreparacao.PvAtual = PersonagemEmPreparacao.PvMaximo;

                    var resAtu = await _personagemService.AtualizarPersonagemAsync(PersonagemEmPreparacao);
                    if (!resAtu.Sucesso)
                    {
                        DefinirErro(resAtu.Mensagem);
                        return;
                    }

                    var classesParaSalvar = ClassesEdicao
                        .Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse))
                        .Select(c => (c.NomeClasse.Trim(), Math.Max(1, c.Nivel)))
                        .ToList();

                    var magiasParaSalvar = MagiasEdicao
                        .Where(m => !string.IsNullOrWhiteSpace(m.Nome))
                        .Select(m => new Magia
                        {
                            Nome = m.Nome.Trim(),
                            CirculoNivel = ExtrairCirculoMagia(m.Circulo),
                            DanoEfeito = m.Efeito,
                            Alcance = m.AlcanceNotas
                        })
                        .ToList();

                    dadosInventario.IdPersonagem = PersonagemEmPreparacao.IdPersonagem;

                    var res3Fn = await _personagemService.SalvarComplementos3FNAsync(
                        PersonagemEmPreparacao.IdPersonagem,
                        classesParaSalvar,
                        SalvaguardasEdicao,
                        PericiasEdicao,
                        dadosInventario,
                        listaItens,
                        magiasParaSalvar);

                    if (!res3Fn.Sucesso)
                    {
                        DefinirErro(res3Fn.Mensagem);
                        return;
                    }

                    idInstanciaCampanha = PersonagemEmPreparacao.IdPersonagem;
                }
                else
                {
                    var resultadoInstancia = await _personagemService.InstanciarPersonagemParaCampanhaAsync(
                        idBaseOuInstancia,
                        CampanhaAtual.IdCampanha,
                        dadosInventario,
                        listaItens);

                    if (!resultadoInstancia.Sucesso || resultadoInstancia.IdPersonagemInstancia <= 0)
                    {
                        DefinirErro(resultadoInstancia.Mensagem);
                        return;
                    }

                    idInstanciaCampanha = resultadoInstancia.IdPersonagemInstancia;
                }

                var resultado = await _campanhaService.EnviarFichaParaAvaliacaoAsync(CampanhaAtual.IdCampanha, idInstanciaCampanha);
                if (!resultado.Sucesso)
                {
                    DefinirErro(resultado.Mensagem);
                    return;
                }

                CampanhaAtual = await _campanhaService.ObterDetalhesCampanhaAsync(CampanhaAtual.IdCampanha);
                ModoAdicionarNovaFicha = false;
                ModoReenviarRecusada = false;
                PersonagemEmPreparacao = null;
                await SincronizarVinculosDoJogadorAsync(idInstanciaCampanha);
                AbaFichaPreview = "atributos";
                Mensagem = "Ficha e inventário atualizados e enviados para avaliação do Mestre!";
            }
            catch (Exception ex)
            {
                DefinirErro($"Erro ao enviar ficha e inventário: {ex.Message}");
            }
            finally
            {
                Carregando = false;
                NotifyStateChanged();
            }
        }

        public async Task RecarregarStatusAsync()
        {
            if (CampanhaAtual is null) return;
            Carregando = true;
            LimparMensagem();
            NotifyStateChanged();
            try
            {
                int? idAtual = VinculoSelecionado?.IdPersonagem;
                CampanhaAtual = await _campanhaService.ObterDetalhesCampanhaAsync(CampanhaAtual.IdCampanha);
                await SincronizarVinculosDoJogadorAsync(idAtual);
                Mensagem = StatusFichaAtual == "Aceita"
                    ? "Ficha aprovada! Você já pode conectar na sessão."
                    : "Status atualizado.";
            }
            finally
            {
                Carregando = false;
                NotifyStateChanged();
            }
        }

        public void ConectarNaSala()
        {
            if (StatusFichaAtual != "Aceita" && FichasAceitasDoJogador.Any())
            {
                VinculoSelecionado = FichasAceitasDoJogador.First();
                FichaCompletaVinculada = VinculoSelecionado.Personagem;
            }

            if (StatusFichaAtual != "Aceita")
            {
                DefinirErro("Selecione uma ficha aprovada pelo Mestre antes de conectar na sessão.");
                return;
            }

            _navigation.NavigateTo("/saladojogo");
        }

        public void IrParaEtapa(int etapa)
        {
            if (etapa == 2 && StatusFichaAtual != "Aceita" && FichasAceitasDoJogador.Any())
            {
                _ = SelecionarVinculoExistenteAsync(FichasAceitasDoJogador.First());
            }

            EtapaAtual = etapa;
            LimparMensagem();
            NotifyStateChanged();
        }

        public static string ObterModificador(int valor)
        {
            var modificador = (int)Math.Floor((valor - 10) / 2.0);
            return modificador >= 0 ? $"+{modificador}" : modificador.ToString();
        }

        public void VoltarParaHome() => _navigation.NavigateTo("/campanhas");
        private void LimparMensagem() { Mensagem = string.Empty; TemErro = false; }
        private void DefinirErro(string mensagem) { TemErro = true; Mensagem = mensagem; NotifyStateChanged(); }
    }
}