﻿using System;
using System.Collections.Generic;
using System.Text;

namespace consumer
{
	public class WharehouseItem
	{
		public int ItemId { get; set; }
		public Dictionary<string , string> TraceKeys { get; set; }

	}
}