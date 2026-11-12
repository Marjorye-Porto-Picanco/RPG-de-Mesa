using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    // =========================================================================
    // DBSETS: ENTIDADES PRINCIPAIS E CATÁLOGOS 3FN
    // =========================================================================
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Perfil> Perfis { get; set; }
    public DbSet<Campanha> Campanhas { get; set; }
    public DbSet<Personagem> Personagens { get; set; }
    public DbSet<Raca> Racas { get; set; }
    public DbSet<Antecedente> Antecedentes { get; set; }
    public DbSet<Classe> Classes { get; set; }
    public DbSet<NPC> Npcs { get; set; }
    public DbSet<Monstro> Monstros { get; set; }
    public DbSet<Item> Itens { get; set; }
    public DbSet<Magia> Magias { get; set; }
    public DbSet<Inventario> Inventarios { get; set; }

    // =========================================================================
    // DBSETS: RELAÇÕES 3FN DO PERSONAGEM
    // =========================================================================
    public DbSet<PersonagemClasse> PersonagemClasses { get; set; }
    public DbSet<PersonagemSalvaguarda> PersonagemSalvaguardas { get; set; }
    public DbSet<PersonagemPericia> PersonagemPericias { get; set; }
    public DbSet<PersonagemMagia> PersonagemMagias { get; set; }
    public DbSet<InventarioItem> InventarioItens { get; set; }

    // =========================================================================
    // DBSETS: TABELAS ASSOCIATIVAS DE CAMPANHA (N:N)
    // =========================================================================
    public DbSet<UsuarioCampanha> UsuariosCampanha { get; set; }
    public DbSet<PersonagemCampanha> PersonagensCampanha { get; set; }
    public DbSet<MonstroCampanha> MonstroCampanhas { get; set; }
    public DbSet<NpcCampanha> NpcCampanhas { get; set; }
    public DbSet<ItemCampanha> ItemCampanhas { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            string connectionString = @"Server=rpgsystem.database.windows.net,1433;
                        Initial Catalog=rpg_system;
                        Persist Security Info=False;
                        User ID=cleverson; 
                        Password=Seila1234@;
                        MultipleActiveResultSets=False;
                        Encrypt=True;
                        TrustServerCertificate=True;
                        Connection Timeout=30;";

            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================================================
        // 1. USUÁRIO E PERFIL
        // =====================================================================
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuario");
            e.HasKey(u => u.IdAccount);
            e.Property(u => u.IdAccount).HasColumnName("id_account");
            e.Property(u => u.UsarName).HasColumnName("usar_name");
            e.Property(u => u.EMail).HasColumnName("e_mail");
            e.Property(u => u.Password).HasColumnName("password");
            e.Property(u => u.DataCadastro).HasColumnName("data_cadastro");
            e.Property(u => u.StatusConta).HasColumnName("status_conta");

            e.HasOne(u => u.Perfil)
             .WithOne(p => p.Usuario)
             .HasForeignKey<Perfil>(p => p.IdAccount);

            e.HasMany(u => u.Personagens)
             .WithOne(p => p.Usuario)
             .HasForeignKey(p => p.IdAccount);
        });

        modelBuilder.Entity<Perfil>(e =>
        {
            e.ToTable("Perfil");
            e.HasKey(p => p.IdPerfil);
            e.Property(p => p.IdPerfil).HasColumnName("id_perfil");
            e.Property(p => p.IdAccount).HasColumnName("id_account");
            e.Property(p => p.Biografia).HasColumnName("biografia");
            e.Property(p => p.AvatarUrl).HasColumnName("avatar_url");
            e.Property(p => p.DataCriacao).HasColumnName("data_criacao");
        });

        // =====================================================================
        // 2. CAMPANHA
        // =====================================================================
        modelBuilder.Entity<Campanha>(e =>
        {
            e.ToTable("Campanha");
            e.HasKey(c => c.IdCampanha);
            e.Property(c => c.IdCampanha).HasColumnName("id_campanha");
            e.Property(c => c.IdAccount).HasColumnName("id_account");
            e.Property(c => c.Nome).HasColumnName("nome");
            e.Property(c => c.DataCriacao).HasColumnName("data_criacao");
            e.Property(c => c.Descricao).HasColumnName("descricao");
            e.Property(c => c.CodigoConvite).HasColumnName("codigo_convite").HasMaxLength(20);

            e.HasOne(c => c.UsuarioMestre)
             .WithMany()
             .HasForeignKey(c => c.IdAccount);

            e.HasMany(c => c.UsuarioCampanhas)
             .WithOne(uc => uc.Campanha)
             .HasForeignKey(uc => uc.IdCampanha);

            e.Ignore(c => c.Personagens);
            e.Ignore(c => c.FichasPendentes);
            e.Ignore(c => c.FichasAceitas);
            e.Ignore(c => c.Npcs);
            e.Ignore(c => c.Itens);
        });

        // =====================================================================
        // 3. CATÁLOGOS BASE DE PERSONAGEM (RAÇA, ANTECEDENTES E CLASSE)
        // =====================================================================
        modelBuilder.Entity<Raca>(e =>
        {
            e.ToTable("Raca");
            e.HasKey(r => r.IdRaca);
            e.Property(r => r.IdRaca).HasColumnName("id_raca");
            e.Property(r => r.Nome).HasColumnName("nome").HasMaxLength(50);
            e.Property(r => r.Tamanho).HasColumnName("tamanho").HasMaxLength(20);
            e.Property(r => r.DeslocamentoBase).HasColumnName("deslocamento_base");
            e.Property(r => r.VisaoNoEscuro).HasColumnName("visao_no_escuro");
        });

        modelBuilder.Entity<Antecedente>(e =>
        {
            e.ToTable("Antecedentes");
            e.HasKey(a => a.IdAntecedente);
            e.Property(a => a.IdAntecedente).HasColumnName("id_antecedente");
            e.Property(a => a.Nome).HasColumnName("nome").HasMaxLength(50);
            e.Property(a => a.ProficienciasPericias).HasColumnName("proficiencias_pericias");
            e.Property(a => a.EquipamentoInicial).HasColumnName("equipamento_inicial");
        });

        modelBuilder.Entity<Classe>(e =>
        {
            e.ToTable("Classe");
            e.HasKey(c => c.IdClasse);
            e.Property(c => c.IdClasse).HasColumnName("id_classe");
            e.Property(c => c.Nome).HasColumnName("nome").HasMaxLength(50);
            e.Property(c => c.DadoVida).HasColumnName("dado_vida").HasMaxLength(10);
            e.Property(c => c.AtributoPrimario).HasColumnName("atributo_primario").HasMaxLength(50);
        });

        // =====================================================================
        // 4. PERSONAGEM (ENTIDADE PRINCIPAL)
        // =====================================================================
        modelBuilder.Entity<Personagem>(e =>
        {
            e.ToTable("Personagem");
            e.HasKey(p => p.IdPersonagem);
            e.Property(p => p.IdPersonagem).HasColumnName("id_personagem");
            e.Property(p => p.IdAccount).HasColumnName("id_account");
            e.Property(p => p.IdRaca).HasColumnName("id_raca");
            e.Property(p => p.IdAntecedente).HasColumnName("id_antecedente");

            e.Property(p => p.Nome).HasColumnName("nome");
            e.Property(p => p.Nivel).HasColumnName("nivel");
            e.Property(p => p.ExperienciaXp).HasColumnName("experiencia_xp");
            e.Property(p => p.PvAtual).HasColumnName("pv_atual");
            e.Property(p => p.PvMaximo).HasColumnName("pv_maximo");
            e.Property(p => p.Iniciativa).HasColumnName("iniciativa");
            e.Property(p => p.Deslocamento).HasColumnName("deslocamento");
            e.Property(p => p.RacaTexto).HasColumnName("raca_texto");
            e.Property(p => p.ClasseTexto).HasColumnName("classe");
            e.Property(p => p.AntecedenteTexto).HasColumnName("antecedente_texto");
            e.Property(p => p.Tendencia).HasColumnName("tendencia");
            e.Property(p => p.Historia).HasColumnName("historia");
            e.Property(p => p.Imagem).HasColumnName("imagem").HasColumnType("varchar(max)");

            // Atributos principais
            e.Property(p => p.Forca).HasColumnName("forca");
            e.Property(p => p.Destreza).HasColumnName("destreza");
            e.Property(p => p.Constituicao).HasColumnName("constituicao");
            e.Property(p => p.Inteligencia).HasColumnName("inteligencia");
            e.Property(p => p.Sabedoria).HasColumnName("sabedoria");
            e.Property(p => p.Carisma).HasColumnName("carisma");

            // Características físicas e narrativas
            e.Property(p => p.Idade).HasColumnName("idade");
            e.Property(p => p.Genero).HasColumnName("genero");
            e.Property(p => p.Altura).HasColumnName("altura");
            e.Property(p => p.Peso).HasColumnName("peso");
            e.Property(p => p.Aparencia).HasColumnName("aparencia");
            e.Property(p => p.Aliados).HasColumnName("aliados");
            e.Property(p => p.Caracteristicas).HasColumnName("caracteristicas");
            e.Property(p => p.Tesouro).HasColumnName("tesouro");

            // Colunas 3FN de Diário e Personalidade
            e.Property(p => p.TracosPersonalidade).HasColumnName("tracos_personalidade");
            e.Property(p => p.PontoDeVista).HasColumnName("ponto_de_vista");
            e.Property(p => p.Ideais).HasColumnName("ideais");
            e.Property(p => p.Ligacoes).HasColumnName("ligacoes");
            e.Property(p => p.Defeitos).HasColumnName("defeitos");
            e.Property(p => p.Idiomas).HasColumnName("idiomas");

            // Chaves Estrangeiras 3FN para Raca e Antecedentes
            e.HasOne(p => p.Raca)
             .WithMany()
             .HasForeignKey(p => p.IdRaca)
             .IsRequired(false);

            e.HasOne(p => p.Antecedente)
             .WithMany()
             .HasForeignKey(p => p.IdAntecedente)
             .IsRequired(false);
        });

        // =====================================================================
        // 5. RELAÇÕES 3FN DO PERSONAGEM (CLASSES, PERÍCIAS, INVENTÁRIO, MAGIAS)
        // =====================================================================
        modelBuilder.Entity<PersonagemClasse>(e =>
        {
            e.ToTable("Personagem_Classe");
            e.HasKey(pc => new { pc.IdPersonagem, pc.IdClasse });
            e.Property(pc => pc.IdPersonagem).HasColumnName("id_personagem");
            e.Property(pc => pc.IdClasse).HasColumnName("id_classe");
            e.Property(pc => pc.NivelNaClasse).HasColumnName("nivel_na_classe");

            e.HasOne(pc => pc.Personagem)
             .WithMany(p => p.PersonagemClasses)
             .HasForeignKey(pc => pc.IdPersonagem)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pc => pc.Classe)
             .WithMany()
             .HasForeignKey(pc => pc.IdClasse)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonagemSalvaguarda>(e =>
        {
            e.ToTable("Personagem_Salvaguarda");
            e.HasKey(ps => new { ps.IdPersonagem, ps.Atributo });
            e.Property(ps => ps.IdPersonagem).HasColumnName("id_personagem");
            e.Property(ps => ps.Atributo).HasColumnName("atributo").HasMaxLength(30);

            e.HasOne(ps => ps.Personagem)
             .WithMany(p => p.Salvaguardas)
             .HasForeignKey(ps => ps.IdPersonagem)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonagemPericia>(e =>
        {
            e.ToTable("Personagem_Pericia");
            e.HasKey(pp => new { pp.IdPersonagem, pp.NomePericia });
            e.Property(pp => pp.IdPersonagem).HasColumnName("id_personagem");
            e.Property(pp => pp.NomePericia).HasColumnName("nome_pericia").HasMaxLength(50);

            e.HasOne(pp => pp.Personagem)
             .WithMany(p => p.Pericias)
             .HasForeignKey(pp => pp.IdPersonagem)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Inventario>(e =>
        {
            e.ToTable("Inventario");
            e.HasKey(inv => inv.IdInventario);
            e.Property(inv => inv.IdInventario).HasColumnName("id_inventario");
            e.Property(inv => inv.IdPersonagem).HasColumnName("id_personagem");
            e.Property(inv => inv.CapacidadeCarga).HasColumnName("capacidade_carga").HasColumnType("decimal(6,2)");
            e.Property(inv => inv.PesoAtual).HasColumnName("peso_atual").HasColumnType("decimal(6,2)");
            e.Property(inv => inv.Dinheiro).HasColumnName("dinheiro").HasColumnType("decimal(10,2)");
            e.Property(inv => inv.MoedasPC).HasColumnName("moedas_pc");
            e.Property(inv => inv.MoedasPP).HasColumnName("moedas_pp");
            e.Property(inv => inv.MoedasPE).HasColumnName("moedas_pe");
            e.Property(inv => inv.MoedasPO).HasColumnName("moedas_po");
            e.Property(inv => inv.MoedasPL).HasColumnName("moedas_pl");
            e.Property(inv => inv.Notas).HasColumnName("notas");

            e.HasOne(inv => inv.Personagem)
             .WithOne(p => p.Inventario)
             .HasForeignKey<Inventario>(inv => inv.IdPersonagem)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventarioItem>(e =>
        {
            e.ToTable("Inventario_Item");
            e.HasKey(ii => new { ii.IdInventario, ii.IdItem });
            e.Property(ii => ii.IdInventario).HasColumnName("id_inventario");
            e.Property(ii => ii.IdItem).HasColumnName("id_item");
            e.Property(ii => ii.Quantidade).HasColumnName("quantidade");

            e.HasOne(ii => ii.Inventario)
             .WithMany(inv => inv.Itens)
             .HasForeignKey(ii => ii.IdInventario)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ii => ii.Item)
             .WithMany()
             .HasForeignKey(ii => ii.IdItem)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Magia>(e =>
        {
            e.ToTable("Magia");
            e.HasKey(m => m.IdMagia);
            e.Property(m => m.IdMagia).HasColumnName("id_magia");
            e.Property(m => m.Nome).HasColumnName("nome").HasMaxLength(100);
            e.Property(m => m.CirculoNivel).HasColumnName("circulo_nivel");
            e.Property(m => m.Escola).HasColumnName("escola").HasMaxLength(50);
            e.Property(m => m.TempoConjuracao).HasColumnName("tempo_conjuracao").HasMaxLength(50);
            e.Property(m => m.Alcance).HasColumnName("alcance").HasMaxLength(50);
            e.Property(m => m.Componentes).HasColumnName("componentes").HasMaxLength(100);
            e.Property(m => m.Duracao).HasColumnName("duracao").HasMaxLength(50);
            e.Property(m => m.DanoEfeito).HasColumnName("dano_efeito").HasMaxLength(100);
        });

        modelBuilder.Entity<PersonagemMagia>(e =>
        {
            e.ToTable("Personagem_Magia");
            e.HasKey(pm => new { pm.IdPersonagem, pm.IdMagia });
            e.Property(pm => pm.IdPersonagem).HasColumnName("id_personagem");
            e.Property(pm => pm.IdMagia).HasColumnName("id_magia");

            e.HasOne(pm => pm.Personagem)
             .WithMany(p => p.PersonagemMagias)
             .HasForeignKey(pm => pm.IdPersonagem)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pm => pm.Magia)
             .WithMany()
             .HasForeignKey(pm => pm.IdMagia)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================================
        // 6. MONSTRO, NPC E ITEM
        // =====================================================================
        modelBuilder.Entity<Monstro>(e =>
        {
            e.ToTable("Monstro");
            e.HasKey(m => m.IdMonstro);
            e.Property(m => m.IdMonstro).HasColumnName("id_monstro");
            e.Property(m => m.IdAccount).HasColumnName("id_account");
            e.Property(m => m.Nome).HasColumnName("nome");
            e.Property(m => m.TipoMonstro).HasColumnName("tipo_monstro");
            e.Property(m => m.NivelDesafio).HasColumnName("nivel_desafio").HasColumnType("decimal(5,2)");
            e.Property(m => m.PvMaximo).HasColumnName("pv_maximo");
            e.Property(m => m.ClasseArmadura).HasColumnName("classe_armadura");
            e.Property(m => m.Tamanho).HasColumnName("tamanho");
            e.Property(m => m.Tendencia).HasColumnName("tendencia");
            e.Property(m => m.Deslocamento).HasColumnName("deslocamento");
            e.Property(m => m.Sentidos).HasColumnName("sentidos");
            e.Property(m => m.Resistencias).HasColumnName("resistencias");
            e.Property(m => m.Habilidades).HasColumnName("habilidades");
            e.Property(m => m.Descricao).HasColumnName("descricao");
            e.Property(m => m.Imagem).HasColumnName("imagem");

            e.HasOne(m => m.Usuario)
             .WithMany()
             .HasForeignKey(m => m.IdAccount);

            e.Ignore(m => m.Vida);
            e.Ignore(m => m.Defesa);
            e.Ignore(m => m.Tipo);
            e.Ignore(m => m.Nd);
        });

        modelBuilder.Entity<NPC>(e =>
        {
            e.ToTable("NPC");
            e.HasKey(n => n.IdNpc);
            e.Property(n => n.IdNpc).HasColumnName("id_npc");
            e.Property(n => n.IdAccount).HasColumnName("id_account");
            e.Property(n => n.Nome).HasColumnName("nome").HasMaxLength(100).IsRequired();
            e.Property(n => n.RacaTexto).HasColumnName("raca_texto").HasMaxLength(50);
            e.Property(n => n.ClasseTexto).HasColumnName("classe").HasMaxLength(50);
            e.Property(n => n.Nivel).HasColumnName("nivel");
            e.Property(n => n.AntecedenteTexto).HasColumnName("antecedente_texto").HasMaxLength(50);
            e.Property(n => n.Tendencia).HasColumnName("tendencia").HasMaxLength(50);
            e.Property(n => n.Forca).HasColumnName("forca");
            e.Property(n => n.Destreza).HasColumnName("destreza");
            e.Property(n => n.Constituicao).HasColumnName("constituicao");
            e.Property(n => n.Inteligencia).HasColumnName("inteligencia");
            e.Property(n => n.Sabedoria).HasColumnName("sabedoria");
            e.Property(n => n.Carisma).HasColumnName("carisma");
            e.Property(n => n.Historia).HasColumnName("historia");
            e.Property(n => n.Imagem).HasColumnName("imagem").HasMaxLength(255);
            e.Property(n => n.Ocupacao).HasColumnName("ocupacao").HasMaxLength(50);
            e.Property(n => n.Alinhamento).HasColumnName("alinhamento").HasMaxLength(50);
            e.Property(n => n.Localizacao).HasColumnName("localizacao").HasMaxLength(100);
            e.Property(n => n.Descricao).HasColumnName("descricao");

            e.HasOne(n => n.Usuario)
             .WithMany()
             .HasForeignKey(n => n.IdAccount)
             .IsRequired(false);
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("Item");
            e.HasKey(i => i.IdItem);
            e.Property(i => i.IdItem).HasColumnName("id_item");
            e.Property(i => i.IdAccount).HasColumnName("id_account");
            e.Property(i => i.Nome).HasColumnName("nome").HasMaxLength(100).IsRequired();
            e.Property(i => i.Tipo).HasColumnName("tipo").HasMaxLength(50);
            e.Property(i => i.Peso).HasColumnName("peso").HasColumnType("decimal(5, 2)");
            e.Property(i => i.Preco).HasColumnName("preco").HasColumnType("decimal(10, 2)");
            e.Property(i => i.Dano).HasColumnName("dano").HasMaxLength(20);
            e.Property(i => i.TipoDano).HasColumnName("tipo_dano").HasMaxLength(30);
            e.Property(i => i.Propriedades).HasColumnName("propriedades");
            e.Property(i => i.CaBonus).HasColumnName("ca_bonus");
            e.Property(i => i.Descricao).HasColumnName("descricao");
            e.Property(i => i.Raridade).HasColumnName("raridade").HasMaxLength(50);
            e.Property(i => i.Sintonizacao).HasColumnName("sintonizacao").HasMaxLength(50);
        });

        // =====================================================================
        // 7. TABELAS ASSOCIATIVAS DE CAMPANHA (N:N)
        // =====================================================================
        modelBuilder.Entity<UsuarioCampanha>(e =>
        {
            e.ToTable("Usuario_Campanha");
            e.HasKey(uc => new { uc.IdAccount, uc.IdCampanha });
            e.Property(uc => uc.IdAccount).HasColumnName("id_account");
            e.Property(uc => uc.IdCampanha).HasColumnName("id_campanha");

            e.HasOne(uc => uc.Usuario)
             .WithMany()
             .HasForeignKey(uc => uc.IdAccount);

            e.HasOne(uc => uc.Campanha)
             .WithMany(c => c.UsuarioCampanhas)
             .HasForeignKey(uc => uc.IdCampanha);
        });

        modelBuilder.Entity<PersonagemCampanha>(e =>
        {
            e.ToTable("Personagem_Campanha");
            e.HasKey(pc => new { pc.IdPersonagem, pc.IdCampanha });
            e.Property(pc => pc.IdPersonagem).HasColumnName("id_personagem");
            e.Property(pc => pc.IdCampanha).HasColumnName("id_campanha");
            e.Property(pc => pc.StatusAprovacao).HasColumnName("status_aprovacao").HasMaxLength(20);

            e.HasOne(pc => pc.Personagem)
             .WithMany(p => p.PersonagemCampanhas)
             .HasForeignKey(pc => pc.IdPersonagem);

            e.HasOne(pc => pc.Campanha)
             .WithMany(c => c.PersonagemCampanhas)
             .HasForeignKey(pc => pc.IdCampanha);
        });

        modelBuilder.Entity<MonstroCampanha>(e =>
        {
            e.ToTable("Monstro_Campanha");
            e.HasKey(mc => new { mc.IdMonstro, mc.IdCampanha });
            e.Property(mc => mc.IdCampanha).HasColumnName("id_campanha");
            e.Property(mc => mc.IdMonstro).HasColumnName("id_monstro");

            e.HasOne(mc => mc.Monstro)
             .WithMany(m => m.MonstroCampanhas)
             .HasForeignKey(mc => mc.IdMonstro);

            e.HasOne(mc => mc.Campanha)
             .WithMany(c => c.MonstroCampanhas)
             .HasForeignKey(mc => mc.IdCampanha);
        });

        modelBuilder.Entity<NpcCampanha>(e =>
        {
            e.ToTable("NPC_Campanha");
            e.HasKey(nc => new { nc.IdNpc, nc.IdCampanha });
            e.Property(nc => nc.IdNpc).HasColumnName("id_npc");
            e.Property(nc => nc.IdCampanha).HasColumnName("id_campanha");

            e.HasOne(nc => nc.Npc)
             .WithMany(n => n.NpcCampanhas)
             .HasForeignKey(nc => nc.IdNpc);

            e.HasOne(nc => nc.Campanha)
             .WithMany(c => c.NpcCampanhas)
             .HasForeignKey(nc => nc.IdCampanha);
        });

        modelBuilder.Entity<ItemCampanha>(e =>
        {
            e.ToTable("Item_Campanha");
            e.HasKey(ic => new { ic.IdCampanha, ic.IdItem });
            e.Property(ic => ic.IdCampanha).HasColumnName("id_campanha");
            e.Property(ic => ic.IdItem).HasColumnName("id_item");

            e.HasOne(ic => ic.Item)
             .WithMany()
             .HasForeignKey(ic => ic.IdItem);

            e.HasOne(ic => ic.Campanha)
             .WithMany(c => c.ItemCampanhas)
             .HasForeignKey(ic => ic.IdCampanha);
        });
    }
}