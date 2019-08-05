/*
 * MasterAgreementType.cs
 *
 */

using System;
using BaseEntity.Metadata;


namespace BaseEntity.Risk
{

	/// <summary>
  ///   MasterAgreementType
	/// </summary>
  [Serializable]
	[Entity(EntityId=126, Description = "User defined classification for Master Agreements", AuditPolicy=AuditPolicy.History)]
  public class MasterAgreementType : AuditedObject
	{
    #region Constructors

		/// <summary>
		///   Default constructor for internal use
		/// </summary>
    protected MasterAgreementType()
    {
    }

    #endregion Constructors

		#region Properties

		/// <summary>
    ///   MasterAgreementType name
		/// </summary>
		[StringProperty(MaxLength=32, IsKey=true)]
		public string Name
		{
			get { return name_; }
			set { name_ = value; }
		}

		/// <summary>
    ///   MasterAgreementType description
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

  } // class MasterAgreementType
} 
