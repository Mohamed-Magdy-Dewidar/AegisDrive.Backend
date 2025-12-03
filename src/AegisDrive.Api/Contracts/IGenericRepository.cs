using AegisDrive.Api.Entities;
using System.Linq.Expressions;

namespace AegisDrive.Api.Contracts
{

    public interface IGenericRepository<TEntity, TKey> where TEntity : BaseEntity<TKey>
    {

        IQueryable<TEntity> GetAll(bool trackChanges = false);
        
        IQueryable<TEntity> GetAll(Expression<Func<TEntity, bool>> expression, bool trackChanges = false);
        
        Task<TEntity?> GetByIdAsync(TKey id);

        Task<TEntity?> GetByIdAsync(TKey id , bool trackChanges = false);

        Task AddAsync(TEntity entity);
        Task AddRangeAsync(IEnumerable<TEntity> entities); 
        void Update(TEntity entity);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        public  Task DeleteAsync(TKey id);


        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> expression, CancellationToken cancellationToken = default);

        public void SaveInclude(TEntity entity, params string[] includedProperties);
    }
}