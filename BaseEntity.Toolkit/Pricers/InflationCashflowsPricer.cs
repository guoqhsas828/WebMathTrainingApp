//
// 
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   Inflation linked (or real) cashflows pricer
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.InflationCashflows" />
  /// </remarks>
  /// <seealso cref="InflationCashflows"/>
  /// <seealso cref="NominalCashflows"/>
  /// <seealso cref="NominalCashflowsPricer"/>
  [Serializable]
  public class InflationCashflowsPricer : NominalCashflowsPricer 
  {
    private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(InflationCashflowsPricer));

		#region Constructors

    /// <summary>
    ///   Inflation cashflow pricer constructor
    /// </summary>
    public InflationCashflowsPricer(NominalCashflows product,
																 Dt asOf,
																 Dt settle,
																 DiscountCurve discountCurve,
																 InflationIndex inflationIndex, 
                                 InflationCurve inflationCurve,
                                 IndexationMethod indexationMethod,
                                 Tenor resetLag)
			: base(product, asOf, settle, discountCurve)
		{
      ReferenceCurve = (CalibratedCurve)inflationCurve;
      InflationIndex = inflationIndex;
      IndexationMethod = indexationMethod;
      ResetLag = resetLag;
		}

		#endregion Constructors

    /// <summary>
    /// Inflation Curve
    /// </summary>
    public CalibratedCurve ReferenceCurve { get; set; }

    /// <summary>
    /// Inflation Index
    /// </summary>
    public InflationIndex InflationIndex { get; set; }

    /// <summary>
    /// Indexation method
    /// </summary>
    public IndexationMethod IndexationMethod { get; set; }

    /// <summary>
    /// Reset Lag
    /// </summary>
    public Tenor ResetLag { get; set; }

    #region Methods
    /// <summary>
    /// Calculate price of cashflows
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="settle"></param>
    /// <param name="maturity"></param>
    /// <param name="includeFee"></param>
    /// <param name="includeProtection"></param>
    /// <returns></returns>
    protected override double CalcPrice(Dt asOf, Dt settle, Dt maturity,
                              bool includeFee, bool includeProtection)
    {
      DiscountCurve dc = DiscountCurve;
      int start = FindStartIndex(settle, 0);
      int stop = FindStopIndex(maturity, start);

      IRateProjector rateProjector = new InflationForwardCalculator(AsOf, InflationIndex, (InflationCurve)ReferenceCurve, IndexationMethod)
      {
        DiscountCurve = dc,
        ResetLag = this.ResetLag
      };

      var resetDate = !Product.Effective.IsEmpty() ? Product.Effective : AsOf;
      var fixSched0 = rateProjector.GetFixingSchedule(Dt.Empty, resetDate, resetDate, resetDate);
      double i0 = rateProjector.Fixing(fixSched0).Forward;

      double pv = 0;

      // Loop backwards for accuracy
      for (int i = start; i < stop; ++i)
      {
        DateNode current = this[i];
        double df = (dc == null ? 1.0 : dc.DiscountFactor(current.Date));
        
        // Inflation growth
        var fixSched = rateProjector.GetFixingSchedule(Dt.Empty, current.Date, current.Date, current.Date);
        double iT = rateProjector.Fixing(fixSched).Forward;
        double inflationGrowth = iT / i0;

        if (includeFee && current.Fee != 0.0)
          pv += (current.Fee + current.Accrued) * df * inflationGrowth;
        if (includeProtection && current.Loss != 0.0)
          pv += current.Loss * df * inflationGrowth;
      }
      if (dc != null)
        pv /= dc.DiscountFactor(asOf);

      return pv;
    }
    #endregion
  }
}
