﻿using System;

namespace Nevar
{
	[Flags]
	public enum NodeFlags : byte
	{
		None = 0,
		Data = 1,
		PageRef = 2,
	}
}