using Microsoft.EntityFrameworkCore;

namespace Auth.EntityFramework.Data;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Автоматически добавляет HasIndex() для всех FK и PK.
    /// </summary>
    /// <param name="modelBuilder">EF ModelBuilder</param>
    /// <param name="indexMethod">Тип индекса: "btree" (по умолчанию) или "hash"</param>
    public static void AddIndexesForForeignKeys(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var fk in entityType.GetForeignKeys())
            {
                // Добавляем индекс только если FK участвует в каскадном удалении или явно используется
                if (fk.DeleteBehavior != DeleteBehavior.NoAction)
                {
                    var props = fk.Properties.Select(p => p.Name).ToArray();
                    modelBuilder.Entity(entityType.ClrType)
                        .HasIndex(props)
                        .HasDatabaseName($"IX_{entityType.GetTableName()}_{string.Join("_", props)}");
                }
            }
        }
    }
}
