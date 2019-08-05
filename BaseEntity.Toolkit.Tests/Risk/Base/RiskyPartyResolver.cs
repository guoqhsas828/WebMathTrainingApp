using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Database;
using NHibernate.Criterion;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public class RiskyPartyResolver : IRiskyPartyResolver
  {
    /// <summary>
    /// Return the legal entity subject to counter-party risk for a supplied trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public LegalEntity GetRiskyCounterParty(Trade trade)
    {
      return GetRiskyParty(trade?.Counterparty);
    }

    /// <summary>
    /// Return the legal entity subject to booking-entity risk for a supplied trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public LegalEntity GetRiskyBookingEntity(Trade trade)
    {
      return GetRiskyParty(trade?.BookingEntity);
    }

    /// <summary>
    /// Find the risky counterparty for a supplied legal entity, resolves to parent if available.
    /// </summary>
    /// <param name="le"></param>
    /// <returns></returns>
    public LegalEntity GetRiskyParty(LegalEntity le)
    {
      return le;
    }

    /// <summary>
    /// Return a list of valid master agreements for a supplied trade.
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
          if(ma.CollateralAgreement == null || ma.CollateralAgreement.IsProductTypeSupported(productType))
            masters.Add(ma);
        }
      }
      return masters;
    }

    /// <summary>
    /// Return a list of valid master agreements for a counterParty/bookingEntity pair.
    /// </summary>
    /// <param name="counterParty"></param>
    /// <param name="bookingEntity"></param>
    /// <returns></returns>
    public IList<MasterAgreement> GetMasterAgreements(LegalEntity counterParty, LegalEntity bookingEntity)
    {
      if (counterParty == null || bookingEntity == null)
        return new List<MasterAgreement>();

      return Find(counterParty, bookingEntity).Cast<MasterAgreement>().ToList();
    }

    private static IList Find(LegalEntity counterParty, LegalEntity bookingEntity)
    {
      var criteria = Session.CreateCriteria(typeof(MasterAgreement));
      criteria.Add(Restrictions.Eq("Counterparty", counterParty));
      criteria.Add(Restrictions.Eq("BookingEntity", bookingEntity));
      return criteria.List();
    }
  }
}