/*
 * CashFlowAgregator.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Class used to generate sum up the cash flows of a pool of securities
  /// </summary>
  [Serializable]
  public class CashFlowAggregator : BaseEntityObject
  {
    #region Data

    private double notional_;
    private CashflowStream cashflows_;
    
    #endregion // Data

    #region Properties
    /// <summary>
    /// Return the CashFlowStream of aggregate cash flows of a pool of securities
    /// </summary>
    public CashflowStream Cashflows
    {
      get { return cashflows_; }
    }

    /// <summary>
    /// Return the Portfolio Notional Amount
    /// </summary>
    public double Notional
    {
      get { return notional_; }
    }

    #endregion // Properties

    #region Contructors
    /// <summary>
    /// Constructor that generates an empty CashFlowAggregator given a schedule of payment dates
    /// To aggregate cash flows, user should call the method "Add"
    /// </summary>
    /// <param name="sched">Schedule of dates</param>
    public CashFlowAggregator(Schedule sched)
    {
      notional_ = 0;      
      cashflows_ = new CashflowStream();
      for (int t = 0; t < sched.Count; t++)
      {
        cashflows_.Add(sched.GetPaymentDate(t), 0, 0, 0, 0);
      }
    }

    /// <summary>
    /// Clone class object
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      CashFlowAggregator obj= (CashFlowAggregator)base.Clone();
      obj.cashflows_ = this.cashflows_.clone();
      return obj;
    }

    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Given a Product, generate a CashflowStream for that Product and add it to the CashflowStream of the CashFlowAggregator
    /// </summary>
    /// <param name="Bal">Outstanding Balance of Security</param>
    /// <param name="weight">Security's weight (Balance/Total Notional) in the basket or portfolio</param>
    /// <param name="csf">CashflowStream to be added</param>
    /// <param name="r">The rate of accrual of cash-on-cash reserve account</param>
    public void Add(double Bal, double weight, CashflowStream csf, double r)
    {
      notional_ += Bal; // add the balance of the new security to the portfolio notional

      int start_marker = 0;
      int end_marker = start_marker+1; // OK, because it's extremely unlikely that we will have date vectors of less that 2 periods
      double pInterest;
      double pPrincipal;
      double pAccrued ;

      for (int agg_marker = 0; agg_marker < this.cashflows_.Count; agg_marker++)
      {
        if (end_marker < csf.Count)
        {
          pInterest = 0;
          pPrincipal = 0;
          pAccrued = 0;
          while (csf.GetDate(end_marker) <= this.cashflows_.GetDate(agg_marker))
          {
            end_marker++;
            if (end_marker >= csf.Count)
              break;
          }
          for (int t = start_marker; t < end_marker; t++)
          {
            pInterest += csf.GetInterest(t);
            pPrincipal += csf.GetPrincipal(t);
            pAccrued += csf.GetAccrual(t);
          }
          // normalize the cash flows by the weight of the security in the portfolio and add to portfolio cash flow stream
          this.cashflows_.Set(agg_marker, (pPrincipal * weight) + this.cashflows_.GetPrincipal(agg_marker),
                                     (pAccrued * weight) + this.cashflows_.GetAccrual(agg_marker),
                                     (pInterest * weight) + this.cashflows_.GetInterest(agg_marker),
                                     (csf.GetNotional(end_marker-1) * weight) + this.cashflows_.GetNotional(agg_marker));
          start_marker = end_marker;
        }
        else
        {
          break;
        }
      }

      // if the dates of the csf are after the last date of this.cashflows_, then we use the last date of cashflows_
      // as a "call" date and add all remaining principal payments on csf to the last period principal of cashflows_
      // in addition, we decrement the balance of cashflows by that amount and do not add any more interest and accrual to cashflows_
      pPrincipal = 0;
      while (end_marker < csf.Count) 
      {
        pPrincipal += csf.GetPrincipal(end_marker);
        end_marker++;
      }
      // normalize the cash flows by the weight of the security in the portfolio and add to portfolio cash flow stream
      this.cashflows_.Set(this.cashflows_.Count - 1, (pPrincipal * weight) + this.cashflows_.GetPrincipal(this.cashflows_.Count - 1),
                                 this.cashflows_.GetAccrual(this.cashflows_.Count-1),
                                 this.cashflows_.GetInterest(this.cashflows_.Count-1),
                                 this.cashflows_.GetNotional(this.cashflows_.Count-1) - (pPrincipal * weight));
    }


    #endregion // Methods

  }
}