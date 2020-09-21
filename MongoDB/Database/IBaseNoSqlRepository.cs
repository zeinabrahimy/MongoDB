using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Database
{
	public interface IBaseNoSqlRepository<TEntity, TPrimaryKey>
		where TEntity : class
	{
		IMongoCollection<TEntity> Collection { get; }
		FilterDefinitionBuilder<TEntity> Filter { get; }
		ProjectionDefinitionBuilder<TEntity> Project { get; }
		UpdateDefinitionBuilder<TEntity> Updater { get; }

		bool Any();
		bool Any(Expression<Func<TEntity, bool>> filter);
		Task<bool> AnyAsync();
		Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter);
		long Count();
		long Count(Expression<Func<TEntity, bool>> filter);
		Task<long> CountAsync();
		Task<long> CountAsync(Expression<Func<TEntity, bool>> filter);
		void Delete(TPrimaryKey primaryKey);
		void DeleteAll();
		Task DeleteAllAsync();
		Task DeleteAsync(TPrimaryKey primaryKey);
		Task DeleteRangeAsync(FilterDefinition<TEntity> filter);
		void DeleteRange(IEnumerable<TPrimaryKey> primaryKeys);
		Task DeleteRangeAsync(IEnumerable<TPrimaryKey> primaryKeys);
		void Dispose();
		long EstimatedCount();
		long EstimatedCount(EstimatedDocumentCountOptions options);
		Task<long> EstimatedCountAsync();
		Task<long> EstimatedCountAsync(EstimatedDocumentCountOptions options);
		IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size);
		IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending);
		IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> filter, int pageIndex, int size);
		IEnumerable<TEntity> FindAll(Expression<Func<TEntity, object>> order, int pageIndex, int size);
		IEnumerable<TEntity> FindAll(Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending);
		IEnumerable<TEntity> FindAll(int pageIndex, int size);
		Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, object>> order, int pageIndex, int size);
		Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending);
		Task<IEnumerable<TEntity>> FindAllAsync(int pageIndex, int size);
		Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size);
		Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending);
		Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, int pageIndex, int size);
		Task<IEnumerable<TEntity>> FindAsync(FilterDefinition<TEntity> filter);
		TEntity First();
		TEntity First(Expression<Func<TEntity, bool>> filter);
		TEntity First(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order);
		TEntity First(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending);
		TEntity First(FilterDefinition<TEntity> filter);
		Task<TEntity> FirstAsync();
		Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> filter);
		Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order);
		Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending);
		Task<TEntity> FirstAsync(FilterDefinition<TEntity> filter);
		TEntity Get(TPrimaryKey primaryKey);
		IEnumerable<TEntity> GetAll();
		Task<IEnumerable<TEntity>> GetAllAsync();
		Task<TEntity> GetAsync(TPrimaryKey primaryKey);
		void Insert(TEntity entity);
		Task InsertAsync(TEntity entity);
		void InsertRange(IEnumerable<TEntity> entities);
		Task InsertRangeAsync(IEnumerable<TEntity> entities);
		TEntity Last();
		TEntity Last(Expression<Func<TEntity, bool>> filter);
		TEntity Last(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order);
		TEntity Last(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending);
		Task<TEntity> LastAsync();
		Task<TEntity> LastAsync(Expression<Func<TEntity, bool>> filter);
		Task<TEntity> LastAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order);
		Task<TEntity> LastAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending);
		bool Replace(TEntity entity, TPrimaryKey primaryKey);
		Task<bool> ReplaceAsync(TEntity entity, TPrimaryKey primaryKey);
		bool Update(FilterDefinition<TEntity> filter, params UpdateDefinition<TEntity>[] updates);
		bool Update(TPrimaryKey primaryKey, params UpdateDefinition<TEntity>[] updates);
		bool Update(TPrimaryKey primaryKey, UpdateDefinition<TEntity> update);
		bool Update<TField>(FilterDefinition<TEntity> filter, Expression<Func<TEntity, TField>> field, TField value);
		bool Update<TField>(TPrimaryKey primaryKey, Expression<Func<TEntity, TField>> field, TField value);
		Task<bool> UpdateAsync(FilterDefinition<TEntity> filter, params UpdateDefinition<TEntity>[] updates);
		Task<bool> UpdateAsync(TPrimaryKey primaryKey, params UpdateDefinition<TEntity>[] updates);
		Task<bool> UpdateAsync(TPrimaryKey primaryKey, UpdateDefinition<TEntity> update);
		Task<bool> UpdateAsync<TField>(FilterDefinition<TEntity> filter, Expression<Func<TEntity, TField>> field, TField value);
		Task<bool> UpdateAsync<TField>(TPrimaryKey primaryKey, Expression<Func<TEntity, TField>> field, TField value);
		Task<TEntity> FindAndUpdateAsync(FilterDefinition<TEntity> filter, UpdateDefinition<TEntity> update, TPrimaryKey reservedPrimaryKey);
	}
}
