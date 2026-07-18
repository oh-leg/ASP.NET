using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain;
using PromoCodeFactory.Core.Exceptions;

namespace PromoCodeFactory.DataAccess.Repositories;

internal class EfRepository<T>(PromoCodeFactoryDbContext context) : IRepository<T> where T : BaseEntity
{
    protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query) => query;

    public async Task Add(T entity, CancellationToken ct)
    {
        await context.Set<T>()
            .AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task Delete(Guid id, CancellationToken ct)
    {
        var entity = await context.Set<T>()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(T), id);
        }

        context.Set<T>()
            .Remove(entity);

        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<T>> GetAll(bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = context.Set<T>();

        if (withIncludes)
        {
            query = ApplyIncludes(query);
        }

        return await query
            .ToArrayAsync(ct);
    }

    public async Task<T?> GetById(Guid id, bool withIncludes = false, CancellationToken ct = default)
    {
        var collection = await GetWhere(e => e.Id == id, withIncludes, ct);
        return collection.FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<T>> GetByRangeId(IEnumerable<Guid> ids, bool withIncludes = false, CancellationToken ct = default)
    {
        return await GetWhere(e => ids.Contains(e.Id), withIncludes, ct);
    }

    public async Task<IReadOnlyCollection<T>> GetWhere(Expression<Func<T, bool>> predicate, bool withIncludes = false, CancellationToken ct = default)
    {
        var query = context.Set<T>()
            .Where(predicate);

        if (withIncludes)
        {
            query = ApplyIncludes(query);
        }

        return await query.ToArrayAsync(ct);
    }

    public async Task Update(T entity, CancellationToken ct)
    {
        context.Set<T>().Update(entity);
        await context.SaveChangesAsync(ct);
    }

}
