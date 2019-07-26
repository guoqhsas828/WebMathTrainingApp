/*
 * CashflowStream.cs
 *
 * Copyright (c)   2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  ///  CashflowStream as a simple wrapper of Cashflow.
  ///  This removes the need for a separate C++ implementation.
  ///  It intends to be temporary before we get rid of CashflowStream class.
  /// </summary>
  /// <exclude/>
  [Serializable,ReadOnly(true)]
  [Obsolete("To be replaced by Cashflow class")]
  public partial class CashflowStream
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public CashflowStream()
    {
      cf_ = new Cashflow();
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As-of date</param>
    public CashflowStream(Dt asOf)
    {
      cf_ = new Cashflow(asOf);
    }

    #endregion Constructors

    #region Properties

    /// <summary>Effective (accrual start) date</summary>
    [Category("Base")]
    public Dt Effective
    {
      get { return cf_.Effective; }
      set { cf_.Effective = value; }
    }

    /// <summary>Currency of payments</summary>
    [Category("Base")]
    public Currency Currency
    {
      get { return cf_.Currency; }
      set { cf_.Currency = value; }
    }

    /// <summary>Currency of default payments</summary>
    [Category("Base")]
    public Currency DfltCurrency
    {
      get { return cf_.DefaultCurrency; }
      set { cf_.DefaultCurrency = value; }
    }

    /// <summary>Daycount for interest accrual</summary>
    [Category("Base")]
    public DayCount DayCount
    {
      get { return cf_.DayCount; }
      set { cf_.DayCount = value; }
    }

    /// <summary>Accrued is paid on the event of default</summary>
    [Category("Base")]
    public bool AccruedPaidOnDefault
    {
      get { return cf_.AccruedPaidOnDefault; }
      set { cf_.AccruedPaidOnDefault = value; }
    }

    /// <summary>Accrued on the event of default includes default date</summary>
    [Category("Base")]
    public double MaturityPaymentIfDefault
    {
      get { return cf_.GetMaturityPaymentIfDefault(); }
      set { cf_.SetMaturityPaymentIfDefault(value); }
    }

    /// <summary>Accrued on the event of default includes default date</summary>
    [Category("Base")]
    public bool AccruedIncludingDefaultDate
    {
      get { return cf_.AccruedIncludingDefaultDate; }
      set { cf_.AccruedIncludingDefaultDate = value; }
    }

    /// <summary>Number of cashflow entries</summary>
    [Category("Base")]
    public int Count
    {
      get { return cf_.Count; }
    }

    /// <summary>
    /// Default losses
    /// </summary>
    public List<Pair<Dt, double>> DefaultLosses
    {
      get { return defaultLosses_; }
    }

    /// <summary>
    /// Wrapped cashflow
    /// </summary>
    public Cashflow WrappedCashflow
    {
      get{ return cf_;}
    }

    #endregion 

    #region Wrappers

    /// <summary>
    /// Get date
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public Dt GetDate(int i)
    {
      return cf_.GetDt(i);
    }

    /// <summary>
    /// Get principal
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public double GetPrincipal(int i)
    {
      return cf_.GetAmount(i);
    }

    /// <summary>
    /// Get accrual
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public double GetAccrual(int i)
    {
      // In Cashflow this field is combined with accrued interest.
      return 0.0;
    }

    /// <summary>
    /// Get interest
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public double GetInterest(int i)
    {
      return cf_.GetAccrued(i);
    }

    /// <summary>
    /// Get notional
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public double GetNotional(int i)
    {
      return cf_.GetPrincipalAt(i);
    }

    /// <summary>
    /// Get notional
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public double GetNotional(Dt date)
    {
      int n = Count;
      if (n == 0) return 0.0;
      if (GetDate(0) >= date)
        return GetNotional(0);
      for (int i = 1; i < n; ++i)
      {
        if (GetDate(i) > date)
          return GetNotional(i - 1);
      }
      return GetNotional(n-1);
    }

    /// <summary>
    /// To string
    /// </summary>
    public override string ToString()
    {
      return cf_.ToString();
    }

    /// <summary>
    /// Add cashflow
    /// </summary>
    /// <param name="date"></param>
    /// <param name="principal"></param>
    public void Add(Dt date, double principal)
    {
      cf_.Add(MissingDate, MissingDate, date, MissingValue,
              principal, MissingValue, MissingValue, MissingValue);
    }

    /// <summary>
    /// Add cashflow
    /// </summary>
    /// <param name="date"></param>
    /// <param name="principal"></param>
    /// <param name="interest"></param>
    /// <param name="notional"></param>
    public void Add(Dt date, double principal, double interest, double notional)
    {
      cf_.Add(MissingDate, MissingDate, date, MissingValue,
              notional, principal, interest, MissingValue, MissingValue);
    }

    /// <summary>
    /// Add cashflow
    /// </summary>
    /// <param name="date"></param>
    /// <param name="principal"></param>
    /// <param name="accrual"></param>
    /// <param name="interest"></param>
    /// <param name="notional"></param>
    public void Add(Dt date, double principal, double accrual, double interest, double notional)
    {
      // We combine accrual and interest together.
      cf_.Add(MissingDate, MissingDate, date, MissingValue,
              notional, principal, accrual + interest, MissingValue, MissingValue);
    }

    /// <summary>
    /// Set cashflow
    /// </summary>
    /// <param name="index"></param>
    /// <param name="principal"></param>
    /// <param name="accrual"></param>
    /// <param name="interest"></param>
    /// <param name="notional"></param>
    public void Set(int index, double principal, double accrual, double interest, double notional)
    {
      cf_.Set(index, notional, principal, accrual+interest, MissingValue, MissingValue);
    }

    /// <summary>
    /// Clone
    /// </summary>
    public CashflowStream clone()
    {
      var cfs = new CashflowStream();
      cfs.cf_ = cf_.clone();
      return cfs;
    }

    /// <summary>
    /// Clear cashflow stream
    /// </summary>
    public void Clear()
    {
      cf_.Clear();
    }

    /// <summary>
    /// Copy cashflow
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="source"></param>
    public static void Copy(CashflowStream destination, CashflowStream source)
    {
      Cashflow.Copy(destination.cf_, source.cf_);
    }

    /// <summary>
    /// Set upfront fee
    /// </summary>
    /// <param name="value"></param>
    public void SetUpfrontFee(double value)
    {
      upfront_ = value;
    }

    /// <summary>
    /// Get upfront fee
    /// </summary>
    /// <returns></returns>
    public double GetUpfrontFee()
    {
      return upfront_;
    }

    /// <summary>
    /// Set fee settlement
    /// </summary>
    /// <param name="value"></param>
    public void SetFeeSettle(Dt value)
    {
      feeSettle_ = value;
    }

    /// <summary>
    /// Get fee  settlement
    /// </summary>
    /// <returns></returns>
    public Dt GetFeeSettle()
    {
      return feeSettle_;
    }

    #endregion

    #region Types
    /// <exclude/>
    /// This class is only for show some useful information in object browser
    [Serializable]
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
      public double Interest
      {
        get { return interest; }
        set { interest = value; }
      }
      /// <exclude/>
      public double Accrual
      {
        get { return accrual; }
        set { accrual = value; }
      }
      /// <exclude/>
      public double Principal
      {
        get { return principal; }
        set { principal = value; }
      }
      /// <exclude/>
      public double Notional
      {
        get { return notional; }
        set { notional = value; }
      }
      #endregion

      #region private memeber
      private Dt date;
      private double principal;
      private double accrual;
      private double interest;
      private double notional;
      #endregion
      /// <exclude/>
      public override string ToString()
      {
        return string.Format("Dt:{0},Principal:{1},Accrual:{2},Interest:{3},Notional:{4}",
                             Date, Principal, Accrual, Interest, Notional);
      }
    }

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
          info.Date = this.GetDate(i);
          info.Principal = this.GetPrincipal(i);
          info.Accrual = this.GetAccrual(i);
          info.Interest = this.GetInterest(i);
          info.Notional = this.GetNotional(i);
          infos[i] = info;
        }
        return infos;
      }
    }
    #endregion

    #region Methods
    /// <summary>
    ///   Return cashflow contents as a DataTable
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Retrieve the contents of a credit-contingent cashflow stream</para>
    ///
    ///   <para>The output table consists of six columns:</para>
    ///   <list type="bullet">
    ///     <item><description>Payment date</description></item>
    ///     <item><description>Principal payment</description></item>
    ///     <item><description>Interest accrual</description></item>
    ///     <item><description>Interest payment</description></item>
    ///     <item><description>Notional payment</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <returns>DataTable representation of cashflows</returns>
    ///
    public System.Data.DataTable ToDataTable()
    {
      System.Data.DataTable dataTable = new System.Data.DataTable("CashflowStream table");
      dataTable.Columns.Add(new System.Data.DataColumn("Date", typeof(Dt)));
      dataTable.Columns.Add(new System.Data.DataColumn("Principal", typeof(double)));
      dataTable.Columns.Add(new System.Data.DataColumn("Accrual", typeof(double)));
      dataTable.Columns.Add(new System.Data.DataColumn("Interest", typeof(double)));
      dataTable.Columns.Add(new System.Data.DataColumn("Notional", typeof(double)));

      for (int i = 0; i < Count; i++)
      {
        System.Data.DataRow row = dataTable.NewRow();
        row["Date"] = GetDate(i);
        row["Principal"] = GetPrincipal(i);
        row["Accrual"] = GetAccrual(i);
        row["Interest"] = GetInterest(i);
        row["Notional"] = GetNotional(i);
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }


    /// <summary>
    ///   Return cashflow contents as a DataTable after rescaling them by a factor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Retrieve the contents of a credit-contingent cashflow stream</para>
    ///
    ///   <para>The output table consists of six columns:</para>
    ///   <list type="bullet">
    ///     <item><description>Payment date</description></item>
    ///     <item><description>Principal payment</description></item>
    ///     <item><description>Interest accrual</description></item>
    ///     <item><description>Interest payment</description></item>
    ///     <item><description>Notional payment</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <returns>DataTable representation of cashflows</returns>
    ///
    public System.Data.DataTable ToDataTable(double factor)
    {
      System.Data.DataTable dataTable = new System.Data.DataTable("CashflowStream table");
      dataTable.Columns.Add(new System.Data.DataColumn("Date", typeof(Dt)));
      dataTable.Columns.Add(new System.Data.DataColumn("Principal", typeof(double)));
      dataTable.Columns.Add(new System.Data.DataColumn("Accrual", typeof(double)));
      dataTable.Columns.Add(new System.Data.DataColumn("Interest", typeof(double)));
      dataTable.Columns.Add(new System.Data.DataColumn("Notional", typeof(double)));

      for (int i = 0; i < Count; i++)
      {
        System.Data.DataRow row = dataTable.NewRow();
        row["Date"] = GetDate(i);
        row["Principal"] = GetPrincipal(i) * factor;
        row["Accrual"] = GetAccrual(i) * factor;
        row["Interest"] = GetInterest(i) * factor;
        row["Notional"] = GetNotional(i) * factor;
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }
    #endregion

    #region Data
    private const double MissingValue = double.NaN;
    private static Dt MissingDate = Dt.Empty;

    private double upfront_;
    private Dt feeSettle_;
    private Cashflow cf_;
    private List<Pair<Dt, double>> defaultLosses_ = new List<Pair<Dt, double>>();
    #endregion Data
  }
}