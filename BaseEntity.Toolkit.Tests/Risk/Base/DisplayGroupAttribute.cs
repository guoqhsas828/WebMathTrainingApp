/*
 * DisplayGroupAttribute.cs
 *
 */

using System;

namespace BaseEntity.Risk
{

	/// <summary>
	/// DisplayGroupAttribute
	/// Used when grouping items 
	/// </summary>
	public class DisplayGroupAttribute : Attribute
	{
		/// <summary>
		/// Group Name
		/// </summary>
		public string Name { get; set; }

	}
}
