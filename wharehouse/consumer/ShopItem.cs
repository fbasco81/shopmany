using System;
using System.Collections.Generic;
using System.Text;

namespace consumer
{
	public class ShopItem
	{
		public int id { get; set; }
		public string name { get; set; }
		public string description { get; set; }

		public double price { get; set; }

	}

	public class ShopItemContainer
	{
		public IEnumerable<ShopItem> items { get; set; }
	}
}
