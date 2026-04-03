using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    /// <summary>
    /// Extension methods for automatically configuring SubActions in EF Core.
    /// This eliminates the need to manually add each SubAction to DbContext.OnModelCreating.
    /// </summary>
    public static class SubActionModelBuilderExtensions
    {
        /// <summary>
        /// Automatically configures all SubAction types using TPC mapping strategy.
        /// Call this from DbContext.OnModelCreating().
        /// </summary>
        public static void ConfigureSubActions(this ModelBuilder modelBuilder)
        {
            // Configure TPC (Table Per Concrete Type) for SubActions
            modelBuilder.Entity<SubActionType>()
                .UseTpcMappingStrategy();

            // Automatically configure each concrete SubAction type from the registry
            foreach (var metadata in SubActionRegistry.Metadata.Values)
            {
                ConfigureSubActionType(modelBuilder, metadata);
            }

            // Configure the relationship between ActionType and SubActionType
            modelBuilder.Entity<ActionType>()
                .HasMany(a => a.SubActions)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureSubActionType(ModelBuilder modelBuilder, SubActionMetadata metadata)
        {
            // Get the generic Entity<T> method
            var entityMethod = typeof(ModelBuilder)
                .GetMethod(nameof(ModelBuilder.Entity), Type.EmptyTypes)
                ?.MakeGenericMethod(metadata.Type);

            if (entityMethod == null) return;

            // Call modelBuilder.Entity<TSubActionType>()
            var entityTypeBuilder = entityMethod.Invoke(modelBuilder, null);
            if (entityTypeBuilder == null) return;

            // Get the ToTable extension method from RelationalEntityTypeBuilderExtensions
            var entityTypeBuilderType = entityTypeBuilder.GetType();
            var toTableMethod = typeof(Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.ToTable) 
                    && m.GetParameters().Length == 2 
                    && m.GetParameters()[1].ParameterType == typeof(string));

            if (toTableMethod == null) return;

            // Call .ToTable(tableName) - extension methods need the instance as first parameter
            toTableMethod.Invoke(null, new object[] { entityTypeBuilder, metadata.TableName });
        }
    }
}
