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
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Perfil> Perfis { get; set; }
    public DbSet<Personagem> Personagens { get; set; }
    public DbSet<Campanha> Campanhas { get; set; }
    public DbSet<NPC> Npcs { get; set; }
    public DbSet<Item> Itens { get; set; }
    public DbSet<Monstro> Monstros { get; set; }
    public DbSet<PersonagemCampanha> PersonagensCampanha { get; set; }
    public DbSet<UsuarioCampanha> UsuariosCampanha { get; set; }
    public DbSet<MonstroCampanha> MonstroCampanhas { get; set; }
    public DbSet<NpcCampanha> NpcCampanhas { get; set; }

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

        // 1. USUARIO
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

        // 2. PERFIL
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

        // 3. CAMPANHA
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
            e.Ignore(c => c.Npcs);
        });

        // 4. PERSONAGEM
        modelBuilder.Entity<Personagem>(e =>
        {
            e.ToTable("Personagem");
            e.HasKey(p => p.IdPersonagem);
            e.Property(p => p.IdPersonagem).HasColumnName("id_personagem");
            e.Property(p => p.IdAccount).HasColumnName("id_account");
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
            e.Property(p => p.Imagem).HasColumnName("imagem");
            e.Property(p => p.Forca).HasColumnName("forca");
            e.Property(p => p.Destreza).HasColumnName("destreza");
            e.Property(p => p.Constituicao).HasColumnName("constituicao");
            e.Property(p => p.Inteligencia).HasColumnName("inteligencia");
            e.Property(p => p.Sabedoria).HasColumnName("sabedoria");
            e.Property(p => p.Carisma).HasColumnName("carisma");
            e.Property(p => p.Idade).HasColumnName("idade");
            e.Property(p => p.Genero).HasColumnName("genero");
            e.Property(p => p.Altura).HasColumnName("altura");
            e.Property(p => p.Peso).HasColumnName("peso");
            e.Property(p => p.Aparencia).HasColumnName("aparencia");
            e.Property(p => p.Aliados).HasColumnName("aliados");
            e.Property(p => p.Caracteristicas).HasColumnName("caracteristicas");
            e.Property(p => p.Tesouro).HasColumnName("tesouro");
        });

        // 5. MONSTRO
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

        // 6. NPC
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

        // 7. ITEM
        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("Item");
            e.HasKey(i => i.IdItem);
            e.Property(i => i.IdItem).HasColumnName("id_item");
            e.Property(i => i.Nome).HasColumnName("nome").HasMaxLength(100).IsRequired();
            e.Property(i => i.Tipo).HasColumnName("tipo").HasMaxLength(50);
            e.Property(i => i.Peso).HasColumnName("peso").HasColumnType("decimal(5, 2)");
            e.Property(i => i.Preco).HasColumnName("preco").HasColumnType("decimal(10, 2)");
            e.Property(i => i.Dano).HasColumnName("dano").HasMaxLength(20);
            e.Property(i => i.TipoDano).HasColumnName("tipo_dano").HasMaxLength(30);
            e.Property(i => i.Propriedades).HasColumnName("propriedades");
            e.Property(i => i.CaBonus).HasColumnName("ca_bonus");
            e.Property(i => i.Descricao).HasColumnName("descricao");
        });

        // 8. TABELAS ASSOCIATIVAS (N:N)
        modelBuilder.Entity<PersonagemCampanha>(e =>
        {
            e.ToTable("Personagem_Campanha");
            e.HasKey(pc => new { pc.IdPersonagem, pc.IdCampanha });
            e.Property(pc => pc.IdPersonagem).HasColumnName("id_personagem");
            e.Property(pc => pc.IdCampanha).HasColumnName("id_campanha");

            e.HasOne(pc => pc.Personagem)
             .WithMany(p => p.PersonagemCampanhas)
             .HasForeignKey(pc => pc.IdPersonagem);

            e.HasOne(pc => pc.Campanha)
             .WithMany(c => c.PersonagemCampanhas)
             .HasForeignKey(pc => pc.IdCampanha);
        });

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
    }
}