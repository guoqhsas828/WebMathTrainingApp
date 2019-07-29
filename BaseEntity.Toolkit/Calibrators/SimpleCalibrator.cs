/*
 * SimpleCalibrator.cs
 *
 *   2008-2011. All rights reserved.
 *
 * Created by rsmulktis on 12/3/2008 11:08:13 AM
 *
 */

using System;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Calibrator class for calibrating curves that only have tenor values (ie no underlying model).
  /// </summary>
  [Serializable]
  public class SimpleCalibrator : Calibrator
  {
    #region Data

    //logger
    private static ILog Log = LogManager.GetLogger(typeof (SimpleCalibrator));

    #endregion

    #region Constructors

    /// <summary>
    ///   Constructor to specify an as of date.
    /// </summary>
    public SimpleCalibrator(Dt asOf) : base(asOf)
    {
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Fits the curve.
    /// </summary>
    /// <remarks>
    ///   Note that this implementation ignores the fromIdx parameter.
    /// </remarks>
    /// <param name = "curve">The curve to fit.</param>
    /// <param name = "fromIdx">The index to start fitting from.</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      // We'll always clear out the curve since the calibration will be so fast
      // Not that this ignores the fromIdx for now.
      curve.Clear();

      // For piecewise flat curve, we make sure it is left continous.
      var interp = curve.Interp as Flat;
      if (interp != null && interp.Round > Double.Epsilon)
        interp.Round = Double.Epsilon; // make it left-continous.

      var count = curve.Tenors.Count;
      var dates = new Dt[count];
      var values = new double[count];
      for (int i = 0; i < count; i++)
      {
        var pt = (CurvePointHolder)curve.Tenors[i].Product;
        dates[i] = pt.Maturity;
        values[i] = pt.Value;
      }
      curve.Add(dates, values);
    }

    #endregion
  }
}