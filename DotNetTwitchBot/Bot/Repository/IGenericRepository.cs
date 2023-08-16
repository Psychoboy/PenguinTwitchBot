using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace DotNetTwitchBot.Bot.Repository
{
    public interface IGenericRepository<T> where T : class
    {
        T? GetById(int id);
        Task<T?> GetByIdAsync(int id);
        IEnumerable<T> GetAll();
        Task<IEnumerable<T>> GetAllAsync();
        IQueryable<T> Find(Expression<Func<T, bool>> expression);
        void Add(T entity);
        ValueTask<EntityEntry<T>> AddAsync(T entity);
        void AddRange(IEnumerable<T> entities);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        void Update(T entity);
        void UpdateRange(IEnumerable<T> entities);
    }
}
