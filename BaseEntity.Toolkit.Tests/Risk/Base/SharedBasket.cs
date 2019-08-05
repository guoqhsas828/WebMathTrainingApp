using System;
using System.Collections.Generic;
using BaseEntity.Metadata;
using BaseEntity.Risk;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 302, AuditPolicy = AuditPolicy.History)]
  public class SharedBasket : AuditedObject, IReferenceCreditsOwner
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    protected SharedBasket()
    {
      Underlyings = new List<CreditBasketUnderlying>();
    }

    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 80, IsKey = true)]
    public string Name { get; set; }

    /// <summary>
    ///   Description of basket
    /// </summary>
    [StringProperty(MaxLength = 80)]
    public string Description { get; set; }

    /// <summary>
    ///   
    /// </summary>
    [ComponentCollectionProperty(Clazz = typeof(CreditBasketUnderlying), IndexColumn = "Idx")]
    public IList<CreditBasketUnderlying> Underlyings { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      var clone = (SharedBasket) base.Clone();
      clone.Underlyings = CloneUtil.CloneToGenericList(Underlyings);
      return clone;
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      // Validate for 100% Basket Weights
      string errorMsg;
      if (!CreditBasketUtil.TryValidateBasketWeights(Underlyings, out errorMsg))
        InvalidValue.AddError(errors, this, "Underlyings", errorMsg);
    }

    #endregion

    #region IReferenceCreditsOwner Members

    /// <summary>
    ///   ObjectId of the Reference Credits Owner 
    /// </summary>
    long IReferenceCreditsOwner.ObjectId
    {
      get { return this.ObjectId; }
    }

    /// <summary>
    ///   Name of the Reference Credits Owner
    /// </summary>
    string IReferenceCreditsOwner.Name
    {
      get { return this.Name; }
    }

    /// <summary>
    ///   List of Reference Credits owned by this object
    /// </summary>
    IList<IReferenceCredit> IReferenceCreditsOwner.ReferenceCredits
    {
      get { return CreditBasketUtil.GetReferenceCredits(Underlyings); }
    }

    /// <summary>
    ///   Apply Corporate Action
    /// </summary>
    IList<CreditBasketUnderlyingDelta> IReferenceCreditsOwner.ApplyCorporateAction(IList<CorporateActionEventItem> corpActionItems)
    {
      return CreditBasketUtil.ApplyCorporateAction(Underlyings, corpActionItems);
    }

    /// <summary>
    ///   Unapply Corporate Action
    /// </summary>
    void IReferenceCreditsOwner.UnApplyCorporateAction(IList<CorporateActionEventItem> corpActionItems, IList<CreditBasketUnderlyingDelta> underlyingDeltas)
    {
      CreditBasketUtil.UnApplyCorporateAction(Underlyings, corpActionItems, underlyingDeltas);
    }

    #endregion
  }
}
