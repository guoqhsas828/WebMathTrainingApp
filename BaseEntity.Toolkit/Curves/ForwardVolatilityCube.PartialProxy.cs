using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Forward volatility cube calibrated from Cap/Floors.
  /// </summary> 
  ///<remarks>
  /// 
  /// The resulting cube of caplet volatilities has dimensions running time,
  /// caplet expiry and strike.  
  /// The caplet tenor is fixed. Interpolation in time dimension defaults to PCHIP, 
  /// interpolation in expiry and strike defaults to linear 
  /// </remarks>
  
  [Serializable]
  public class ForwardVolatilityCube : BaseEntityObject
  {
    #region Constructors
    /// <summary>
    /// Constructor for the Forward Volatility Cube class
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="dates"></param>
    /// <param name="expiries"></param>
    /// <param name="strikes"></param>
    internal ForwardVolatilityCube(Dt asOf, Dt[] dates, Dt[] expiries, double[] strikes)
      : this(asOf)
    {
      expiries_ = expiries;
      dates_ = dates;
      strikes_ = strikes;
      var firstCapIdx = new int[strikes.Length];
      for (int i = 0; i < strikes.Length; i++)
        firstCapIdx[i] = 0;
      native_.SetFirstCapIdx(firstCapIdx);
      InitCube();
    }

    /// <summary>
    /// Create a flat forward volatility cube
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="vol"></param>
    internal ForwardVolatilityCube(Dt asOf, double vol)
      : this(asOf)
    {
      expiries_ = new Dt[] { asOf };
      dates_ = new Dt[] { asOf };
      strikes_ = new double[] { 0.00 };
      var firstCapIdx = new int[] {0};
      native_.SetFirstCapIdx(firstCapIdx);
      InitCube();
      AddVolatility(0, 0, 0, vol);
    }
    /// <summary>
    /// Create forward volatility cube constant in running time and strike dimensions from 
    /// a caplet volatility curve
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="expiries"></param>
    /// <param name="vols"></param>
    internal ForwardVolatilityCube(Dt asOf, Dt[] expiries, double[] vols)
      : this(asOf)
    {
      expiries_ = expiries;
      dates_ = new Dt[] { asOf };
      strikes_ = new double[] { 0.0 };
      var firstCapIdx = new int[] { 0 };
      native_.SetFirstCapIdx(firstCapIdx);
      InitCube();
      for (int i = 0; i < vols.Length; i++)
        AddVolatility(0, i, 0, vols[i]);
    }

    private ForwardVolatilityCube(Dt asOf)
    {
      native_ = new NativeForwardVolatilityCube(asOf);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Description of the cube.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Dates
    /// </summary>
    public Dt[] Dates
    {
      get { return dates_; }
    }

    /// <summary>
    /// Expiries
    /// </summary>
    public Dt[] Expiries
    {
      get { return expiries_; }
    }

    /// <summary>
    /// Tenors for the expiries that define the cube.
    /// </summary>
    public Tenor[] ExpiryTenors
    {
      get { return expiryTenors_; }
      set{ expiryTenors_ = value;}
    }

    /// <summary>
    /// Strikes 
    /// </summary>
    public double[] Strikes
    {
      get { return strikes_; }
    }

    public NativeForwardVolatilityCube Native
    {
      get { return native_; }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Init method for the Forward Volatility Cube
    /// 
    /// </summary>
    internal void InitCube()
    {
      native_.Initialize(Dates.Length, Expiries.Length, Strikes.Length);
      for (int i = 0; i < Dates.Length; i++)
        native_.SetDate(i, Dates[i]);
      for (int i = 0; i < Expiries.Length; i++)
        native_.SetExpiry(i, Expiries[i]);
      for (int i = 0; i < Strikes.Length; i++)
        native_.SetStrike(i, Strikes[i]);
    }

    /// <summary>
    /// todo: method should take in a date , expiry and strike and be able to add a value
    /// </summary>
    /// <param name="dateIndex"></param>
    /// <param name="expiryIndex"></param>
    /// <param name="strikeIndex"></param>
    /// <param name="vol"></param>
    internal void AddVolatility(int dateIndex, int expiryIndex, int strikeIndex, double vol)
    {
      native_.AddValue(dateIndex, expiryIndex, strikeIndex, vol);
    }

    /// <summary>
    /// todo: method should be able to take in a date and strike 
    /// </summary>
    /// <param name="dateIndex"></param>
    /// <param name="expiryIndex"></param>
    /// <param name="strikeIndex"></param>
    /// <returns></returns>
    public double GetVolatility(int dateIndex, int expiryIndex, int strikeIndex)
    {
      return native_.GetValue(dateIndex, expiryIndex, strikeIndex);
    }

    /// <summary>
    /// Clear out the cube.
    /// </summary>
    public void Clear()
    {
      //TODO: Clear cube.
    }

    /// <summary>
    /// Interpolates the forward volatility for the specified dates and strike.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility</returns>
    public double Interpolate(Dt date, Dt expiry, double strike)
    {
      return native_.Interpolate(date, expiry, strike);
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="expiryIndex">Index of the expiry.</param>
    /// <param name="strikeIndex">Index of the strike.</param>
    /// <returns></returns>
    public double GetValue(int dateIndex, int expiryIndex, int strikeIndex)
    {
      return native_.GetValue(dateIndex, expiryIndex, strikeIndex);
    }
    #endregion

    #region Data
    private NativeForwardVolatilityCube native_;
    private readonly Dt[] dates_;
    private readonly Dt[] expiries_;
    private readonly double[] strikes_;
    private Tenor[] expiryTenors_;
    #endregion

  }
}
