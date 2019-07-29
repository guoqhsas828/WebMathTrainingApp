/*
 * RateReset.cs
 *
 *   2007-2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Historical rate reset
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This class defines a historical rate reset.</para>
  ///
  ///   <para>A rate reset schedule is represented by a list of HistoricalResets (IList\langle RateReset \rangle)
  ///   and utility methods are provided for standard calculations in
  ///   <see cref="RateResetUtil"/></para>
  ///
  ///   <para>RateReset is an immutable class.</para>
  /// </remarks>
  ///
  /// <seealso cref="RateResetUtil"/>
  ///
  [Component(ChildKey = new[] { "Date" })]
  [Serializable]
  public class RateReset : BaseEntityObject, IDate, IComparable<RateReset>
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected RateReset()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// 
    /// <param name="date">Date rate is effective from</param>
    /// <param name="rate">Rate effective</param>
    ///
    public RateReset(Dt date, double rate)
    {
      Date = date;
      Rate = rate;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate
    /// </summary>
    ///
    public override void Validate(ArrayList errors)
    {
      if (!Date.IsValid())
        InvalidValue.AddError(errors, this, "Date", String.Format("Rate effective date {0} is invalid", Date));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Coupon effective date
    /// </summary>
    [DtProperty(AllowNullValue = false)]
    public Dt Date { get; set; }

    /// <summary>
    ///   Effective rate
    /// </summary>
    [NumericProperty(AllowNullValue = false, Format = NumberFormat.Percentage)]
    public double Rate { get; set; }

    /// <summary>
    /// Gets the value (alternative name of rate)
    /// </summary>
    /// <value>The value.</value>
    public double Value { get { return Rate;} }

    #endregion Properties

    #region IComparable<RateReset> Members

    /// <summary>
    /// Compares 2 HistoricalResets
    /// </summary>
    /// <param name="other">RateReset to compare to</param>
    /// <returns></returns>
    public int CompareTo(RateReset other)
    {
      //check start dates
      return Dt.Cmp(this.Date, other.Date);
    }

    int IComparable<IDate>.CompareTo(IDate other)
    {
      //check start dates
      return Dt.Cmp(this.Date, other.Date);
    }

    #endregion IComparable<RateReset> Methods

  } // RateReset
}
