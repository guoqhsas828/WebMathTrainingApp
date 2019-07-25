/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  BusinessDay conventions for date arithmetic.
  /// </summary>
  [BaseEntity.Shared.AlphabeticalOrderEnum]
  public enum BDConvention
  {
    /// <summary>
    /// None
    /// <para>The date will NOT be adjusted if it falls on a day that
    /// is not a business day.</para>
    /// </summary>
    None,

    /// <summary>
    /// Following
    /// <para>The non-business date will be adjusted to the first following
    /// day that is a business day</para>
    /// </summary>
    Following,

    /// <summary>
    /// Modified Following.
    /// <para>The non-business date will be adjusted
    /// to the first following day that is a business day unless
    /// that day falls in the next calendar month, in which case
    /// that date will be the first preceding day that is a business day.</para>
    /// </summary>
    Modified,

    /// <summary>
    /// Preceding.
    /// <para>The non-business day will be adjusted to the first
    /// preceding day that is a business day.</para>
    /// </summary>
    Preceding,

    /// <summary>
    /// Modified preceeding
    /// <para>The non-business date will be adjusted to the first preceding
    /// day that is a business day unless that day falls in the previous
    /// calendar month, in which case that date will be the first
    /// following day that is a business day.</para>
    /// </summary>
    ModPreceding,

    /// <summary>
    /// FRN Roll Convention
    /// <para>Per 2000 ISDA Definitions, Section 4.11. FRN Convention; Eurodollar Convention
    /// In respect of either Payment Dates or Period End Dates for a
    /// Swap Transaction and a party, that the Payment Dates or Period
    /// End Dates of that party will be each day during the term of
    /// the Swap Transaction that numerically corresponds to the
    /// preceding applicable Payment Date or Period End Date, as
    /// the case may be, of that party in the calendar month that
    /// is the specified number of months after the month in which
    /// the preceding applicable Payment Date or Period End Date
    /// occurred (or, in the case of the first applicable Payment
    /// Date or Period End Date, the day that numerically corresponds
    /// to the Effective Date in the calendar month that is the 
    /// specified number of months after the month in which the
    /// Effective Date occurred), except that (a) if there is not
    /// any such numerically corresponding day in a calendar month
    /// in which a Payment Date or Period End Date, as the case may
    /// be, of that party should occur, then the Payment Date or Period
    /// End Date will be the last day that is a Business Day in that
    /// month, (b) if a Payment Date or Period End Date, as the case may 
    /// be, of the party would otherwise fall on a day that is not a
    /// Business Day, then the Payment Date or Period End Date will be
    /// the first following day that is a Business Day unless that day
    /// falls in the next calendar month, in which case the Payment Date
    /// or Period End Date will be the first preceding day that is a
    /// Business Day, and (c) if the preceding applicable Payment Date
    /// or Period End Date, as the case may be, of that party occurred
    /// on the last day in a calendar month that was a Business Day,
    /// then all subsequent applicable Payment Dates or Period End Dates,
    /// as the case may be, of that party prior to the Termination Date
    /// will be the last day that is a Business Day in the month that is
    /// the specified number of months after the month in which the
    /// preceding applicable Payment Date or Period End Date occurred.</para>
    /// </summary>
    FRN,
  }
}
