// 
// Copyright (c) WebMathTraining 2002-2012. All rights reserved.
// 

using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Represents a collection of strings within the configuration file
  /// </summary>
  public class StringElementCollection : ConfigurationElementCollection
  {
    /// <summary>
    /// The create new element.
    /// </summary>
    /// <returns>
    /// </returns>
    protected override ConfigurationElement CreateNewElement()
    {
      return new StringElement();
    }

    /// <summary>
    /// The get element key.
    /// </summary>
    /// <param name="element">
    /// The element.
    /// </param>
    /// <returns>
    /// The get element key.
    /// </returns>
    protected override object GetElementKey(ConfigurationElement element)
    {
      return ((StringElement)element).Value;
    }

		/// <summary>
		/// Add an element
		/// </summary>
		/// <param name="s"></param>
		public void Add(StringElement s)
		{
			BaseAdd(s);
		}

		/// <summary>
		/// Add a collection of elements
		/// </summary>
		/// <param name="collection"></param>
		public void AddRange(IEnumerable<string> collection)
		{
			if (collection == null)
				return;

			foreach(var s in collection)
				BaseAdd(new StringElement{Value=s});
		}

    /// <summary>
    /// Gets Items.
    /// </summary>
    public List<string> Items
    {
      get { return this.Cast<StringElement>().Select(i => i.Value).ToList(); }
    }
  }
}