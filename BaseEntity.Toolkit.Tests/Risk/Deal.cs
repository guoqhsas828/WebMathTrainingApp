using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Ties together a list of Trades
  /// </summary>
  [Serializable]
  [Entity(EntityId = 921, AuditPolicy = AuditPolicy.History, Key = new[] {"Name"}, Description = "List of trades combined as a single structure")]
  public class Deal : AuditedObject
  {
    #region Properties

    /// <summary>
    /// Unique name identifying this deal
    /// </summary>
    [StringProperty(MaxLength = 64)]
    public string Name { get; set; }

    /// <summary>
    /// Description of this deal
    /// </summary>
    [StringProperty(MaxLength = 1024)]
    public string Description { get; set; }

    /// <summary>
    /// Plug-in Defining this deal, GUI, Business Logic, etc.
    /// </summary>
    [StringProperty(MaxLength = 128, AllowNullValue = true)]
    public string DealType { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (string.IsNullOrEmpty(Name))
      {
        InvalidValue.AddError(errors, this, "Name", "Deal name cannot be blank.");
      }
    }

    #endregion
  }
}