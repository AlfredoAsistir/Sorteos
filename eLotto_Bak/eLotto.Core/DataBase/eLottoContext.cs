using Microsoft.EntityFrameworkCore;
using eLotto.Core.Models;
using eLotto.Core.Data.Configurations;
using eLotto.Core.Services;

namespace eLotto.Core.Data
{
    public class eLottoContext : DbContext
    {
        public eLottoContext(DbContextOptions<eLottoContext> options)
            : base(options)
        {
        }

        public DbSet<Users> Users { get; set; }
        public DbSet<Rol> Rol { get; set; }
        public DbSet<UserRols> UserRols { get; set; }
        public DbSet<UserWallet> UserWallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<Sorteos> Sorteos { get; set; }
        public DbSet<SorteosBoletos> SorteosBoletos { get; set; }
        public DbSet<SorteosRascaditoPremios> SorteosRascaditoPremios { get; set; }
        public DbSet<SorteosRascaditos> SorteosRascaditos { get; set; }
        public DbSet<BoletosConfirmados> BoletosConfirmados { get; set; }
        public DbSet<BoletosConfirmadosHistorial> BoletosConfirmadosHistorial { get; set; }
        public DbSet<GanadoresSorteos> GanadoresSorteos { get; set; }
        public DbSet<SorteosRascaditosGanadoresHistorial> SorteosRascaditosGanadoresHistorial { get; set; }
        public DbSet<ReferralProgramSettings> ReferralProgramSettings { get; set; }
        public DbSet<ReferralDepositReward> ReferralDepositRewards { get; set; }
        public DbSet<ReferralWinnerCashReward> ReferralWinnerCashRewards { get; set; }

        public override int SaveChanges()
        {
            StampNewDates();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampNewDates();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampNewDates();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampNewDates();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void StampNewDates()
        {
            var now = ApplicationClock.Now;
            foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Added))
            {
                switch (entry.Entity)
                {
                    case SorteosRascaditos item when item.FechaGeneracion == default:
                        item.FechaGeneracion = now;
                        break;
                    case BoletosConfirmadosHistorial item when item.FechaArchivado == default:
                        item.FechaArchivado = now;
                        break;
                    case SorteosRascaditosGanadoresHistorial item when item.FechaArchivado == default:
                        item.FechaArchivado = now;
                        break;
                    case ReferralDepositReward item when item.CreatedAt == default:
                        item.CreatedAt = now;
                        break;
                    case ReferralWinnerCashReward item when item.CreatedAt == default:
                        item.CreatedAt = now;
                        break;
                    case ReferralProgramSettings item when item.UpdatedAt == default:
                        item.UpdatedAt = now;
                        break;
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new UserWalletConfiguration());
            modelBuilder.ApplyConfiguration(new WalletTransactionConfiguration());
            modelBuilder.ApplyConfiguration(new SorteosConfiguration());
            modelBuilder.ApplyConfiguration(new SorteosBoletosConfiguration());
            modelBuilder.ApplyConfiguration(new SorteosRascaditoPremiosConfiguration());
            modelBuilder.ApplyConfiguration(new SorteosRascaditosConfiguration());
            modelBuilder.ApplyConfiguration(new BoletosConfirmadosConfiguration());
            modelBuilder.ApplyConfiguration(new BoletosConfirmadosHistorialConfiguration());
            modelBuilder.ApplyConfiguration(new GanadoresSorteosConfiguration());
            modelBuilder.ApplyConfiguration(new SorteosRascaditosGanadoresHistorialConfiguration());
            modelBuilder.ApplyConfiguration(new ReferralProgramSettingsConfiguration());
            modelBuilder.ApplyConfiguration(new ReferralDepositRewardConfiguration());
            modelBuilder.ApplyConfiguration(new ReferralWinnerCashRewardConfiguration());

            modelBuilder.Entity<Users>(entity =>
            {
                entity.ToTable("Users", table => table.HasCheckConstraint(
                    "CK_Users_ReferralCode_Format",
                    "DATALENGTH([ReferralCode]) = 8 AND [ReferralCode] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ]%'"));
                entity.Property(user => user.ActiveSessionId);
                entity.Property(user => user.WhatsApp)
                    .HasMaxLength(15)
                    .IsRequired();

                entity.Property(user => user.ConfirmCode)
                    .HasMaxLength(64);

                entity.Property(user => user.StripeCustomerId)
                    .HasMaxLength(255);

                entity.Property(user => user.ReferralCode)
                    .HasMaxLength(8)
                    .IsUnicode(false)
                    .IsRequired();

                entity.HasIndex(user => user.ReferralCode)
                    .IsUnique()
                    .HasDatabaseName("UX_Users_ReferralCode");

                entity.HasIndex(user => user.ReferredByUserId)
                    .HasDatabaseName("IX_Users_ReferredByUserId");

                entity.HasOne(user => user.Referrer)
                    .WithMany(user => user.Referrals)
                    .HasForeignKey(user => user.ReferredByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(user => user.StripeCustomerId)
                    .IsUnique()
                    .HasFilter("[StripeCustomerId] IS NOT NULL");

                entity.HasIndex(user => user.WhatsApp)
                    .IsUnique()
                    .HasDatabaseName("UX_Users_WhatsApp");
            });

        }
    }
}



