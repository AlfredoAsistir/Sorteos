using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using eLotto.Core.Models;

namespace eLotto.Core.Data.Configurations
{
    public class UserWalletConfiguration : IEntityTypeConfiguration<UserWallet>
    {
        public void Configure(EntityTypeBuilder<UserWallet> builder)
        {
            builder.ToTable("UserWallets");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Balance).HasPrecision(18, 2);
            builder.HasIndex(x => x.UserId).IsUnique();
            builder.HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<UserWallet>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
