using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Data.Configurations;

internal class TelegramConfiguration : IEntityTypeConfiguration<TelegramEntity>
{
    public void Configure(EntityTypeBuilder<TelegramEntity> builder)
    {
        builder.HasOne(t => t.User)
            .WithOne()
            .HasForeignKey<TelegramEntity>(t => t.UserId);

        builder.HasIndex(t => t.TelegramId).IsUnique();
    }
}
