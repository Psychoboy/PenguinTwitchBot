using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace DotNetTwitchBot.Bot.Core.Database
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
			// Only apply custom configuration for PostgreSQL
			if (provider != "postgres")
				return;

			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				foreach (var property in entityType.GetProperties())
				{
					// Only configure properties of type DateTime or DateTime?
					if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
					{
						// For PostgreSQL, use timestamp with time zone and convert to UTC
						property.SetColumnType("timestamp with time zone");

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
									v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
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
									v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null
								));
						}
					}
				}
			}
		}
	}
}
