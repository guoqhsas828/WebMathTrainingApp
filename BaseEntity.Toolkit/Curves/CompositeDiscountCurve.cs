using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Discount curve formed of the combination of two different discount curves with different settle dates 
  /// </summary>
  [Serializable]
  public class CompositeDiscountCurve : DiscountCurve
  {
    #region Calibrator
    private class CompositeCalibrator : DiscountCalibrator
    {
      internal CompositeCalibrator(Dt asOf) : base(asOf) { }

      protected override void FitFrom(CalibratedCurve curve, int fromIdx)
      {
        var cdc = (CompositeDiscountCurve)curve;
        cdc.InitDerivativesWrtQuotes = false;
        cdc.preSpotCurve_.Fit();
        cdc.postSpotCurve_.Fit();
        cdc.SetUpCompositeCurve();
      }
    }
    #endregion Calibrator

    #region Data
    private DiscountCurve preSpotCurve_;
    private DiscountCurve postSpotCurve_;
    private int nPtsPre_;
    #endregion

    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="preSpotCurve">Pre spot curve. The settle date of this curve must be prior to that of post spot curve</param>
    /// <param name="postSpotCurve">Post spot curve</param>
    public CompositeDiscountCurve(Dt asOf, DiscountCurve preSpotCurve, DiscountCurve postSpotCurve)
      : base(new CompositeCalibrator(asOf))
    {
      if (preSpotCurve.GetDt(0) > postSpotCurve.AsOf)
        throw new ToolkitException("First curve point of pre spot curve should be less or equal than settle date of post spot curve");
      preSpotCurve_ = (DiscountCurve)preSpotCurve.CloneWithCalibrator();
      postSpotCurve_ = (DiscountCurve)postSpotCurve.CloneWithCalibrator();

      // Set up C++ curve
      SetUpCompositeCurve();

      // Find the point before spot date.
      {
        int prept = 0;
        Dt spot = postSpotCurve.AsOf;
        int count = preSpotCurve.Count;
        for (int i = 0; i < count; ++i)
        {
          if (preSpotCurve.GetDt(i) >= spot)
            break;
          ++prept;
        }
        nPtsPre_ = prept;
      }

      //At least one tenor will be added
      Tenors = new CurveTenorCollection();
      DiscountCurveCalibrationUtils.SetCurveDates(preSpotCurve_.Tenors);
      DiscountCurveCalibrationUtils.SetCurveDates(postSpotCurve_.Tenors);
      if (preSpotCurve_.Tenors != null)
      {
        for (int i = 0; i < preSpotCurve_.Tenors.Count; i++)
        {
          if (preSpotCurve_.Tenors[i].CurveDate >= postSpotCurve_.AsOf)
            break;
          Tenors.Add(preSpotCurve_.Tenors[i]);
        }
      }
      if (postSpotCurve_.Tenors != null)
      {
        for (int i = 0; i < postSpotCurve_.Tenors.Count; i++)
          Tenors.Add(postSpotCurve_.Tenors[i]);
      }
    }

    /// <summary>
    /// Clone method
    /// </summary>
    public override object Clone()
    {
      return new CompositeDiscountCurve(AsOf, preSpotCurve_, postSpotCurve_);
    }

    /// <summary>
    ///   Set up the C++ composite curve.
    /// </summary>
    /// <remarks>the serialization service also calls this function
    /// to set up the composite curve with correct references.</remarks>
    [OnDeserialized, AfterFieldsCloned]
    private void SetUpCompositeCurve(StreamingContext context)
    {
      Composite(preSpotCurve_, postSpotCurve_);
      return;
    }

    private void SetUpCompositeCurve()
    {
      Composite(preSpotCurve_, postSpotCurve_);
      return;
    }
    #endregion

  }
} 
