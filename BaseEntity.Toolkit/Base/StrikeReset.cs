// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Historical strike reset
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This class defines a historical strike reset.</para>
  ///
  ///   <para>A strike reset schedule is represented by a list of HistoricalResets (IList\langle StrikeReset \rangle)
  ///   and utility methods are provided for standard calculations in
  ///   </para>
  ///
  ///   <para>StrikeReset is an immutable class.</para>
  /// </remarks>
  ///
  ///
  [Component(ChildKey = new[] {"Date"})]
  [Serializable]
  public class StrikeReset : BaseEntityObject, IDate, IComparable<StrikeReset>
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected StrikeReset()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// 
    /// <param name="date">Date rate is effective from</param>
    /// <param name="rate">Strike effective</param>
    ///
    public StrikeReset(Dt date, double rate)
    {
      Date = date;
      Strike = rate;
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
        InvalidValue.AddError(errors, this, "Date", String.Format("Effective date {0} is invalid", Date));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Effective date
    /// </summary>
    [DtProperty(AllowNullValue = false)]
    public Dt Date { get; set; }

    /// <summary>
    ///   Effective strike
    /// </summary>
    [NumericProperty(AllowNullValue = false)]
    public double Strike { get; set; }

    #endregion Properties

    #region IComparable<StrikeReset> Members

    /// <summary>
    /// Compares 2 HistoricalStrikes
    /// </summary>
    /// <param name="other">StrikeReset to compare to</param>
    /// <returns></returns>
    public int CompareTo(StrikeReset other)
    {
      //check start dates
      return Dt.Cmp(this.Date, other.Date);
    }

    int IComparable<IDate>.CompareTo(IDate other)
    {
      //check start dates
      return Dt.Cmp(this.Date, other.Date);
    }

    #endregion IComparable<StrikeReset> Methods
  }
}