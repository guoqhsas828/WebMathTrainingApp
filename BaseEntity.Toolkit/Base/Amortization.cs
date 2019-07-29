/*
 * Amortization.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   An individual amortization amount for a Fixed income security.
  /// </summary>
  ///
  /// <remarks>
  ///  <para>This class represents an amortization schedule for a fixed income
  ///   security such as a Bond.
  ///  </para>
  /// <para>In the following, we will introduce how an amortization schedule is filled 
  /// in the cash flow. To do that, we first define the following symbols:
  /// </para>
  /// 
  /// <para>
  ///  <m>T_1, T_2, T_3 ...</m> The cash flow period dates, 
  /// which may be different from the coupon period end dates.
  /// </para>
  /// 
  /// <para><m>t_1, t_2, t_3 ...</m> The amortization dates.</para>
  /// 
  /// <para>
  /// <m>a_1, a_2, a_3 ...</m> The amortization amount, 
  /// representing percentage of  the orignial notional 
  /// <m>N_0 (\equiv 0)</m> 
  /// and assume <m> a_0 = 0</m> 
  /// </para>
  /// 
  /// <para>
  /// To simplify the question, here we only consider about the amortization effect 
  /// on the cash flow, and other considerations such as default, fixed or floating 
  /// rate and so on are ignored. The principle to calculate amortizations is 
  /// to search the amortization between the last cash flow date and current cash 
  /// flow date and apply the remaining notional for the following cash flow period. 
  /// </para>
  /// 
  /// <para>We supports three types of amortization amount. They are: </para>
  /// 
  /// <para>PercentOfInitialNotional---Amortization amount <m>a_i</m> represents the percentage 
  /// of the original notional.</para>
  /// 
  /// <para>PercentOfCurrentNotional---Amortization amount  
  /// <m>a_i</m> represents the percentage of the current notional.</para>
  /// 
  /// <para>RemainingNotionalLevels----Amortization amount <m>a_i</m>
  /// directly represents the new notional level for that period, 
  /// scaled by the original notional. It sort of likes “step” schedule.</para>
  /// 
  /// <para>
  /// Let us define an index function, which satisfies
  /// <math>\psi(j)=max\{i: t_i \leq T_j\}\tag{1} </math> 
  /// For the type of PercentOfInitialNotional, the total amortization up to time <m>T_j</m> is
  /// <math>A_j^{tot} = \sum_{i=0}^{\psi(j)}a_i \tag{2}</math>
  /// Therefore, the balance applied to the cash flow period <m> [T_j, T_{j+1}]</m> is:
  /// <math>B(T_j, T_{j+1}) = 1-A_j^{tot} \tag{3}</math>
  /// Please note that the amortization payment on cash flow 
  /// dates <m> T_j</m> is <m>A_j^{tot}-A_{j-1}^{tot}</m>.
  /// </para>
  /// 
  /// <para>
  /// For the type of PercentOfCurrentNotional, the amortization amount at <m>t_i</m> is
  /// <math>A_i = a_i \prod_{k=0}^{i-1}(1-a_k) \tag{4} </math>
  /// And the balance applied to the period <m>[T_j, T_{j+1}]</m> is
  /// <math>B(T_j, T_{j+1}) = 1 - \sum_{i=1}^{\psi(j)}A_i = \prod_{i=1}^{\psi(j)}(1-a_i) \tag{5}</math>
  /// </para>
  /// 
  /// <para>For the type of RemainingNotionalLevels, the balance applied to the 
  /// period <m>[T_j,T_(j+1)]</m> is: 
  /// <math>B(T_j, T_{j+1})=a_{\psi(j)}\tag{6}</math>
  /// where <m>\psi(j)</m> is defined in the fomula (1).</para>
  /// <para></para>
  /// </remarks>
  ///
  /// <seealso cref="AmortizationUtil"/>
  ///
  [Serializable]
  public class Amortization : BaseEntityObject, IDate, IComparable<Amortization>
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    protected Amortization()
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Amortization type defaults to a percentage of initial notional</para>
    /// </remarks>
    ///
    /// <param name="date">Date of amortization</param>
    /// <param name="amount">Amortization amount</param>
    ///
    public Amortization(Dt date, double amount)
    {
      // Use properties to get validation
      date_ = date;
      type_ = AmortizationType.PercentOfInitialNotional;
      amount_ = amount;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="date">Date of amortization</param>
    /// <param name="amortType">Amortization type</param>
    /// <param name="amount">Amortization amount</param>
    ///
    public Amortization(Dt date, AmortizationType amortType, double amount)
    {
      // Use properties to get validation
      date_ = date;
      type_ = amortType;
      amount_ = amount;
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate Amortization
    /// </summary>
    ///
    public override void Validate(ArrayList errors)
    {
      if (!date_.IsValid())
        InvalidValue.AddError(errors, this, "Date", String.Format("Amortization date {0} is invalid", date_));
      if (amount_ < 0.0)
        InvalidValue.AddError(errors, this, "Amount", String.Format("Amortization {0} is negative", amount_));

      return;
    }
    #endregion Methods

    #region Properties

    /// <summary>
    ///   Type of amortization
    /// </summary>
    public AmortizationType AmortizationType
    {
      get { return type_; }
    }

    /// <summary>
    ///   Amount of amortization payment
    /// </summary>
    public double Amount
    {
      get { return amount_; }
    }

    /// <summary>
    /// For use in floating amortization payments
    /// </summary>
    public bool AmountOverridden
    {
      get { return amount_ > 0; }
    }

    /// <summary>
    ///   Date of amortization payment
    /// </summary>
    public Dt Date
    {
      get { return date_; }
    }

    #endregion Properties

    #region Data

    private readonly double amount_;
    private readonly Dt date_;
    private readonly AmortizationType type_;

    #endregion Data

    #region IComparable<Amortization> Members

    /// <summary>
    /// Compares the Amortization to another Amortization based on Date.
    /// </summary>
    /// 
    /// <param name="other">The Amortization to compare.</param>
    /// 
    /// <returns>Order</returns>
    /// 
    public int CompareTo(Amortization other)
    {
      return Date.CompareTo(other.Date);
    }

    #endregion

    #region IDate Members

    int IComparable<IDate>.CompareTo(IDate other)
    {
      //check start dates
      return Dt.Cmp(Date, other.Date);
    }

    #endregion
  } // class Amortization
}