/*
 * IPeriod.cs
 *
 *
 */
using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   An interface represent a period enclosed by start and end dates,
  ///   both ends inclusive.
  /// </summary>
  public interface IPeriod : IComparable<IPeriod>
	{
    /// <summary>Period start date.</summary>
    Dt StartDate { get; }

    /// <summary>Period end date.</summary>
    Dt EndDate { get; }

    /// <summary>
    /// Gets a value indicating whether the end date is excluded from the period.
    /// </summary>
    /// <value><c>true</c> if end date is exclusive; otherwise, <c>false</c>.</value>
    bool ExclusiveEnd { get; }
  }

  /// <summary>
  ///  An interface represent a date, perhaps in a sequence of dates.
  /// </summary>
  public interface IDate : IComparable<IDate>
  {
    /// <summary>Gets the date.</summary>
    Dt Date { get; }
  }

  /// <summary>
  ///   A simple period with both end inclusive
  /// </summary>
  [Serializable]
  public class Period : BaseEntityObject, IPeriod, IComparable
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    protected Period()
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="startDate">The start date of the period (inclusive)</param>
    /// <param name="endDate">The end date of the period (inclusive)</param>
    public Period(Dt startDate, Dt endDate)
    {
      _startDate = startDate;
      _endDate = endDate;
    }

    #region Properties

    /// <summary>
    ///   Start of call period
    /// </summary>
    [DtProperty]
    public Dt StartDate
    {
      get { return _startDate; }
    }

    /// <summary>
    ///   End of call period
    /// </summary>
    [DtProperty]
    public Dt EndDate
    {
      get { return _endDate; }
    }

    /// <summary>
    ///  Whether end date is exclusive
    /// </summary>
    bool IPeriod.ExclusiveEnd
    {
      get { return false; }
    }

    #endregion

    #region Validation

    /// <summary>
    ///   Validate things that cannot be tested in property set methods.
    /// </summary>
    public override void Validate(ArrayList errors)
    {
      if (!_startDate.IsValid())
        InvalidValue.AddError(errors, this, "StartDate",
          String.Format("Invalid start date {0}", _startDate));
      if (!_endDate.IsValid())
        InvalidValue.AddError(errors, this, "EndDate",
          String.Format("Invalid end date {0}", _endDate));
      if (_startDate > _endDate)
        InvalidValue.AddError(errors, this, "EndDate",
          String.Format("Start date {0} cannot be after end date {1}", _startDate,
            _endDate));
      return;
    }

    #endregion

    #region IComparable Members

    /// <summary>
    /// Compares 2 CallPeriods
    /// </summary>
    /// <param name="other">CallPeriod to compare to</param>
    /// <returns></returns>
    public int CompareTo(IPeriod other)
    {
      if (other == null) return 1;
      //check start dates
      return Dt.Cmp(this.StartDate, other.StartDate);
    }

    ///<summary>
    ///</summary>
    ///<param name="other"></param>
    ///<returns></returns>
    public int CompareTo(object other)
    {
      if ((other is IPeriod) == false)
        throw new ArgumentException("Compare to non-IPeriod type object not supported");

      return CompareTo((IPeriod) other);
    }

    #endregion IComparable Methods

    #region Data

    private readonly Dt _startDate;
    private readonly Dt _endDate;

    #endregion Data
  }
}
