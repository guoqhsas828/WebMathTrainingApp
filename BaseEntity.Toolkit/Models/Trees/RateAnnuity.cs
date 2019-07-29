// 
// 
// 

using System;
using System.Diagnostics;
using Distribution = BaseEntity.Toolkit.Calibrators.Volatilities.DistributionType;

namespace BaseEntity.Toolkit.Models.Trees
{
  [DebuggerDisplay("{Rate}, {Annuity}")]
  [Serializable]
  public struct RateAnnuity
  {
    #region Data and properties

    /// <summary>
    ///  The annuity, which can be either the swap level, 
    ///  or the discount factor or zero price 
    /// </summary>
    public readonly double Annuity;

    /// <summary>
    /// The floating payment value, <m>V_i = R_i\,A_i</m>,
    /// where <m>R_i</m> is the rate (forward rate or swap rate),
    /// <m>A_i</m> is the corresponding annuity.
    /// </summary>
    public readonly double Value;

    /// <summary>
    /// Gets the rate.
    /// </summary>
    /// <value>The rate.</value>
    /// <remarks>
    /// <para>The rate can be either the forward rate or the swap rate.</para>
    /// 
    /// <para>For forward rates, we assume the rate is scaled by the year
    /// fractions, i.e., <m>R_i = \delta_i\,L_i</m>.</para>
    /// </remarks>
    public double Rate
    {
      get { return Value/Annuity; }
    }

    #endregion

    #region Constructors

    private RateAnnuity(double value, double annuity)
    {
      Annuity = annuity;
      Value = value;
    }

    public static RateAnnuity FromRate(double rate, double annuity)
    {
      return new RateAnnuity(rate*annuity, annuity);
    }

    public static RateAnnuity FromValue(double value, double annuity)
    {
      return new RateAnnuity(value, annuity);
    }

    #endregion
  }
}
