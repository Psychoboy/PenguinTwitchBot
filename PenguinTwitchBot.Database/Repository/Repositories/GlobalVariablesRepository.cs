using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models;
using Npgsql;
using Microsoft.Data.Sqlite;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class GlobalVariablesRepository(ApplicationDbContext context) : GenericRepository<GlobalVariable>(context), IGlobalVariablesRepository
    {
        private static string NormalizeName(string name)
        {
            return GlobalVariable.NormalizeName(name);
        }

        public async Task<List<GlobalVariable>> GetAllOrderedAsync()
        {
            return await _context.GlobalVariables
                .AsNoTracking()
                .OrderBy(variable => variable.Name)
                .ToListAsync();
        }

        public async Task<GlobalVariable?> GetByNameAsync(string name)
        {
            var normalizedName = NormalizeName(name);
            return await _context.GlobalVariables
                .AsNoTracking()
                .FirstOrDefaultAsync(variable => variable.Name == normalizedName);
        }

        public async Task<GlobalVariable> UpsertAsync(string name, string value)
        {
            var normalizedName = NormalizeName(name);
            var entity = await _context.GlobalVariables
                .FirstOrDefaultAsync(variable => variable.Name == normalizedName);

            if (entity == null)
            {
                entity = new GlobalVariable
                {
                    Name = normalizedName,
                    Value = value
                };
                await _context.GlobalVariables.AddAsync(entity);

                try
                {
                    await _context.SaveChangesAsync();
                    return entity;
                }
                catch (DbUpdateException ex) when (IsUniqueNameViolation(ex))
                {
                    _context.Entry(entity).State = EntityState.Detached;
                    entity = await _context.GlobalVariables
                        .FirstOrDefaultAsync(variable => variable.Name == normalizedName);

                    if (entity == null)
                        throw;

                    entity.Value = value;
                    await _context.SaveChangesAsync();
                    return entity;
                }
            }
            else
            {
                entity.Name = normalizedName;
                entity.Value = value;

                await _context.SaveChangesAsync();
                return entity;
            }
        }

        public async Task<bool> DeleteByNameAsync(string name)
        {
            var normalizedName = NormalizeName(name);
            var entity = await _context.GlobalVariables
                .FirstOrDefaultAsync(variable => variable.Name == normalizedName);

            if (entity == null)
            {
                return false;
            }

            _context.GlobalVariables.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        private static bool IsUniqueNameViolation(DbUpdateException exception)
        {
            if (exception.InnerException is SqliteException sqliteException)
            {
                return sqliteException.SqliteExtendedErrorCode == 2067 || sqliteException.SqliteErrorCode == 19;
            }

            if (exception.InnerException is PostgresException postgresException)
            {
                return postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
            }

            return false;
        }
    }
}