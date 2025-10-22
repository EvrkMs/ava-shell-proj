using Microsoft.EntityFrameworkCore;
using System.Linq;

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
                    var propertyNames = fk.Properties.Select(p => p.Name).ToArray();
                    var hasExistingIndex = entityType.GetIndexes()
                        .Any(idx => idx.Properties.Select(p => p.Name)
                            .SequenceEqual(propertyNames));

                    if (hasExistingIndex) continue;

                    modelBuilder.Entity(entityType.ClrType)
                        .HasIndex(propertyNames)
                        .HasDatabaseName($"IX_{entityType.GetTableName()}_{string.Join("_", propertyNames)}");
                }
            }
        }
    }
}
