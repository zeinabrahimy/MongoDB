using System;
using System.Collections.Generic;
using System.Text;

namespace Example.DataModel
{
	public class Food
	{
		public int Id { get; set; }
		public Guid Guid { get; set; }
		public int FoodId { get; set; }
		public string Name { get; set; }
	}
}
