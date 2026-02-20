using Deadlock.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deadlock.Infrastructure.Data.Configrations
{
    internal class AppUserConfigration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> builder)
        {
            builder.Property(c => c.Name)
                  .HasMaxLength(200);

            builder.Property(c => c.ImgUrl)
                 .HasMaxLength(500);

            builder.Property(c => c.RefreshToken)
               .HasMaxLength(2000)
               .IsRequired(false);

            builder.HasOne(u => u.Manager)
           .WithMany(m => m.Subordinates)
           .HasForeignKey(u => u.ManagerId)
           .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}
