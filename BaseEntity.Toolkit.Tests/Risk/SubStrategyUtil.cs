/*
 * StrategyUtil.cs
 *
 *
 */

using System;
using System.Collections;
using BaseEntity.Database;
using BaseEntity.Metadata;


namespace BaseEntity.Risk
{

	/// <summary>
	///   Utility methods for <see cref="SubStrategy">SubStrategy class</see>.
	/// </summary>
  public class SubStrategyUtil : ObjectFactory<SubStrategy>
  {
		#region Query

		/// <summary>
		///   Get sub strategy by name
		/// </summary>
		///
		/// <param name="name">Name of sub strategy</param>
		///
		/// <returns>Sub strategy matching specified name</returns>
		///
		public static SubStrategy FindByName(string name)
		{
			IList list = Session.Find("from SubStrategy a where a.Name = ?", name, ScalarType.String);
			if (list.Count == 0)
				return null;
			else if (list.Count == 1)
				return (SubStrategy)list[0];
			else
				throw new DatabaseException("Invalid substrategy: " + name);
    }


		/// <summary>
		///   Get all sub strategies
		/// </summary>
		///
		/// <returns>List of all sub strategies</returns>
		///
		public static IList FindAll()
		{
			return Session.Find("from SubStrategy");
		}

		#endregion Query

    #region Factory

		/// <summary>
		///   Create a new sub strategy
		/// </summary>
		///
		/// <returns>Created sub strategy</returns>
		///
		public static SubStrategy CreateInstance()
		{
			return Create();
		}


		/// <summary>
		///   Create a new sub strategy
		/// </summary>
		///
		/// <param name="parent">Parent strategy</param>
		/// <param name="name">Name of sub strategy</param>
		/// <param name="description">Description of strategy</param>
		///
		/// <returns>Created Strategy</returns>
		///
		public static SubStrategy CreateInstance(Strategy parent, string name, string description)
		{
			SubStrategy substrategy = Create();

			substrategy.Name = name;
			substrategy.Description = description;

			return substrategy;
		}

		#endregion

  } // class SubStrategyUtil
}  
