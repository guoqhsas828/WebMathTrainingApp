/*
 * PrepaymentModel.cs
 *
 *   2008. All rights reserved.
 *
 */

using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// This will be the class for generating prepayment vectors using a variety of prepayment methodologies
  /// Currently, it only implements global CPR
  /// </summary>
	public class PrepaymentModel : BaseEntityObject
  {
    #region Data
    private Schedule schedDates_;
    private double [] prepaymentRates_;
    #endregion // Data

    #region Properties
    /// <summary>
    /// Array of Prepayment Rates
    /// </summary>
    public double[] PrepaymentRates
    {
      get { return prepaymentRates_; }
    }
    #endregion // Properties

    #region Constructors
    /// <summary>
    /// Constructor for an empty prepayment model
    /// </summary>
    public PrepaymentModel()
    {
      schedDates_ = null;
      prepaymentRates_ = null;
    }
    /// <summary>
    /// Constructor for a prepayment model using a Global CPR
    /// </summary>
    /// <param name="cpr">Global Constant Prepayment Rate (CPR)</param>
    /// <param name="security">Asset-backed security</param>
    public PrepaymentModel(double cpr, ABS security)
    {
      schedDates_ = security.PaySchedule;
      prepaymentRates_ = prepaymentSpeed(cpr, security.Effective, security.PaySchedule, security.Frequency);
    }


    /// <summary>
    /// Clone class object
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      PrepaymentModel obj = (PrepaymentModel)base.Clone();
      obj.schedDates_ = this.schedDates_; //need Clone() to be added to Schedule
      obj.prepaymentRates_ = CloneUtil.Clone(this.prepaymentRates_);
      return obj;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Method for generating a vector of prepayment rates off a global CPR assumption
    /// </summary>
    /// <param name="cpr">Global Constant Prepayment Rate (CPR)</param>
    /// <param name="asOfDate">"As of" or pricing date</param>
    /// <param name="sched">Schedule of payment dates</param>
    /// <param name="freq">Frequency</param>
    /// <returns>Array of prepayment speeds on each payment period</returns>
    private double [] prepaymentSpeed(double cpr, Dt asOfDate, Schedule sched, Frequency freq)
    {
/***************
      double [] pCPR = new double[sched.Count+1] ;

      if (cpr > 0) {
        // MARTIN:
        // Dt.Fraction may not be the best choice here.  The error is small.
        // However, prepayments DO apply to principal and should not be dependent on day counts
        pCPR[1] = cpr * Dt.Fraction(asOfDate,sched.GetPaymentDate(0), dCount);
        int frq = 0;
        switch (freq) {
          case Frequency.Monthly:
            frq = 12;
            break;
          case Frequency.Quarterly:
            frq = 4;
            break;
          case Frequency.SemiAnnual:
            frq = 2;
            break;
          case Frequency.Annual:
            frq = 1;
            break;
        }

        for (int t = 2; t < sched.Count ; t++)
        {
          pCPR[t] = cpr / frq;
        }
        // MARTIN:
        // Dt.Fraction may not be the best choice here.  The error is small.
        // However, prepayments DO apply to principal and should not be dependent on day counts
        pCPR[sched.Count] = cpr * Dt.Fraction(sched.GetPaymentDate(sched.Count - 2), sched.GetPaymentDate(sched.Count - 1), dCount);
      }
****************/
      double [] pCPR = new double[sched.Count] ;

      if (cpr > 0)
      {
        int frq = 0;
        switch (freq)
        {
          case Frequency.Monthly:
            frq = 12;
            break;
          case Frequency.Quarterly:
            frq = 4;
            break;
          case Frequency.SemiAnnual:
            frq = 2;
            break;
          case Frequency.Annual:
            frq = 1;
            break;
        }
        for (int t = 0; t < sched.Count; t++)
        {
          if (t == 0)
          {
            pCPR[t] = cpr * Dt.Fraction(asOfDate, sched.GetPaymentDate(t), DayCount.None);
          }
          else if (t > 0 && t < sched.Count - 1)
          {
            pCPR[t] = cpr / frq;
          }
          else
          {
            pCPR[t] = cpr * Dt.Fraction(sched.GetPaymentDate(t - 1), sched.GetPaymentDate(t), DayCount.None);
          }
        }
      }
      return pCPR ;
    }
    #endregion // Methods
  }
}
