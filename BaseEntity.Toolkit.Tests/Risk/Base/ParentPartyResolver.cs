using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Database;
using NHibernate.Criterion;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Resolve risky party to party parent if available
  /// </summary>
  public class ParentPartyResolver : IRiskyPartyResolver
  {

    #region IRiskyPartyResolver Members

    /// <summary>
    /// Find all valid master agreements for a specific trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public IList<MasterAgreement> GetMasterAgreements(Trade trade)
    {
      var cpty = GetRiskyCounterParty(trade);
      var bk = GetRiskyBookingEntity(trade);
      if (cpty == null || bk == null)
        return new List<MasterAgreement>();
      var masters = new List<MasterAgreement>();
      var productType = trade.Product.GetType().Name;
      foreach (var ma in Find(cpty, bk).Cast<MasterAgreement>())
      {
        if (ma.ProductTypes != null && (ma.ProductTypes.Count == 0 || ma.ProductTypes.Contains(productType)))
        {
          if (ma.CollateralAgreement == null || ma.CollateralAgreement.IsProductTypeSupported(productType))
            masters.Add(ma);
        }
      }
      return masters;
    }

    /// <summary>
    /// Find all valid master agreements for a CP/BE pair/
    /// </summary>
    /// <param name="counterParty"></param>
    /// <param name="bookingEntity"></param>
    /// <returns></returns>
    public IList<MasterAgreement> GetMasterAgreements(LegalEntity counterParty, LegalEntity bookingEntity)
    {
      if (counterParty == null || bookingEntity == null) return new List<MasterAgreement>();
      return Find(counterParty, bookingEntity).Cast<MasterAgreement>().ToList();
    }

    /// <summary>
    /// Find the risky booking entity for a trade, resolves to parent if available.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public LegalEntity GetRiskyBookingEntity(Trade trade)
    {
      return GetRiskyParty(trade?.BookingEntity);
    }

    /// <summary>
    /// Find the risky counterparty for a trade, resolves to parent if available.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public LegalEntity GetRiskyCounterParty(Trade trade)
    {
      return GetRiskyParty(trade?.Counterparty);
    }

    /// <summary>
    /// Find the risky counterparty for a supplied legal entity, resolves to parent if available.
    /// </summary>
    /// <param name="le"></param>
    /// <returns></returns>
    public LegalEntity GetRiskyParty(LegalEntity le)
    {
      if (le == null) return null;
      return le.Parent ?? le;
    }

    #endregion

    #region Private Methods

    private static IList Find(LegalEntity counterParty, LegalEntity bookingEntity)
    {
      var criteria = Session.CreateCriteria(typeof(MasterAgreement));
      criteria.Add(Restrictions.Eq("Counterparty", counterParty));
      criteria.Add(Restrictions.Eq("BookingEntity", bookingEntity));
      return criteria.List();
    }

    #endregion
  }
}
