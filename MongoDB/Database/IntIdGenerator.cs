using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Polly;
using Polly.Retry;
using System;

namespace Database
{
	/// <summary>
	/// Base class for id generator based on integer values.
	/// </summary>
	public class IntIdGenerator<TEntity> : IIdGenerator
	{
		#region Fields
		private readonly string m_idCollectionName = "IDs";
		private readonly string _collectionName;
		#endregion

		#region Constructors
		/// <summary>
		/// Initializes a new instance of the <see cref="MongoDBIntIdGenerator.IntIdGeneratorBase"/> class.
		/// </summary>
		/// <param name="idCollectionName">Identifier collection name.</param>
		public IntIdGenerator(string collectionName)
		{
			_collectionName = collectionName;
		}
		#endregion

		#region Methods

		/// <summary>
		/// Tests whether an Id is empty.
		/// </summary>
		/// <param name="id">The Id.</param>
		/// <returns>True if the Id is empty.</returns>
		public bool IsEmpty(object id)
		{
			return (int)id == 0;
		}

		/// <summary>
		/// Generates an Id for a document.
		/// </summary>
		/// <param name="container">The container of the document (will be a MongoCollection when called from the C# driver).</param>
		/// <param name="document">The document.</param>
		/// <returns>An Id.</returns>
		public object GenerateId(object container, object document)
		{
			var idSequenceCollection = ((IMongoCollection<TEntity>)container).Database
				.GetCollection<IdEntity>(m_idCollectionName);

			idSequenceCollection.Indexes.CreateOne(new CreateIndexModel<IdEntity>(new IndexKeysDefinitionBuilder<IdEntity>().Ascending(x => x.CollectionName), new CreateIndexOptions { Unique = true }));
			var filterDefinition = Builders<IdEntity>.Filter.Eq(x => x.CollectionName, _collectionName);
			var updateDefinition = Builders<IdEntity>.Update.Inc("IdValue", 1);


			return Retry(() =>
			{
				return idSequenceCollection.FindOneAndUpdate(
					filterDefinition,
					updateDefinition,
					new FindOneAndUpdateOptions<IdEntity>
					{
						IsUpsert = true,
						ReturnDocument = ReturnDocument.After
					});
			}).IdValue;
		}

		#endregion

		protected virtual TResult Retry<TResult>(Func<TResult> action)
		{
			return RetryPolicy
					.Handle<MongoCommandException>()
					.Retry(3)
					.Execute(action);
		}

		#region Entity
		protected class IdEntity
		{
			[BsonId(IdGenerator = typeof(MongoDB.Bson.Serialization.IdGenerators.ObjectIdGenerator))]
			public ObjectId Id { get; set; }
			public string CollectionName { get; set; }
			public int IdValue { get; set; }
		}
		#endregion
	}


}
