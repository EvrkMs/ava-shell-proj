using ComputerAdminAuth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComputerAdminAuth.Data.Configurations;

public class TelegramConfiguration : IEntityTypeConfiguration<TelegramEntity>
{
    public void Configure(EntityTypeBuilder<TelegramEntity> builder)
    {
        builder.HasOne(t => t.User)
            .WithOne()
            .HasForeignKey<TelegramEntity>(t => t.UserId);
    }
}
