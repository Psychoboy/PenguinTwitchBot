using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class GlobalVariablesRepository(ApplicationDbContext context) : GenericRepository<GlobalVariable>(context), IGlobalVariablesRepository
    {
        private static string NormalizeName(string name)
        {
            return name.Trim().ToLowerInvariant();
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
            var variables = await _context.GlobalVariables
                .AsNoTracking()
                .ToListAsync();

            return variables.FirstOrDefault(variable => variable.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
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
            }
            else
            {
                entity.Name = normalizedName;
                entity.Value = value;
            }

            await _context.SaveChangesAsync();
            return entity;
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
    }
}