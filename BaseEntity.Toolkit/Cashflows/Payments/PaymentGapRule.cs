// 
// PaymentGapRule.cs
//  -2014. All rights reserved.
// 
using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  #region Payment lag rule
  
  ///<summary>
  /// Payment date lagging from period end
  ///</summary>
  [Serializable]
  public class PayLagRule
  {
    #region Constructor
    ///<summary>
    /// Constructor
    ///</summary>
    ///<param name="lagDays">Days of payment lagging</param>
    ///<param name="businessFlag">True if lagging days follow business calendar</param>
    public PayLagRule(int lagDays, bool businessFlag)
    {
      paymentLagDays_ = Math.Abs(lagDays);
      paymentLagBusinessFlag_ = businessFlag;
    }

    #endregion

    #region Methods

    ///<summary>
    /// Calculate the actual payment date with payment lag
    ///</summary>
    ///<param name="periodEnd">Period end</param>
    ///<param name="lagDays">Days of payment lag</param>
    ///<param name="businessFlag">Days in business calendar or regular calendar</param>
    ///<param name="roll">BD Convention</param>
    ///<param name="cal">Calendar</param>
    ///<returns>Payment date</returns>
    public static Dt CalcPaymentDate(Dt periodEnd, int lagDays, bool businessFlag, BDConvention roll, Calendar cal)
    {
      if (businessFlag)
        return Dt.AddDays(periodEnd, lagDays, cal);

      return Dt.Roll(Dt.Add(periodEnd, lagDays), roll, cal);
    }

    #endregion

    #region Properties

    ///<summary>
    /// Pay lag days after period end
    ///</summary>
    public int PaymentLagDays
    {
      get { return paymentLagDays_; }
    }

    ///<summary>
    /// Flag to indicate if the pay lag days is applied by business days
    ///</summary>
    public bool PaymentLagBusinessFlag
    {
      get { return paymentLagBusinessFlag_; }
    }

    #endregion

    #region Data

    private readonly int paymentLagDays_;
    private readonly bool paymentLagBusinessFlag_;

    #endregion

  }

  #endregion

  #region Ex-div rule

  /// <summary>
  /// Ex-div details
  /// </summary>
  [Serializable]
  public class ExDivRule
  {
    ///<summary>
    /// Constructor
    ///</summary>
    ///<param name="exDivDays">Ex-div Days before next coupon date</param>
    ///<param name="exDivBusinessFlag">Flag to indicate if the ex-div days is applied by business days</param>
    ///<param name="bondType">Bond type</param>
    public ExDivRule(int exDivDays, bool exDivBusinessFlag, BondType bondType)
    {
      if (bondType == BondType.UKGilt)
      {
        exDivDays_ = 6;
        exDivBusinessFlag_ = true;
      }
      else if (bondType == BondType.AUSGovt)
      {
        exDivDays_ = 7;
        exDivBusinessFlag_ = false;
      }
      else
      {
        exDivDays_ = Math.Abs(exDivDays);
        exDivBusinessFlag_ = exDivBusinessFlag;
      }
    }

    ///<summary>
    /// Constructor without picking ex-div parameters from bond type
    ///</summary>
    ///<param name="exDivDays">Ex-div Days before next coupon date</param>
    ///<param name="exDivBusinessFlag">Flag to indicate if the ex-div days is applied by business days</param>
    public ExDivRule(int exDivDays, bool exDivBusinessFlag)
      : this(exDivDays, exDivBusinessFlag, BaseEntity.Toolkit.Base.BondType.None)
    {
    }

    ///<summary>
    /// Ex-div Days before next coupon date
    ///</summary>
    public int ExDivDays
    {
      get { return exDivDays_; }
    }

    ///<summary>
    /// Flag to indicate if the ex-div days is applied by business days
    ///</summary>
    public bool ExDivBusinessFlag
    {
      get { return exDivBusinessFlag_; }
    }

    private readonly int exDivDays_;
    private readonly bool exDivBusinessFlag_;

  }

  #endregion

}
