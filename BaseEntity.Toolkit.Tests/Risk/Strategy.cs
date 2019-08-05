using System;
using System.Collections;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{

	/// <summary>
	///   Strategy for a trade. Each trade may belong to a single strategy
	/// </summary>
	///
	/// <remarks>
	///   <para>Strategies are unique groupings of trades for reporting purposes.</para>
	///
	///   <para>Strategies may also be part of another parent strategy.</para>
	///
	///   <para>For retrieving and constructing Strategies, use the
	///   <see cref="StrategyUtil">Strategy Util</see>
	///   class.</para>
	/// </remarks>
	///
  [Serializable]
	[Entity(EntityId = 403, AuditPolicy = AuditPolicy.History, Description = "Strategies are unique groupings of trades for reporting purposes")]
	public class Strategy : AuditedObject
	{
		#region Properties

		/// <summary>
		///   Strategy name
		/// </summary>
		[StringProperty(MaxLength=32, IsKey=true)]
		public string Name
		{
			get { return name_; }
			set { name_ = value; }
		}

		/// <summary>
		///   Strategy description
		/// </summary>
		[StringProperty(MaxLength=128)]
		public string Description
		{
			get { return description_; }
			set { description_ = value; }
		}

		#endregion

    #region Data

		private string name_;
		private string description_;

		#endregion

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="System.String" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
      return Name;
    }
	} // class Strategy
} 
