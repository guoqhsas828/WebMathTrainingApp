using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Database;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public static class ProductUtil
  {
    /// <summary>
    ///   Returns a list of Product Types that implement the IRateResetProduct interface
    /// </summary>
    public static List<Type> GetAllIRateResetProductTypes()
    {
      var productTypes = new List<Type>();

      //foreach (ClassMeta cm in ClassCache.FindAll())
      //{
      //  if ((typeof(IRateResetProduct)).IsAssignableFrom(cm.Type))
      //  {
      //    productTypes.Add(cm.Type);
      //  }
      //}

      return productTypes;
    }

    /// <summary>
    ///   Returns a list of Product Types that implement the ICreditProduct interface
    /// </summary>
    public static List<Type> GetAllCreditProductTypes()
    {
      var productTypes = new List<Type>();

      //foreach (ClassMeta cm in ClassCache.FindAll())
      //{
      //  if ((typeof(ICreditProduct)).IsAssignableFrom(cm.Type))
      //  {
      //    productTypes.Add(cm.Type);
      //  }
      //}

      return productTypes;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static List<string> GetProductTypesUsingCDSCurves()
    {
      List<Type> allCreditProducts = GetAllCreditProductTypes();

      // Not including Stock and StockOption because we don't need a Credit Curve for pricing these trades.
      // Including Loan because Loan trades are priced using CDS Curves similiar to a Bond
      List<string> excludeProdTypes = GetProductTypesUsingLCDSCurves();
      excludeProdTypes.Add("Stock");
      excludeProdTypes.Add("StockOptions");

      var cdsCurvesProdTypes = new List<string>();
      foreach (Type prodType in allCreditProducts)
      {
        string prodTypeName = prodType.Name;
        if (!excludeProdTypes.Contains(prodTypeName))
          cdsCurvesProdTypes.Add(prodType.Name);
      }
      return cdsCurvesProdTypes;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static List<string> GetProductTypesUsingLCDSCurves()
    {
      return new List<string>
      {
        typeof(LCDS).Name,
        //typeof(LCDO).Name,
        typeof(LCDX).Name,
        //typeof(LCDXTranche).Name
      };
    }

    /// <summary>
    ///   Returns whether or not a given product is a standard product.
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public static bool IsStandardProduct(Product p)
    {
      return TradeProductOwnershipResolver.IsStandardProduct(p.GetType());
    }

    /// <summary>
    ///  Returns whether or not a given type is a standard product.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public static bool IsStandardProduct(Type t)
    {
      return TradeProductOwnershipResolver.IsStandardProduct(t);
    }

    /// <summary>
    ///   Returns a list of Product Types that is marked with "StandardProduct" attribute
    /// </summary>
    public static List<Type> GetAllStandardProductTypes()
    {
      return (from cm in ClassCache.FindAll() where cm.Type.IsSubclassOf(typeof(Product)) && IsStandardProduct(cm.Type) select cm.Type).ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public static bool IsIRiskyProduct(Product p)
    {
      return RiskyCounterpartyTradeMeta.Value.Contains(p.GetType());
    }

    /// <summary>
    /// Loads the product with the given ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static Product FindById(long id)
    {
      return (Product)Session.Get(typeof(Product), id);
    }

    /// <summary>
    ///   Get Product with the specified name.
    /// </summary>
    /// <param name="name">The name to find.</param>
    /// <returns>Loan with a matching name.</returns>
    public static Product FindByName(string name)
    {
      IList list = Session.Find("from Product l where l.Name = ?", name, ScalarType.String);
      if (list.Count == 0)
        return null;
      if (list.Count == 1)
        return (Product)list[0];
      throw new DatabaseException(String.Format("Product Name='{0}' not unique", name));
    }


    /// <summary>
    /// Load all products from the session
    /// </summary>
    /// <returns></returns>
    public static IList FindAll()
    {
      return Session.Find("from Product");
    }

    /// <summary> Produce list of strings as a comma separated list </summary>
    public static string AsCommaSeparatedList(IList<string> strings)
    {
      string ret = String.Empty;
      if (strings == null || strings.Count == 0) return ret;
      for (int i = 0; i < strings.Count; i++)
      {
        if (String.IsNullOrWhiteSpace(strings[i]))
          continue;
        if (ret == String.Empty)
        {
          ret = strings[i];
        }
        else
        {
          ret += ", ";
          ret += strings[i];
        }
      }
      return ret;
    }

    /// <summary> Find the first date the outstanding principal changes.</summary>
    public static Dt GetFirstAmortizationDate(IDictionary<Dt, double> amortizationSchedule)
    {
      if (amortizationSchedule == null || amortizationSchedule.Count < 1)
        return Dt.Empty;
      // Get the sorted list of dates.
      Dt[] sortedDates = amortizationSchedule.Keys.ToArray();
      Array.Sort(sortedDates);
      double priorNotional = 1.0;
      foreach (Dt dt in sortedDates)
      {
        if (Math.Abs(amortizationSchedule[dt] - priorNotional) > AmortizationUtil.NotionalTolerance)
          return dt;
        priorNotional = amortizationSchedule[dt];
      }
      return Dt.Empty;
    }

    /// <summary>
    /// Create an amortization schedule that linearly amortizes to zero notional, starting from the date specified.
    /// For a swap leg, the remaining notional is associated with the start of a coupon period.
    /// </summary>
    public static Dictionary<Dt, double> CreateLinearAmortizationSchedule(Toolkit.Products.SwapLeg toolkitSwapLeg, Dt firstAmortizationDate)
    {
      if (toolkitSwapLeg == null || firstAmortizationDate.IsEmpty() || firstAmortizationDate >= toolkitSwapLeg.Maturity) return null;
      Dictionary<Dt, double> amortizationSched = null;
      Schedule sched = toolkitSwapLeg.Schedule;
      int i, startIndex = sched.GetNextCouponIndex(firstAmortizationDate);
      Dt accrStart = sched.GetPeriodStart(startIndex);
      if (accrStart < firstAmortizationDate)
        startIndex++;
      if (startIndex > 0)
        startIndex--;
      if (startIndex >= sched.Count)
        return null;
      double amort = 1.0 / (sched.Count - startIndex);
      double remainingNotional = 1.0;
      amortizationSched = new Dictionary<Dt, double>();
      for (i = startIndex + 1; i < sched.Count; i++)
      {
        remainingNotional -= amort;
        accrStart = sched.GetPeriodStart(i);
        amortizationSched.Add(accrStart, remainingNotional); // Amort. scdedule dates are associated with period start dates.
      }
      return amortizationSched;
    }

    /// <summary>
    /// Create an amortization schedule that linearly amortizes to zero notional, starting from the date specified.
    /// Unlike a swap leg, for a cap-floor, the remaining notional is associated with the payment date.
    /// </summary>
    public static Dictionary<Dt, double> CreateLinearAmortizationSchedule(CapBase toolkitCap, Dt firstAmortizationDate)
    {
      if (toolkitCap == null || firstAmortizationDate.IsEmpty() || firstAmortizationDate > toolkitCap.Maturity) return null;
      Dictionary<Dt, double> amortizationSched = null;
      Schedule sched = toolkitCap.Schedule;
      int i, startIndex = sched.GetNextCouponIndex(firstAmortizationDate);
      if (startIndex < 0 && firstAmortizationDate == toolkitCap.Maturity)
      {
        startIndex = sched.Count - 1;
      }
      else
      {
        Dt accrStart = sched.GetPeriodStart(startIndex);
        if (firstAmortizationDate <= accrStart && startIndex > 0)
          startIndex--;
      }

      if (startIndex <= 0)
        startIndex = 1;
      if (startIndex >= sched.Count)
        return null;
      double amort = 1.0 / (sched.Count - startIndex + 1);
      double remainingNotional = 1.0;
      amortizationSched = new Dictionary<Dt, double>();
      for (i = startIndex; i < sched.Count; i++)
      {
        remainingNotional -= amort;
        Dt payDt = sched.GetPaymentDate(i);
        amortizationSched.Add(payDt, remainingNotional);
      }
      return amortizationSched;
    }

    #region Data

    private static readonly Lazy<HashSet<Type>> RiskyCounterpartyTradeMeta = new Lazy<HashSet<Type>>(LoadIRiskyCounterpartyTradeMeta);

    private static HashSet<Type> LoadIRiskyCounterpartyTradeMeta()
    {
      return new HashSet<Type>((from cm in ClassCache.FindAll() where cm.Type.IsSubclassOf(typeof(Trade)) && TradeUtil.IsIRiskyCounterpartyTrade(cm.Type) select TradeProductOwnershipResolver.GetProductAttribute(cm.Type).ProductType));
    }

    #endregion
  }

}
