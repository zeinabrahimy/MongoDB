using Database.Transactional;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Transactions;

namespace Database.Repository
{
	internal class MongoDBRepository<TEntity, TPrimaryKey> : IDisposable, IBaseNoSqlRepository<TEntity, TPrimaryKey> where TEntity : class
	{
		#region MongoSpecific
		private IMongoCollection<TEntity> _collection;
		private string _connectionString;
		private string _collectionName;
		private IMongoClient _mongoClient;
		private MongoUrl _mongoUrl;
		private readonly string _primarykeyName;
		private IClientSessionHandle _session;
		private bool isInReplicaSet;

		/// <summary>
		/// with custom settings
		/// </summary>
		/// <param name="dataContext">INoSqlEntityDatabaseInfo<TEntity></param>
		public MongoDBRepository(INoSqlEntityDatabaseInfo<TEntity> dataContext)
		{
			_connectionString = dataContext.EntityConnectionString;
			_collectionName = BsonClassMap.GetRegisteredClassMaps().Where(x => x.ClassType == (typeof(TEntity))).First().Discriminator;
			_primarykeyName = BsonClassMap.GetRegisteredClassMaps().Where(x => x.ClassType == (typeof(TEntity))).First().IdMemberMap.ElementName;

			_mongoClient = new MongoClient(_connectionString);
			_session = _mongoClient.StartSession();
			var db = _session.Client.GetDatabase(dataContext.DatabaseName);
			_collection = GetCollectionIfExists(db, _collectionName);

			if (_collection == null)
			{
				try
				{
					db.CreateCollection(_collectionName);
				}
				catch
				{

				}
				_collection = db.GetCollection<TEntity>(_collectionName);
			}
		}

		private void CheckIsInReplicaSet()
		{
			isInReplicaSet = false;
			var db = new MongoClient(_connectionString).GetDatabase("admin");

			var command = new BsonDocumentCommand<BsonDocument>(new BsonDocument() { { "getCmdLineOpts", 1 } });

			var res = db.RunCommand<BsonDocument>(command);
			BsonElement replicationElement;
			bool hasReplicationElement = res.GetElement("parsed").Value.ToBsonDocument().TryGetElement("replication", out replicationElement);
			if (hasReplicationElement && replicationElement != null)
			{
				BsonElement replSetNameElement;
				bool hasReplSetNameElement = replicationElement.Value.ToBsonDocument().TryGetElement("replSetName", out replSetNameElement);
				if (hasReplSetNameElement && replSetNameElement != null)
				{
					isInReplicaSet = true;
					_connectionString += (_connectionString.Contains("?") ? "&" : "?") + "replicaSet=" + replSetNameElement.Value.AsString;
				}
			}
		}

		~MongoDBRepository()
		{
			Dispose();
		}

		private IMongoCollection<TEntity> GetCollectionIfExists(IMongoDatabase database, string collectionName)
		{
			var command = $"{{ collStats: \"{collectionName}\", scale: 1 }}";
			try
			{
				database.RunCommand<BsonDocument>(command);
				return database.GetCollection<TEntity>(collectionName);
			}
			catch (MongoCommandException e) when (e.ErrorMessage.EndsWith("not found."))
			{
				return null;
			}
		}

		public void Dispose()
		{
			if (_session != null && _session.IsInTransaction)
			{
				_session.AbortTransaction();
			}
		}

		/// <summary>
		/// mongo collection
		/// </summary>
		public IMongoCollection<TEntity> Collection
		{
			get
			{
				return _collection;
			}
		}

		/// <summary>
		/// filter for collection
		/// </summary>
		public FilterDefinitionBuilder<TEntity> Filter
		{
			get
			{
				return Builders<TEntity>.Filter;
			}
		}

		/// <summary>
		/// projector for collection
		/// </summary>
		public ProjectionDefinitionBuilder<TEntity> Project
		{
			get
			{
				return Builders<TEntity>.Projection;
			}
		}

		/// <summary>
		/// updater for collection
		/// </summary>
		public UpdateDefinitionBuilder<TEntity> Updater
		{
			get
			{
				return Builders<TEntity>.Update;
			}
		}

		#endregion

		#region CRUD

		#region Delete

		public void Delete(TPrimaryKey primaryKey)
		{
			TransactionPolicy();
			Retry(() =>
			{
				return Collection.DeleteOne(_session, Filter.Eq(_primarykeyName, primaryKey)).IsAcknowledged;
			});
		}



		public void DeleteRange(IEnumerable<TPrimaryKey> primaryKeys)
		{
			TransactionPolicy();
			Retry(() =>
			{
				return Collection.DeleteMany(_session, Filter.In(_primarykeyName, primaryKeys)).IsAcknowledged;
			});
		}

		public virtual void DeleteAll()
		{
			TransactionPolicy();
			Retry(() =>
			{
				return Collection.DeleteMany(_session, Filter.Empty).IsAcknowledged;
			});
		}

		public async Task DeleteAsync(TPrimaryKey primaryKey)
		{
			TransactionPolicy();
			await RetryAsync(() =>
			{
				return Collection.DeleteOneAsync(_session, Filter.Eq(_primarykeyName, primaryKey));
			}).ConfigureAwait(false);
		}

		public async Task DeleteRangeAsync(FilterDefinition<TEntity> filter)
		{
			await RetryAsync(() =>
			{
				return Collection.DeleteManyAsync(_session, filter);
			}).ConfigureAwait(false);
		}

		public async Task DeleteRangeAsync(IEnumerable<TPrimaryKey> primaryKeys)
		{
			await RetryAsync(() =>
			{
				return Collection.DeleteManyAsync(Filter.In(_primarykeyName, primaryKeys));
			}).ConfigureAwait(false);
		}

		public async Task DeleteAllAsync()
		{
			TransactionPolicy();
			await RetryAsync(() =>
			{
				return Collection.DeleteManyAsync(_session, Filter.Empty);
			}).ConfigureAwait(false);
		}

		#endregion

		#region Find

		public IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> filter, int pageIndex, int size)
		{
			return Find(filter, i => _primarykeyName, pageIndex, size);
		}

		public IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size)
		{
			return Find(filter, order, pageIndex, size, true);
		}

		public virtual IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending)
		{
			return Retry(() =>
			{
				var query = Collection.Find(_session, filter).Skip(pageIndex * size).Limit(size);
				return (isDescending ? query.SortByDescending(order) : query.SortBy(order)).ToEnumerable();
			});
		}

		public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, int pageIndex, int size)
		{
			return await FindAsync(filter, i => _primarykeyName, pageIndex, size);
		}

		public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size)
		{
			return await FindAsync(filter, order, pageIndex, size, true);
		}

		public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending)
		{
			return await RetryAsync(() =>
			{
				var query = Collection.Find(_session, filter).Skip(pageIndex * size).Limit(size);
				if (isDescending)
					return query.SortByDescending(order).ToListAsync();
				else
					return query.SortBy(order).ToListAsync();
			}).ConfigureAwait(false);
		}

		public async Task<IEnumerable<TEntity>> FindAsync(FilterDefinition<TEntity> filter)
		{
			return await RetryAsync(() =>
			{
				var query = Collection.Find(_session, filter);
				return query.ToListAsync();
			}).ConfigureAwait(false);
		}

		#endregion

		#region FindAll

		public IEnumerable<TEntity> FindAll(int pageIndex, int size)
		{
			return FindAll(i => _primarykeyName, pageIndex, size);
		}

		public IEnumerable<TEntity> FindAll(Expression<Func<TEntity, object>> order, int pageIndex, int size)
		{
			return FindAll(order, pageIndex, size, true);
		}

		public virtual IEnumerable<TEntity> FindAll(Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending)
		{
			return Retry(() =>
			{
				var query = Collection.Find(_session, Filter.Empty).Skip(pageIndex * size).Limit(size);
				return (isDescending ? query.SortByDescending(order) : query.SortBy(order)).ToEnumerable();
			});
		}

		public async Task<IEnumerable<TEntity>> FindAllAsync(int pageIndex, int size)
		{
			return await FindAllAsync(i => _primarykeyName, pageIndex, size);
		}

		public async Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, object>> order, int pageIndex, int size)
		{
			return await FindAllAsync(order, pageIndex, size, true);
		}

		public virtual async Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, object>> order, int pageIndex, int size, bool isDescending)
		{
			return await RetryAsync(() =>
			{
				var query = Collection.Find(_session, Filter.Empty).Skip(pageIndex * size).Limit(size);
				if (isDescending)
					return query.SortByDescending(order).ToListAsync();
				else
					return query.SortBy(order).ToListAsync();
			}).ConfigureAwait(false);
		}

		#endregion

		#region First

		public TEntity First()
		{
			return Collection.Find(_session, Filter.Empty).FirstOrDefault();
		}

		public TEntity First(FilterDefinition<TEntity> filter)
		{
			return Collection.Find(_session, filter).FirstOrDefault();
		}

		public TEntity First(Expression<Func<TEntity, bool>> filter)
		{
			return First(filter, i => _primarykeyName);
		}

		public TEntity First(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order)
		{
			return First(filter, order, false);
		}

		public TEntity First(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending)
		{
			return Find(filter, order, 0, 1, isDescending).FirstOrDefault();
		}

		#endregion

		#region First Async

		public async Task<TEntity> FirstAsync()
		{
			return (await RetryAsync(() =>
			{
				return Collection.FindAsync(_session, Filter.Empty);
			}).ConfigureAwait(false)).FirstOrDefault();
		}

		public async Task<TEntity> FirstAsync(FilterDefinition<TEntity> filter)
		{
			return (await RetryAsync(() =>
			{
				return Collection.FindAsync(_session, filter);
			}).ConfigureAwait(false)).FirstOrDefault();
		}

		public async Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> filter)
		{
			return await FirstAsync(filter, i => _primarykeyName);
		}

		public async Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order)
		{
			return await FirstAsync(filter, order, false);
		}

		public async Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending)
		{
			return (await FindAsync(filter, order, 0, 1, isDescending)).FirstOrDefault();
		}

		#endregion

		#region Get

		public virtual TEntity Get(TPrimaryKey primaryKey)
		{
			return Retry(() =>
			{
				return Collection.Find(_session, Filter.Eq(_primarykeyName, primaryKey)).FirstOrDefault();
			});
		}

		public async Task<TEntity> GetAsync(TPrimaryKey primaryKey)
		{
			return (await RetryAsync(() =>
			{
				return Collection.Find(_session, Filter.Eq(_primarykeyName, primaryKey)).ToListAsync();
			}).ConfigureAwait(false)).FirstOrDefault();
		}

		public virtual IEnumerable<TEntity> GetAll()
		{
			return Retry(() =>
			{
				return Collection.Find(_session, Filter.Empty).ToEnumerable();
			});
		}

		public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
		{
			return await RetryAsync(() =>
			{
				return Collection.Find(_session, Filter.Empty).ToListAsync();
			}).ConfigureAwait(false);
		}

		#endregion Get

		#region Insert

		public virtual void Insert(TEntity entity)
		{
			TransactionPolicy();
			Retry(() =>
			{
				Collection.InsertOne(_session, entity);
				return true;
			});
		}

		public async Task InsertAsync(TEntity entity)
		{
			TransactionPolicy();
			await RetryAsyncAction(() =>
			{
				return Collection.InsertOneAsync(_session, entity);
			}).ConfigureAwait(false);
		}

		public virtual void InsertRange(IEnumerable<TEntity> entities)
		{
			TransactionPolicy();
			Retry(() =>
			{
				Collection.InsertMany(_session, entities);
				return true;
			});
		}

		public async Task InsertRangeAsync(IEnumerable<TEntity> entities)
		{
			TransactionPolicy();
			await RetryAsyncAction(() =>
			{
				return Collection.InsertManyAsync(_session, entities);
			}).ConfigureAwait(false);
		}

		#endregion

		#region Last

		public TEntity Last()
		{
			return FindAll(i => _primarykeyName, 0, 1, true).FirstOrDefault();
		}

		public TEntity Last(Expression<Func<TEntity, bool>> filter)
		{
			return Last(filter, i => _primarykeyName);
		}

		public TEntity Last(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order)
		{
			return Last(filter, order, false);
		}

		public TEntity Last(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending)
		{
			return First(filter, order, !isDescending);
		}

		#endregion Last

		#region Last Async

		public async Task<TEntity> LastAsync()
		{
			return (await FindAllAsync(i => _primarykeyName, 0, 1, true).ConfigureAwait(false)).FirstOrDefault();
		}

		public async Task<TEntity> LastAsync(Expression<Func<TEntity, bool>> filter)
		{
			return await LastAsync(filter, i => _primarykeyName);
		}

		public async Task<TEntity> LastAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order)
		{
			return await LastAsync(filter, order, false);
		}

		public async Task<TEntity> LastAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<TEntity, object>> order, bool isDescending)
		{
			return await FirstAsync(filter, order, !isDescending);
		}

		#endregion

		#region Replace

		public virtual bool Replace(TEntity entity, TPrimaryKey primaryKey)
		{
			TransactionPolicy();
			return Retry(() =>
			{
				return Collection.ReplaceOne(_session, Filter.Eq(_primarykeyName, primaryKey), entity).IsAcknowledged;
			});
		}

		public virtual async Task<bool> ReplaceAsync(TEntity entity, TPrimaryKey primaryKey)
		{
			TransactionPolicy();
			return (await RetryAsync(() =>
			{
				return Collection.ReplaceOneAsync(_session, Filter.Eq(_primarykeyName, primaryKey), entity);
			}).ConfigureAwait(false)).IsAcknowledged;
		}

		#endregion

		#region Update

		public bool Update<TField>(TPrimaryKey primaryKey, Expression<Func<TEntity, TField>> field, TField value)
		{
			return Update(primaryKey, Updater.Set(field, value));
		}

		public virtual bool Update(TPrimaryKey primaryKey, UpdateDefinition<TEntity> update)
		{
			TransactionPolicy();
			return Retry(() =>
			{
				return Collection.UpdateOne(_session, Filter.Eq(_primarykeyName, primaryKey), update).IsAcknowledged;
			});
		}

		public virtual bool Update(TPrimaryKey primaryKey, params UpdateDefinition<TEntity>[] updates)
		{
			TransactionPolicy();
			return Retry(() =>
			{
				return Collection.UpdateOne(_session, Filter.Eq(_primarykeyName, primaryKey), Updater.Combine(updates)).IsAcknowledged;
			});
		}

		public bool Update<TField>(FilterDefinition<TEntity> filter, Expression<Func<TEntity, TField>> field, TField value)
		{
			return Update(filter, Updater.Set(field, value));
		}

		public bool Update(FilterDefinition<TEntity> filter, params UpdateDefinition<TEntity>[] updates)
		{
			TransactionPolicy();
			return Retry(() =>
			{
				return Collection.UpdateMany(_session, filter, Updater.Combine(updates)/*.CurrentDate()*/).IsAcknowledged;
			});
		}

		#endregion

		#region Update Async

		public async Task<bool> UpdateAsync<TField>(TPrimaryKey primaryKey, Expression<Func<TEntity, TField>> field, TField value)
		{
			return await UpdateAsync(primaryKey, Updater.Set(field, value));
		}

		public virtual async Task<bool> UpdateAsync(TPrimaryKey primaryKey, UpdateDefinition<TEntity> update)
		{
			TransactionPolicy();
			return (await RetryAsync(() =>
			{
				return Collection.UpdateOneAsync(_session, Filter.Eq(_primarykeyName, primaryKey), update);
			}).ConfigureAwait(false)).IsAcknowledged;
		}

		public virtual async Task<bool> UpdateAsync(TPrimaryKey primaryKey, params UpdateDefinition<TEntity>[] updates)
		{
			TransactionPolicy();
			return (await RetryAsync(() =>
			{
				return Collection.UpdateOneAsync(_session, Filter.Eq(_primarykeyName, primaryKey), Updater.Combine(updates));
			}).ConfigureAwait(false)).IsAcknowledged;
		}

		public async Task<bool> UpdateAsync<TField>(FilterDefinition<TEntity> filter, Expression<Func<TEntity, TField>> field, TField value)
		{
			return await UpdateAsync(filter, Updater.Set(field, value));
		}

		public async Task<bool> UpdateAsync(FilterDefinition<TEntity> filter, params UpdateDefinition<TEntity>[] updates)
		{
			TransactionPolicy();
			return (await RetryAsync(() =>
			{
				return Collection.UpdateManyAsync(_session, filter, Updater.Combine(updates));
			}).ConfigureAwait(false)).IsAcknowledged;
		}

		public async Task<TEntity> FindAndUpdateAsync(FilterDefinition<TEntity> filter, UpdateDefinition<TEntity> update, TPrimaryKey reservedPrimaryKey)
		{
			TransactionPolicy();

			return (await RetryAsync(() =>
			{
				return Collection.FindOneAndUpdateAsync(_session, filter, update.SetOnInsert(_primarykeyName, reservedPrimaryKey), new FindOneAndUpdateOptions<TEntity>
				{
					IsUpsert = true,
					ReturnDocument = ReturnDocument.After
				});

			}).ConfigureAwait(false));
		}

		#endregion

		#endregion

		#region Utils

		#region Any
		public bool Any()
		{
			return Retry(() =>
			{
				return First(Filter.Empty) != null;
			});
		}

		public bool Any(Expression<Func<TEntity, bool>> filter)
		{
			return Retry(() =>
			{
				return First(filter) != null;
			});
		}

		public async Task<bool> AnyAsync()
		{
			return (await RetryAsync(() =>
			{
				return FirstAsync(Filter.Empty);
			}).ConfigureAwait(false)) != null;
		}

		public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter)
		{
			return (await RetryAsync(() =>
			{
				return FirstAsync(filter);
			}).ConfigureAwait(false)) != null;
		}

		#endregion

		#region Count

		public long Count()
		{
			return Retry(() =>
			{
				return Collection.CountDocuments(_session, Filter.Empty);
			});
		}

		public long Count(Expression<Func<TEntity, bool>> filter)
		{
			return Retry(() =>
			{
				return Collection.CountDocuments(_session, filter);
			});
		}

		public async Task<long> CountAsync()
		{
			return await RetryAsync(() =>
			{
				return Collection.CountDocumentsAsync(_session, Filter.Empty);
			}).ConfigureAwait(false);
		}

		public async Task<long> CountAsync(Expression<Func<TEntity, bool>> filter)
		{
			return await Retry(() =>
			{
				return Collection.CountDocumentsAsync(_session, filter);
			}).ConfigureAwait(false);
		}

		#endregion

		#region EstimatedCount

		public long EstimatedCount()
		{
			return Retry(() =>
			{
				return Collection.EstimatedDocumentCount();
			});
		}

		public long EstimatedCount(EstimatedDocumentCountOptions options)
		{
			return Retry(() =>
			{
				return Collection.EstimatedDocumentCount(options);
			});
		}

		public async Task<long> EstimatedCountAsync()
		{
			return await RetryAsync(() =>
			{
				return Collection.EstimatedDocumentCountAsync();
			}).ConfigureAwait(false);
		}

		public async Task<long> EstimatedCountAsync(EstimatedDocumentCountOptions options)
		{
			return await RetryAsync(() =>
			{
				return Collection.EstimatedDocumentCountAsync(options);
			}).ConfigureAwait(false);
		}

		#endregion

		#endregion

		#region RetryPolicy

		protected virtual TResult Retry<TResult>(Func<TResult> action)
		{
			return RetryPolicy
					.Handle<MongoConnectionException>(i => i.InnerException.GetType() == typeof(IOException) ||
																								 i.InnerException.GetType() == typeof(SocketException))
					.Retry(3)
					.Execute(action);
		}

		protected virtual Task<TResult> RetryAsync<TResult>(Func<Task<TResult>> action)
		{
			return RetryPolicy
					.Handle<MongoConnectionException>(i => i.InnerException.GetType() == typeof(IOException) ||
																								 i.InnerException.GetType() == typeof(SocketException))
					 .RetryAsync(3)
					 .ExecuteAsync(action);
		}

		protected virtual Task RetryAsyncAction(Func<Task> action)
		{
			return RetryPolicy
					.Handle<MongoConnectionException>(i => i.InnerException.GetType() == typeof(IOException) ||
																								 i.InnerException.GetType() == typeof(SocketException))
					 .RetryAsync(3)
					 .ExecuteAsync(action);
		}

		private bool addedToCurrentTransactionScope = false;

		#endregion
		private void TransactionPolicy()
		{
			if (isInReplicaSet && Transaction.Current != null)
			{
				if (!_session.IsInTransaction)
					_session.StartTransaction();
				if (!addedToCurrentTransactionScope)
				{
					MongoDBResourceManager txRm = new MongoDBResourceManager(_session);
					Transaction.Current.EnlistVolatile(txRm, EnlistmentOptions.None);
					addedToCurrentTransactionScope = true;
				}
			}
		}
	}
}