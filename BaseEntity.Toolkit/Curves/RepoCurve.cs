
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
  public class RepoCurve : DiscountCurve
  {
    #region Constructors
    ///<summary>
    ///</summary>
    ///<param name="asOf">The repo curve spot date</param>
    ///<param name="rate">Flat repo rate</param>
    ///<param name="dc">The day count convention to calculate interim re-investment proceeds</param>
    ///<param name="ccy">Currency</param>
    public RepoCurve(Dt asOf, double rate, DayCount dc, Currency ccy)
      : base(asOf, rate)
    {
      Ccy = ccy;
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

  }
}
