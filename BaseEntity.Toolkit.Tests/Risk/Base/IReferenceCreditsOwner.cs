using System.Collections.Generic;
using BaseEntity.Database;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Implemented by all objects owning reference credits.
  /// </summary>
  public interface IReferenceCreditsOwner
  {
    #region Properties

    /// <summary>
    ///   ObjectId of the Reference Credits Owner 
    /// </summary>
    long ObjectId { get; }

    /// <summary>
    ///   Name of the Reference Credits Owner
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   List of Reference Credits owned by this object
    /// </summary>
    IList<IReferenceCredit> ReferenceCredits { get; }

    #endregion

    #region Methods

    /// <summary>
    ///   Apply Corporate Action
    /// </summary>
    IList<CreditBasketUnderlyingDelta> ApplyCorporateAction(IList<CorporateActionEventItem> corpActionItems);

    /// <summary>
    ///   Unapply Corporate Action
    /// </summary>
    void UnApplyCorporateAction(IList<CorporateActionEventItem> corpActionItems, IList<CreditBasketUnderlyingDelta> underlyingDeltas);

    #endregion
  }
}
