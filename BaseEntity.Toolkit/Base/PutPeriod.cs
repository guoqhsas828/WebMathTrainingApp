/*
 * PutPeriod.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   Definition for an element of a put schedule.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This class defines the put schedule of a fixed income security such
  ///   as a Bond or a swap.</para>
  /// 
  ///   <para>PutPeriod is an immutable class.</para>
  /// </remarks>
  ///
  [Component]
  [Serializable]
  public class PutPeriod : BaseEntityObject, IOptionPeriod, IComparable<PutPeriod>, IComparable
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected PutPeriod()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="startDate">start of put period (inclusive)</param>
    /// <param name="endDate">end of call period (inclusive)</param>
    /// <param name="price">Put price</param>
    /// <param name="style">American or European</param>
    ///
    public PutPeriod(Dt startDate, Dt endDate, double price, OptionStyle style)
    {
      // Use properties to get validation
      startDate_ = startDate;
      endDate_ = endDate;
      price_ = price;
      style_ = style;
      return;
    }


    /// <summary>
    /// Create a put period from notification dates (exercise dates)
    /// </summary>
    /// <param name="notificationDate">exercise dates</param>
    /// <param name="price">Put price</param>
    /// <param name="style">Option styles, such as American, Bermuda and European</param>
    /// <param name="underlyingStartDate"></param>
    public PutPeriod(Dt notificationDate, double price, OptionStyle style,
      Dt underlyingStartDate)
    {
      // Use properties to get validation
      NotificationDate = notificationDate;
      startDate_ = underlyingStartDate;
      endDate_ = underlyingStartDate;
      price_ = price;
      style_ = style;
      return;
    }


    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate things that cannot be tested in property set methods.
    /// </summary>
    public override void Validate(ArrayList errors)
    {
      if (!startDate_.IsValid())
        InvalidValue.AddError(errors, this, "StartDate", String.Format("Invalid start date {0}", startDate_));
      if (!endDate_.IsValid())
        InvalidValue.AddError(errors, this, "EndDate", String.Format("Invalid end date {0}", endDate_));
      if (startDate_ > endDate_)
        InvalidValue.AddError(errors, this, "EndDate", String.Format("Start date {0} cannot be after end date {1}", startDate_, endDate_));
      if (style_ == OptionStyle.None)
        InvalidValue.AddError(errors, this, "Style", "Invalid option style. Must not be None");

      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Accrual start date
    /// </summary>
    [DtProperty]
    public Dt StartDate
    {
      get { return startDate_; }
    }
    
    /// <summary>
    ///   End of call period
    /// </summary>
    [DtProperty]
    public Dt EndDate
    {
      get { return endDate_; }
    }
    
    /// <summary>
    ///   Put price
    /// </summary>
    [NumericProperty]
    public double PutPrice
    {
      get { return price_; }
    }
    
    /// <summary>
    ///   Option style
    /// </summary>
    [EnumProperty]
    public OptionStyle Style
    {
      get { return style_; }
    }

    /// <summary>
    /// 
    /// </summary>
    public Dt NotificationDate { get; set; }
    

    #endregion Properties

    #region IComparable<CallPeriod> Members

    /// <summary>
    ///   Compares 2 PutPeriods
    /// </summary>
    /// <param name="other">PutPeriod to compare</param>
    /// <returns></returns>
    public int CompareTo(PutPeriod other)
    {
      return Dt.Cmp(this.StartDate, other.StartDate);
    }


    ///<summary>
    /// General comparer, needed for BinarySearch in UniqueSequence
    ///</summary>
    ///<param name="obj">Other object</param>
    ///<returns></returns>
    public int CompareTo(object obj)
    {
      if (obj == null)
        return 1;
      var ipd = obj as IPeriod;
      if (ipd == null)
        throw new ArgumentException("Comparing PutPeriod against non-IPeriod object not supported");

      return Dt.Cmp(this.StartDate, ipd.StartDate);
    }

    #endregion

    #region Data

    private readonly Dt startDate_;
    private readonly Dt endDate_;
    private readonly double price_;
    private readonly OptionStyle style_;

    #endregion Data

    #region IOptionPeriod Members

    double IOptionPeriod.ExercisePrice
    {
      get { return price_; }
    }

    OptionType IOptionPeriod.Type
    {
      get { return OptionType.Put; }
    }

    int IComparable<IPeriod>.CompareTo(IPeriod other)
    {
      return Dt.Cmp(this.StartDate, other.StartDate);
    }


    #endregion

    #region IPeriod Members

    bool IPeriod.ExclusiveEnd
    {
      get { return false; }
    }

    #endregion

  } // PutPeriod

}
