using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class GenericRepository<T>(ApplicationDbContext context) : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context = context;

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
    }
}
