using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq;

namespace Auth.EntityFramework.Data;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Автоматически добавляет HasIndex() для всех FK и PK.
    /// </summary>
    /// <param name="modelBuilder">EF ModelBuilder</param>
    /// <param name="indexMethod">Тип индекса: "btree" (по умолчанию) или "hash"</param>
    public static void AddIndexesForKeys(this ModelBuilder modelBuilder, string? indexMethod = null)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // --- Primary Key ---
            var pk = entityType.FindPrimaryKey();
            if (pk != null)
            {
                var pkProps = pk.Properties.Select(p => p.Name).ToArray();

                // Проверим, есть ли уже индекс на PK
                bool hasPkIndex = entityType
                    .GetIndexes()
                    .Any(i => i.Properties.Select(p => p.Name).SequenceEqual(pkProps));

                if (!hasPkIndex)
                {
                    var indexBuilder = modelBuilder.Entity(entityType.ClrType)
                        .HasIndex(pkProps)
                        .HasDatabaseName($"IX_{entityType.GetTableName()}_PK");

                    if (indexMethod != null)
                        indexBuilder.HasMethod(indexMethod);
                }
            }

            // --- Foreign Keys ---
            foreach (var fk in entityType.GetForeignKeys())
            {
                var fkProps = fk.Properties.Select(p => p.Name).ToArray();

                // Проверим, есть ли уже индекс на FK
                bool hasFkIndex = entityType
                    .GetIndexes()
                    .Any(i => i.Properties.Select(p => p.Name).SequenceEqual(fkProps));

                if (!hasFkIndex)
                {
                    var indexBuilder = modelBuilder.Entity(entityType.ClrType)
                        .HasIndex(fkProps)
                        .HasDatabaseName($"IX_{entityType.GetTableName()}_{string.Join("_", fkProps)}");

                    if (indexMethod != null)
                        indexBuilder.HasMethod(indexMethod);
                }
            }
        }
    }
}
