// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows.RateProjectors
{
  /// <summary>
  ///  Calculate the asset prices from a price curve.
  /// </summary>
  [Serializable]
  internal class CurveBasedPriceCalculator : IPriceCalculator
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CurveBasedPriceCalculator"/> class.
    /// </summary>
    /// <param name="priceCurve">The price curve.</param>
    /// <param name="historicalPriceObservations">The historical price observations.</param>
    /// <exception cref="System.NullReferenceException">price curve cannot be null</exception>
    public CurveBasedPriceCalculator(
      Curve priceCurve,
      RateResets historicalPriceObservations)
    {
      if (priceCurve == null)
      {
        throw new NullReferenceException("price curve cannot be null");
      }
      AsOf = priceCurve.AsOf;
      PriceCurve = priceCurve;
      HistoricalObservations = historicalPriceObservations;
    }

    #region Methods

    /// <summary>
    /// Fixes the price at the specified reset date.
    /// </summary>
    /// <param name="resetDate">The reset date.</param>
    /// <param name="referenceDt">The reference date</param>
    /// <returns>The price and reset state.</returns>
    /// <exception cref="ToolkitException"></exception>
    public Fixing GetPrice(Dt resetDate, Dt referenceDt)
    {
      Dt asOf = AsOf, fwdStart = referenceDt;

      RateResetState state;
      var price = RateResetUtil.FindRate(resetDate, asOf,
        HistoricalObservations, UseAsOfResets, out state);
      switch (state)
      {
        case RateResetState.IsProjected:
          price = PriceCurve.Interpolate(resetDate);
          break;
        case RateResetState.Missing:
          if (!RateResetUtil.ProjectMissingRateReset(resetDate, asOf, fwdStart))
          {
            throw new MissingFixingException(String.Format(
              "Historical price not found on {0}", resetDate));
          }
          state = RateResetState.IsProjected;
          price = PriceCurve.Interpolate(resetDate);
          break;
      }
      return new Fixing { Forward = price, RateResetState = state };
    }

    /// <summary>
    /// Gets the fixing schedule.
    /// </summary>
    /// <param name="resetDate">The reset date.</param>
    /// <returns>FixingSchedule.</returns>
    internal FixingSchedule GetFixingSchedule(Dt resetDate)
    {
      return new ForwardPriceFixingSchedule { ResetDate = resetDate };
    }

    /// <summary>
    ///  Try get the historical prices from price curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>RateResets.</returns>
    internal static RateResets GetHistoricalPrices(CalibratedCurve curve)
    {
      return curve?.ReferenceIndex?.HistoricalObservations;
    }

    /// <summary>
    ///  Try get the name of the index from the price curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>System.String.</returns>
    private static string GetIndexName(CalibratedCurve curve)
    {
      if (curve == null) return null;
      if (curve.ReferenceIndex == null) return curve.Name;
      return curve.ReferenceIndex.IndexName ?? curve.Name;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The date before which all the prices are historical
    /// </summary>
    /// <value>As of.</value>
    public Dt AsOf { get; set; }

    /// <summary>
    /// If provided reset matches AsOf then use it
    /// </summary>
    /// <value><c>true</c> if [use as of resets]; otherwise, <c>false</c>.</value>
    public bool UseAsOfResets { get; set; }

    /// <summary>
    /// Gets the price curve.
    /// </summary>
    /// <value>The price curve.</value>
    public Curve PriceCurve { get; private set; }

    #endregion

    #region IRateProjector Implementation

    /// <summary>
    /// Name of Index
    /// </summary>
    /// <value>The name of the index.</value>
    public string IndexName
    {
      get { return _indexName ?? GetIndexName(PriceCurve as CalibratedCurve); }
      set { _indexName = value; }
    }

    /// <summary>
    /// Historical index fixings
    /// </summary>
    /// <value>The historical observations.</value>
    public RateResets HistoricalObservations
    {
      get
      {
        return _historicalPrices ??
          GetHistoricalPrices(PriceCurve as CalibratedCurve);
      }
      set { _historicalPrices = value; }
    }

    /// <summary>
    /// Fixing on reset
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns>Fixing.</returns>
    public Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var fs = fixingSchedule as ForwardPriceFixingSchedule;
      return fs != null
        ? GetPrice(fs.ResetDate, fs.ReferenceDate)
        : GetPrice(fixingSchedule.ResetDate, fixingSchedule.ResetDate);
    }

    /// <summary>
    /// Initialize fixing schedule
    /// </summary>
    /// <param name="prevPayDt">Previous payment date</param>
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="payDt">Payment date</param>
    /// <returns>Fixing schedule</returns>
    public FixingSchedule GetFixingSchedule(Dt prevPayDt,
      Dt periodStart, Dt periodEnd, Dt payDt)
    {
      return GetFixingSchedule(periodEnd);
    }

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns>Reset info for each component of the fixing</returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public List<RateResets.ResetInfo> GetResetInfo(
      FixingSchedule schedule)
    {
      throw new NotImplementedException();
    }

    #endregion

    #region Data Members

    /// <summary>
    /// The index name
    /// </summary>
    private string _indexName;

    /// <summary>
    /// The historical prices
    /// </summary>
    private RateResets _historicalPrices;

    #endregion
  }
}
