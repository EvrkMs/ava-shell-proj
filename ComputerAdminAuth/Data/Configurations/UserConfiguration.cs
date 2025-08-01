using ComputerAdminAuth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComputerAdminAuth.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.Property(u => u.FullName)
            .HasMaxLength(200);

        builder.Property(u => u.Status)
            .HasConversion<string>() // enum в строку, можно заменить на .HasConversion<int>() при необходимости
            .IsRequired();
    }
}
