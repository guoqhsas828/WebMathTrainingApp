
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Curves
{
///<summary>
/// Repo Curve
/// Contains a term structure of repo rates
///</summary>
  [Serializable]
  public class RateQuoteCurve : Curve
  {
    #region Constructors
    ///<summary>
    /// Default constructor
    ///</summary>
    ///<param name="asOf">The repo curve spot date</param>
    public RateQuoteCurve(Dt asOf)
      : base(asOf)
    {
    }

    ///<summary>
    ///</summary>
    ///<param name="asOf">The repo curve spot date</param>
    ///<param name="rate">Flat repo rate</param>
    ///<param name="dc">The day count convention to calculate interim re-investment proceeds</param>
    ///<param name="ccy">Currency</param>
    public RateQuoteCurve(Dt asOf, double rate, DayCount dc, Currency ccy)
      : base(asOf, rate)
    {
      RateQuoteDayCount = dc;
      Ccy = ccy;
    }

  ///<summary>
    ///</summary>
    ///<param name="asOf">The repo curve spot date</param>
    ///<param name="interpMethod">The method to interpolate repo rates</param>
    ///<param name="extrapMethod">The method to extrapolate repo rates</param>
    ///<param name="dc">The day count convention to calculate interim re-investment proceeds</param>
    ///<param name="ccy">The currency</param>
    public RateQuoteCurve(Dt asOf, InterpMethod interpMethod, ExtrapMethod extrapMethod, DayCount dc, Currency ccy)
      : base(asOf)
    {
      Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      RateQuoteDayCount = dc;
      Ccy = ccy;
      RateQuoteFrequency = Frequency.None;
    }

    #endregion

    #region Methods
    ///<summary>
    /// Check if there are any repo rates on the curve
    ///</summary>
    ///<returns></returns>
    public bool IsEmpty()
    {
      return !(Points.Count > 0);
    }

    #endregion
    #region Properties
    ///<summary>
    /// Day count convention to calculate interim re-investment proceeds
    ///</summary>
    public DayCount RateQuoteDayCount { get; set; }

    ///<summary>
    /// Frequency of rate quote 
    ///</summary>
    public Frequency RateQuoteFrequency { get; set; }
    #endregion Properties
  }
}
