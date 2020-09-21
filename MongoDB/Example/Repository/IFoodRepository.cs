using System.Collections.Generic;
using System.Threading.Tasks;

namespace Example.Repository
{
	public interface IFoodRepository
  {
    Task<IEnumerable<DataModel.Food>> Search(Dto.FoodSearchRequest request);
    Task Insert(IEnumerable<DataModel.Food> foods);
    Task Insert(DataModel.Food food);
    Task<DataModel.Food> GetAsync(int foodId);
    Task Update(DataModel.Food food);

  }
}
