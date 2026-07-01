
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class GenericRepository<T>(ApplicationDbContext context, IBackupTools? backupTools = null) : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context = context;
        private readonly IBackupTools? _backupTools = backupTools;

        public void Add(T entity)
        {
            _context.Set<T>().Add(entity);
        }

        public ValueTask<EntityEntry<T>> AddAsync(T entity)
        {
            return _context.Set<T>().AddAsync(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            _context.Set<T>().AddRange(entities);
        }

        public Task AddRangeAsync(IEnumerable<T> entities)
        {
            return _context.Set<T>().AddRangeAsync(entities);
        }

        public int ExecuteDeleteAll()
        {
            return _context.Set<T>().ExecuteDelete();
        }

        public Task<int> ExecuteDeleteAllAsync()
        {
            return _context.Set<T>().ExecuteDeleteAsync();
        }

        public IQueryable<T> Find(Expression<Func<T, bool>> expression)
        {
            return _context.Set<T>().Where(expression);
        }

        public IEnumerable<T> Get(Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, int? limit = null, int? offset = null, string includeProperties = "")
        {
            IQueryable<T> query = _context.Set<T>();

            if (filter != null)
            {
                query = query.Where(filter);
            }

            foreach (var includeProperty in includeProperties.Split
                (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty);
            }

            if (orderBy != null)
            {
                query = orderBy(query).AsQueryable();
            }

            if (offset != null)
            {
                query = query.Skip((int)offset);
            }

            if (limit != null)
            {
                query = query.Take((int)limit);
            }
            return query.ToList();
        }

        public async Task<List<T>> GetAsync(Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, int? limit = null, int? offset = null, string includeProperties = "")
        {
            IQueryable<T> query = _context.Set<T>();

            if (filter != null)
            {
                query = query.Where(filter);
            }

            foreach (var includeProperty in includeProperties.Split
                (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty);
            }

            if (orderBy != null)
            {
                query = orderBy(query).AsQueryable();
            }

            if (offset != null)
            {
                query = query.Skip((int)offset);
            }

            if (limit != null)
            {
                query = query.Take((int)limit);
            }

            return await query.ToListAsync();
        }

        public IEnumerable<T> GetAll()
        {
            return _context.Set<T>();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public T? GetById(int? id)
        {
            return _context.Set<T>().Find(id);
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _context.Set<T>().FindAsync(id);
        }


        public void Remove(T entity)
        {
            _context.Set<T>().Remove(entity);
        }
        public void RemoveRange(IEnumerable<T> entities)
        {
            _context.Set<T>().RemoveRange(entities);
        }

        public void Update(T entity)
        {
            _context.Set<T>().Update(entity);
        }

        public void UpdateRange(IEnumerable<T> entities)
        {
            _context.Set<T>().UpdateRange(entities);
        }

        public int Count()
        {
            return _context.Set<T>().Count();
        }

        public Task<int> CountAsync()
        {
            return _context.Set<T>().CountAsync();
        }

        public virtual async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            if (_backupTools != null)
            {
                await _backupTools.BackupTable<T>(context, backupDirectory, logger);
                return;
            }

            var fileName = System.IO.Path.Combine(backupDirectory, $"{typeof(T).Name}.json");
            var options = new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                WriteIndented = true
            };
            await using var fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, bufferSize: 65536, useAsync: true);
            await using var writer = new System.Text.Json.Utf8JsonWriter(fileStream);
            writer.WriteStartArray();
            var count = 0;
            await foreach (var record in context.Set<T>().AsNoTracking().AsAsyncEnumerable())
            {
                System.Text.Json.JsonSerializer.Serialize(writer, record, options);
                count++;
                if (count % 500 == 0)
                    await writer.FlushAsync();
            }
            writer.WriteEndArray();
            await writer.FlushAsync();
            logger?.LogDebug("Backed up {Count} records to {Name}", count, typeof(T).Name);
        }

        public virtual async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            if (_backupTools != null)
            {
                await _backupTools.RestoreTable<T>(context, backupDirectory, logger);
                return;
            }

            try
            {
                var fileName = System.IO.Path.Combine(backupDirectory, $"{typeof(T).Name}.json");
                if (!System.IO.File.Exists(fileName)) return;

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                await using var fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, bufferSize: 65536, useAsync: true);
                var records = await System.Text.Json.JsonSerializer.DeserializeAsync<List<T>>(fileStream, options);
                if (records == null) throw new Exception($"{typeof(T).Name}.json was null");

                await context.Set<T>().ExecuteDeleteAsync();
                context.Set<T>().AddRange(records);
                logger?.LogDebug("Restored {Count} records from {Name}", records.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(T).Name);
                throw;
            }
        }
    }
}
