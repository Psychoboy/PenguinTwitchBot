using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace PenguinTwitchBot.Database.Bot.Core.Database
{
	/// <summary>
	/// Helper class to configure DateTime properties for multi-database support.
	/// PostgreSQL requires explicit handling of timezone-aware datetimes.
	/// </summary>
	public static class DateTimeConfigurationHelper
	{
		/// <summary>
		/// Configures all DateTime properties in the model for the specified provider.
		/// </summary>
		public static void ConfigureDateTimes(this ModelBuilder modelBuilder, string? provider)
		{
			// Apply datetime configuration for all providers that don't store timezone info natively.
			// PostgreSQL/SQLite also need Kind tagging on reads.
			if (provider != "postgres" && provider != "sqlite")
				return;

			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				foreach (var property in entityType.GetProperties())
				{
					// Only configure properties of type DateTime or DateTime?
					if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
					{
						// For PostgreSQL, use timestamp with time zone and convert to UTC.
						if (provider == "postgres")
						{
							property.SetColumnType("timestamp with time zone");
						}

						// Add a value converter to handle Local -> UTC conversion
						var clrType = property.ClrType;
						if (clrType == typeof(DateTime))
						{
							property.SetValueConverter(
								new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
									v => v.Kind == DateTimeKind.Local
										? v.ToUniversalTime()
										: (v.Kind == DateTimeKind.Unspecified
											? DateTime.SpecifyKind(v, DateTimeKind.Utc)
											: v),
									v => v.Kind == DateTimeKind.Unspecified
										? DateTime.SpecifyKind(v, DateTimeKind.Utc)
										: v
								));
						}
						else if (clrType == typeof(DateTime?))
						{
							property.SetValueConverter(
								new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
									v => v.HasValue
										? (v.Value.Kind == DateTimeKind.Local
											? v.Value.ToUniversalTime()
											: (v.Value.Kind == DateTimeKind.Unspecified
												? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
												: v.Value))
										: null,
									v => v.HasValue
										? (v.Value.Kind == DateTimeKind.Unspecified
											? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
											: v.Value)
										: null
								));
						}
					}
				}
			}
		}
	}
}
