/*
 * ABSCDO.cs
 *
 *  -2008. All rights reserved. 
 *
 * $Id$
 *
 * TBD: Add ABSCDO specific stuffs. HJ Feb07
 *
 */
using System;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   CDO of ABS Tranches
  /// </summary>
  /// <remarks>
  ///   <para>Synthetic CDOs of ABS are OTC contracts which repackage the
  ///   risk of a pool of underlying tranches of asset-backed securities.</para>
  ///   <para>Similar in concept to a CDO-squared, synthetic CDO of ABS tranches
  ///   accumulate losses based on losses of the underlying ABS tranches which, in turn
  ///   accumulate losses on the underlying pool of loans (e.g. residential mortgages).</para>
  ///   <para>For a particular tranche, the buyer is exposed to the losses of the underlying
  ///   ABS tranches between some 'attachment' and 'detachment' point. For example a 14%-29%
  ///   tranche is exposed to all losses above the first 14% of original face to a maximum of
  ///   29% of original face.</para>
  ///   <p><img src="ABSCDO.png" /></p>
  /// </remarks>
  [Serializable]
  public class ABSCDO : SyntheticCDO
  {
    #region Constructors

    /// <summary>
    ///   Default Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Defaults to 0-100% tranche.</para>
    /// </remarks>
    ///
    protected ABSCDO() : base()
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>Defaults to 0-100% tranche.</para>
    /// </remarks>
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="premium">Annualised premium in basis points (200 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    public ABSCDO(
      Dt effectiveDate,
      Dt maturityDate,
      Currency currency,
      double premium,
      DayCount dayCount,
      Frequency frequency,
      BDConvention roll,
      Calendar calendar)
      : base(effectiveDate, maturityDate, currency, premium, dayCount, frequency, roll, calendar)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>Defaults to 0-100% tranche.</para>
    /// </remarks>
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="premium">Annualised premium in basis points (200 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    public ABSCDO(
      Dt effectiveDate,
      Dt maturityDate,
      Currency currency,
      double fee,
      double premium,
      DayCount dayCount,
      Frequency frequency,
      BDConvention roll,
      Calendar calendar)
      : base(effectiveDate, maturityDate, currency, fee, premium, dayCount, frequency, roll, calendar)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="premium">Annualised premium in basis points (200 = 200bp)</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="attachment">Attachment point for tranche in percent (0.1 = 10%)</param>
    /// <param name="detachment">Detachment point for tranche in percent (0.2 = 20%)</param>
    public ABSCDO(
      Dt effectiveDate,
      Dt maturityDate,
      Currency currency,
      DayCount dayCount,
      Frequency frequency,
      BDConvention roll,
      Calendar calendar,
      double premium,
      double fee,
      double attachment,
      double detachment)
      : base(effectiveDate, maturityDate, currency, dayCount, frequency, roll, calendar,
        premium, fee, attachment, detachment)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="effectiveDate">Issue Date (date premium started accruing)</param>
    /// <param name="firstPremiumDate">First Premium payment date</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="premium">Annualised premium in basis points (200 = 200bp)</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="attachment">Attachment point for tranche in percent (0.1 = 10%)</param>
    /// <param name="detachment">Detachment point for tranche in percent (0.2 = 20%)</param>
    public ABSCDO(
      Dt effectiveDate,
      Dt firstPremiumDate,
      Dt maturityDate,
      Currency currency,
      DayCount dayCount,
      Frequency frequency,
      BDConvention roll,
      Calendar calendar,
      double premium,
      double fee,
      double attachment,
      double detachment)
      : base(effectiveDate, maturityDate, currency, dayCount, frequency, roll, calendar,
        premium, fee, attachment, detachment)
    {
    }

    #endregion Constructors
  } // ABSCDO
}
