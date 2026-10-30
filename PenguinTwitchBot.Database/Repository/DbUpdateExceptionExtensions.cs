using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PenguinTwitchBot.Database.Repository
{
    public static class DbUpdateExceptionExtensions
    {
        public static bool IsUniqueViolation(this DbUpdateException exception)
        {
            return exception.InnerException switch
            {
                SqliteException sqlite => sqlite.SqliteExtendedErrorCode is 2067 or 1555,
                PostgresException postgres => postgres.SqlState == PostgresErrorCodes.UniqueViolation,
                _ => false
            };
        }
    }
}
