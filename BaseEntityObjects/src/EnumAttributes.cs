/*
 * EnumAttributes.cs -
 *
 * Copyright (c) WebMathTraining 2005-2008. All rights reserved.
 *
 */

using System;

namespace BaseEntity.Shared
{

	/// <summary>
	///  Indicates that enum values should be sorted aphabetically.
	/// </summary>
	[AttributeUsage(AttributeTargets.Enum)]
	public class AlphabeticalOrderEnumAttribute : Attribute
	{
	}

} // namespace WebMathTraining.Shared

