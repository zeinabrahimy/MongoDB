using System;
using System.Collections.Generic;
using System.Text;

namespace Example.Dto
{
	public class FoodSearchRequest
	{
		public IEnumerable<int> FoodIds { get; set; }

	}
}
