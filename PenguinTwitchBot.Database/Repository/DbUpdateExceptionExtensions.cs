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
                SqliteException sqlite => sqlite.SqliteExtendedErrorCode == 2067 || sqlite.SqliteErrorCode == 19,
                PostgresException postgres => postgres.SqlState == PostgresErrorCodes.UniqueViolation,
                _ => false
            };
        }
    }
}
