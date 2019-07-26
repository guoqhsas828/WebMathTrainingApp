/*
 * Cashflow.PartialProxy.cs
 *
 * Copyright (c)   2002-2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  public partial class Cashflow
  {
    
    /// <exclude/>
    /// This class is only for show some useful information in object browser
    public class ScheduleInfo
    {
      #region Properties

      /// <exclude/>
      public Dt Date
      {
        get { return date; }
        set { date = value; }
      }
      /// <exclude/>
      public double Amount
      {
        get { return amount; }
        set { amount = value; }
      }
      /// <exclude/>
      public double Accrual
      {
        get { return accrual; }
        set { accrual = value; }
      }
      /// <exclude/>
      public double Loss
      {
        get { return loss; }
        set { loss = value; }
      }

      #endregion Properties

      #region Methods

      /// <exclude/>
      public override string ToString()
      {
        return string.Format("Dt:{0},Amount:{1},Accrual:{2},Loss:{3}", Date, Amount, Accrual, Loss);
      }

      #endregion Methods

      #region Data

      private double loss;
      private Dt date;
      private double accrual;
      private double amount;

      #endregion Data
    }

    /// <summary>
    /// Enhanced subclass of schedule info to store detailed coupon payment and related rebate information
    /// </summary>
    public class DefaultRecoveryScheduleInfo : ScheduleInfo
    {
      /// <summary>
      /// 
      /// </summary>
      /// <param name="defaultDate"></param>
      /// <param name="accrual"></param>
      /// <param name="creditCurveName"></param>
      public void UpdateRecoveryInfo(Dt defaultDate, double accrual, string creditCurveName)
      {
        var partialCouponScheduleInfo = PartialCoupons.FirstOrDefault(si => si.DefaultDate == defaultDate && si.CreditCurveName == creditCurveName);
        if (partialCouponScheduleInfo == null)
        {
          _partialCoupons.Add(new PartialCouponScheduleInfo { DefaultDate = defaultDate, Accrual = accrual, CreditCurveName = creditCurveName });
        }
        else
        {
          partialCouponScheduleInfo.Accrual += accrual;
        }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="date"></param>
      /// <param name="creditCurveName"></param>
      /// <returns></returns>
      public double? GetPartialCoupon(Dt date, string creditCurveName)
      {
        var partialCouponScheduleInfo = PartialCoupons.FirstOrDefault(si => si.DefaultDate == date);
        return partialCouponScheduleInfo == null ? (double?)null : partialCouponScheduleInfo.Accrual;
      }

      /// <summary>
      /// Partial coupon information
      /// </summary>
      public IList<PartialCouponScheduleInfo> PartialCoupons
      {
        get { return _partialCoupons;}
        set { _partialCoupons = value; }
      }

      private IList<PartialCouponScheduleInfo> _partialCoupons = new List<PartialCouponScheduleInfo>();
    }

    /// <summary>
    ///  Paritial coupons
    /// </summary>
    public class PartialCouponScheduleInfo
    {

      /// <summary>
      /// 
      /// </summary>
      public Dt DefaultDate { get; set; }

      /// <summary>
      /// 
      /// </summary>
      public string CreditCurveName { get; set; }

      /// <summary>
      /// 
      /// </summary>
      public double Accrual { get; set; }
    }

    #region ScheduleInfo

    /// <exclude/>
    /// This class is only for show some useful information in object browser
    public ScheduleInfo[] Schedules
    {
      get
      {
        ScheduleInfo[] infos = new ScheduleInfo[this.Count];
        for (int i = 0; i < Count; i++)
        {
          ScheduleInfo info = new ScheduleInfo();
          info.Date = this.GetDt(i);
          info.Amount = this.GetAmount(i);
          info.Accrual = this.GetAccrued(i);
          info.Loss = this.GetDefaultAmount(i);
          infos[i] = info;
        }
        return infos;
      }
    }

    #endregion ScheduleInfo

    #region DefaultPayment

    /// <summary>
    ///   For public use only
    ///   <preliminary/>
    /// </summary>
    /// 
    /// <remarks>
    ///   <para><c>DefaultPayment.Date</c> is the date when the default payments are made,
    ///    not the date when the default occurs.</para>
    /// 
    ///   <para><c>DefaultPayment.Accrual</c> is the accrual up to the default date carried
    ///    to the payment date to settle.</para>
    /// 
    ///   <para><c>DefaultPayment.Amount</c> is recovery received on the payment date.</para>
    /// 
    ///   <para><c>DefaultPayment.Loss</c> is protection paid on the payment date.</para>
    /// </remarks>
    /// 
    /// <exclude/>
    public ScheduleInfo DefaultPayment
    {
      get { return defaultPayment_; }
      set { defaultPayment_ = value; }
    }

    private ScheduleInfo defaultPayment_;

    #endregion DefaultPayment

    /// <summary>
    /// 
    /// </summary>
    public DefaultRecoveryScheduleInfo RecoveryScheduleInfo { get; set; }

    #region Extra

    public void AddMaturityPayment(
      double amount, double accrued, double coupon, double defaultAmount)
    {
      int idx = Count - 1;
      if (idx >= 0)
      {
        Set(idx, GetAmount(idx) + amount,
            GetAccrued(idx) + accrued, GetCoupon(idx) + coupon,
            GetDefaultAmount(idx) + defaultAmount);
      }
      return;
    } // 

    /// <summary>
    ///   For public use only.
    /// </summary>
    /// <param name="cPtr">cPtr</param>
    /// <param name="cMemoryOwn">cMemoryOwn</param>
    /// <returns>Cashflow</returns>
    /// <exclude/>
    public static Cashflow Create(IntPtr cPtr, bool cMemoryOwn)
    {
      return new Cashflow(cPtr, cMemoryOwn);
    }
    #endregion Extra

  }
}