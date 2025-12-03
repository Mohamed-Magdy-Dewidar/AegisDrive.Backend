using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;



namespace AegisDrive.Api.DataBase;

public class GenericRepository<TEntity, TKey> : IGenericRepository<TEntity , TKey> where TEntity : BaseEntity<TKey>
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;


    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public IQueryable<TEntity> GetAll(bool trackChanges = false) =>
           !trackChanges
               ? _context.Set<TEntity>().AsNoTracking()
               : _context.Set<TEntity>();

    public IQueryable<TEntity> GetAll(Expression<Func<TEntity, bool>> expression , bool trackChanges = false) =>
           !trackChanges
               ? _context.Set<TEntity>().AsNoTracking().Where(expression)
               : _context.Set<TEntity>().Where(expression);

 
    public async Task<TEntity?> GetByIdAsync(TKey id) =>
        await _context.Set<TEntity>().FindAsync(id);

    public Task<TEntity?> GetByIdAsync(TKey id, bool trackChanges = false)
        => !trackChanges ? _context.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(e => e.Id.Equals(id))
        : _context.Set<TEntity>().FirstOrDefaultAsync(e => e.Id.Equals(id));

    public async Task AddAsync(TEntity entity) =>
        await _context.Set<TEntity>().AddAsync(entity);

    public async Task AddRangeAsync(IEnumerable<TEntity> entities)
        => await _context.Set<TEntity>().AddRangeAsync(entities);

    public void Update(TEntity entity) =>
        _context.Set<TEntity>().Update(entity);

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> expression, CancellationToken cancellationToken = default)
         => await _context.Set<TEntity>().AsNoTracking().AnyAsync(expression, cancellationToken);


    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

 
    public virtual async Task DeleteAsync(TKey id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            entity.IsDeleted = true;
            _dbSet.Update(entity);
        }
    }



    public void SaveInclude(TEntity entity, params string[] includedProperties)
    {
        // Attempt to find the entity in the local change tracker first
        var localEntity = _dbSet.Local.FirstOrDefault(e => e.Id.Equals(entity.Id));
        EntityEntry entry;

        if (localEntity != null)
        {
            // If the entity is already being tracked, use the local entry
            entry = _context.Entry(localEntity);
        }
        else
        {
            // If the entity is not in the change tracker, explicitly fetch it
            entry = _context.Entry(entity);
        }

        // Iterate through the properties and set the IsModified flag for the included properties
        foreach (var property in entry.Properties)
        {
            // Set IsModified to true for the properties you want to update, false otherwise
            if (includedProperties.Contains(property.Metadata.Name))
            {
                property.IsModified = true;
            }
            else
            {
                property.IsModified = false;
            }
        }
    }

  
}

