using System;
using System.Collections.Generic;
using System.Text;
using Database;
using System.Threading.Tasks;
using MongoDB.Driver;
using System.Linq;

namespace Example.DataAccess
{
	internal class FoodRepository : Repository.IFoodRepository
	{
		IBaseNoSqlRepository<DataModel.Food, int> baseRepository;

		public FoodRepository(IBaseNoSqlRepository<DataModel.Food, int> baseRepository)
		{
			this.baseRepository = baseRepository;
			baseRepository.Collection.Indexes.CreateOne(new CreateIndexModel<DataModel.Food>(new IndexKeysDefinitionBuilder<DataModel.Food>().Ascending(x => x.FoodId), new CreateIndexOptions { Unique = true }));
		}

		#region IQuestionRepository Implementation

		public async Task<IEnumerable<DataModel.Food>> Search(Dto.FoodSearchRequest request)
		{
			FilterDefinition<DataModel.Food> filter = null;

			if (request != null && request.FoodIds != null && request.FoodIds.Count() > 0)
				filter = baseRepository.Filter.In(f => f.FoodId, request.FoodIds);

			if (filter != null)
				return await baseRepository.FindAsync(filter).ConfigureAwait(false);

			return await baseRepository.GetAllAsync().ConfigureAwait(false);
		}

		public async Task Insert(IEnumerable<DataModel.Food> foods)
		{
			try
			{
				await baseRepository.InsertRangeAsync(foods).ConfigureAwait(false);
			}
			catch { }
		}

		public async Task<DataModel.Food> GetAsync(int foodId)
		{
			var filter = baseRepository.Filter.Eq(f => f.FoodId, foodId);
			return (await baseRepository.FindAsync(filter).ConfigureAwait(false)).FirstOrDefault();
		}

		public async Task Insert(DataModel.Food food)
		{
			await baseRepository.InsertAsync(food).ConfigureAwait(false);
		}

		public async Task Update(DataModel.Food food)
		{
			await baseRepository.ReplaceAsync(food, food.Id).ConfigureAwait(false);
		}

		#endregion
	}
}
