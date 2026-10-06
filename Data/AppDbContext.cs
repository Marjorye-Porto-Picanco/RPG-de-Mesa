using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Models;

namespace RPGdeMesa.Data
{
	public class AppDbContext : DbContext
	{
		// Define as tabelas do banco de dados
		public DbSet<Usuario> Usuarios { get; set; }
		public DbSet<Perfil> Perfeis { get; set; }
		public DbSet<Personagem> Personagens { get; set; }

		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
		{
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if (!optionsBuilder.IsConfigured)
			{
				// Substitua com os dados da sua instância local do SQL Server
				string connectionString = @"Server=localhost;
										  Database=rpg_system;
										  Trusted_Connection=True;
										  TrustServerCertificate=True;";

                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,                              // Tenta até 5 vezes se houver falha transitória
                        maxRetryDelay: TimeSpan.FromSeconds(5),        // Aguarda até 5 segundos entre as tentativas
                        errorNumbersToAdd: null);
                });
            }
		}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relacionamento 1:1 entre Usuario e Perfil
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Perfil)
                .WithOne(p => p.Usuario)
                .HasForeignKey<Perfil>(p => p.IdAccount);

            // Relacionamento 1:N entre Usuario e Personagem
            modelBuilder.Entity<Usuario>()
                .HasMany(u => u.Personagens)
                .WithOne(p => p.Usuario)
                .HasForeignKey(p => p.IdAccount);
        }
    }
}