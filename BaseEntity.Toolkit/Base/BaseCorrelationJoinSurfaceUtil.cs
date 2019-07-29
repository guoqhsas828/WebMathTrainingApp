/*
 * BaseCorrelationJoinSurfaceUtil.cs
 *
 * Class that takes the XML tag "UseOldJoinSurfaces" and build mixed BaseCorrelationObject
 *
 *
 */
using System;
using System.Collections;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Pricers.Baskets;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  This class will take all neccesary parameters and build the mixed BaseCorrelationObject
  /// </summary>
	public static class BaseCorrelationJoinSurfaceUtil
  {
    #region Methods
    /// <summary>
    ///  Construct the combined case correlation object
    /// </summary>
    /// <param name="baseCorrelations">Array of base correlation surfaces</param>
    /// <param name="weights">Array of weights used to combine the correlation surfaces</param>
    /// <param name="strikeInterp">Interpolation method for strikse</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes</param>
    /// <param name="timeInterp">Interpolation method for tenors</param>
    /// <param name="timeExtrap">Extrapolation method for tenors</param>
    /// <param name="min">Lower limit for base correlations</param>
    /// <param name="max">Upper limit for base correlations</param>
    /// <param name="combiningMethod">Correlation mixing method: JoinSurfaces, PvAveraging, ByName</param>
    /// <returns>BaseCorrelationObject</returns>
    public static BaseCorrelationObject CombineBaseCorrelations(
      BaseCorrelationObject[] baseCorrelations,
      double[] weights,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod timeInterp,
      ExtrapMethod timeExtrap,
      double min,
      double max,
      BaseCorrelationCombiningMethod combiningMethod
      )
    {
      return CombineBaseCorrelations(baseCorrelations, weights,
        strikeInterp, strikeExtrap, timeInterp, timeExtrap, min, max, combiningMethod, null, null);
    }

    /// <summary>
    ///  Construct the combined case correlation object
    /// </summary>
    /// <param name="baseCorrelations">Array of base correlation surfaces</param>
    /// <param name="weights">Array of weights used to combine the correlation surfaces</param>
    /// <param name="strikeInterp">Interpolation method for strikse</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes</param>
    /// <param name="timeInterp">Interpolation method for tenors</param>
    /// <param name="timeExtrap">Extrapolation method for tenors</param>
    /// <param name="min">Lower limit for base correlations</param>
    /// <param name="max">Upper limit for base correlations</param>
    /// <param name="mixMethod">Correlation mixing method: JoinSurfaces, PvAveraging, ByName</param>
    /// <param name="creditNames">Credit names to add for ByName method</param>
    /// <param name="associatedBCs">Base correlations associated with credit names</param>
    /// <returns>BaseCorrelationObject</returns>
    public static BaseCorrelationObject CombineBaseCorrelations(
      BaseCorrelationObject[] baseCorrelations, 
      double[] weights, 
      InterpMethod strikeInterp, 
      ExtrapMethod strikeExtrap, 
      InterpMethod timeInterp, 
      ExtrapMethod timeExtrap, 
      double min, 
      double max, 
      BaseCorrelationCombiningMethod mixMethod,
      string[] creditNames,
      BaseCorrelationObject[] associatedBCs)
    {
      // Validation
      if (mixMethod != BaseCorrelationCombiningMethod.ByName)
      {
        if (baseCorrelations == null || baseCorrelations.Length == 0)
          throw new ArgumentException(String.Format("{0} requires at least one base correlation", mixMethod.ToString()));
        if (weights == null || weights.Length == 0)
        {
          int length = baseCorrelations.Length;
          weights = new double[length];
          for (int k = 0; k < length; ++k) weights[k] = 1.0 / length;
        }
        else
        {
          if (baseCorrelations.Length != weights.Length)
            throw new ArgumentException("Base correlations and weights must be of same size");
          else
          {
            Array[] bcs_wts_pair = RemoveNullPairs(baseCorrelations, weights);
            baseCorrelations = (BaseCorrelationObject[])bcs_wts_pair[0];
            weights = (double[])bcs_wts_pair[1];
            if (weights.Length > 1)
            {
              double sum = 0;
              for (int k = 0; k < weights.Length; ++k)
                sum += weights[k];

              // make the weights add up to 1
              for (int i = 0; i < weights.Length; ++i)
                weights[i] /= sum;
            }
            else
              weights[0] = 1.0;
          }
        }
        if (baseCorrelations == null || baseCorrelations.Length < 1)
          throw new System.ArgumentException("Must specify Base correlations");
      }
      else if ((baseCorrelations == null || baseCorrelations.Length == 0)
        && (associatedBCs == null || associatedBCs.Length == 0))
        throw new System.ArgumentException("Must specify Base correlations");

      BaseCorrelationObject bco = null;
      switch (mixMethod)
      {
        case BaseCorrelationCombiningMethod.MergeSurfaces:
          bco = new BaseCorrelationCombined(baseCorrelations, weights, strikeInterp, strikeExtrap, timeInterp, timeExtrap, min, max);
          break;
        case BaseCorrelationCombiningMethod.PvAveraging:
          bco = new BaseCorrelationMixWeighted(baseCorrelations, weights);
          if (!Double.IsNaN(max)) bco.Extended = (max > 1);
          break;
        case BaseCorrelationCombiningMethod.ByName:
          if (baseCorrelations == null || baseCorrelations.Length == 0)
          {
            bco = BaseCorrelationMixByName.FromCorrelationByNames(associatedBCs, creditNames);
            if (!Double.IsNaN(max)) bco.Extended = (max > 1);
            break;
          }
          bco = BaseCorrelationMixByName.FromDisjointCorrelations(baseCorrelations);
          if (creditNames != null && creditNames.Length != 0)
          {
            if (associatedBCs == null || associatedBCs.Length != creditNames.Length)
              throw new System.ArgumentException("Additional names and correlations not match");
            bco = ((BaseCorrelationMixByName)bco).CreateSupersetCorrelation(creditNames, associatedBCs);
            if (!Double.IsNaN(max)) bco.Extended = (max > 1);
          }
          break;
        case BaseCorrelationCombiningMethod.JoinSurfaces:
        default:
          bco = new BaseCorrelationJointSurfaces(baseCorrelations, weights, timeInterp, timeExtrap, min, max);
          break;
      }

      // Check for correlated recovery flag
      {
        CorrelatedRecoveryChecker check = new CorrelatedRecoveryChecker();
        bco.Walk(check.Check);
        bco.RecoveryCorrelationModel = check.RecoveryCorrelationModel;
      }

      return bco;
    }

    private class CorrelatedRecoveryChecker
    {
      internal bool Check(BaseCorrelationObject bco)
      {
        if ((bco is BaseCorrelationTermStruct) || (bco is BaseCorrelation))
        {
          if (initialized_)
          {
            // Already found base correlation and use correlated recovery or new LCDO model,
            // We check if model choices are consistent.
            if (ModelChoice != bco.ModelChoice && (
              ModelChoice.WithCorrelatedRecovery
              || ModelChoice.BasketModel == BasketModelType.LCDOCommonSignal
              || bco.ModelChoice.WithCorrelatedRecovery
              || bco.ModelChoice.BasketModel == BasketModelType.LCDOCommonSignal))
            {
              throw new ArgumentException(
                "Cannot mix base correlations with different model choices.");
            }
          }
          else
          {
            RecoveryCorrelationModel = bco.RecoveryCorrelationModel;
            initialized_ = true;
          }
          return false; // do not check sub-objects
        }
        return true; // check all sub-objects
      }

      private BasketModelChoice ModelChoice
      {
        get { return RecoveryCorrelationModel.ModelChoice; }
      }
      private bool initialized_;
      internal RecoveryCorrelationModel RecoveryCorrelationModel
        = RecoveryCorrelationModel.Default;
    }

    #endregion Methods

    #region Helpers 

    /// <summary>
    /// Check if a string is either null, empty, or blank
    /// </summary>
    /// <exclude/>
    private static bool HasValue(string strValue)
    {
      return (String.IsNullOrEmpty(strValue)) ? false : (strValue.Trim().Equals(String.Empty)) ? false : true;
    }

    /// <summary>
    ///   Return true if object is empty in some sense.
    /// </summary>
    /// <exlcude/>
    private static bool IsEmpty(Object obj)
    {
      return (obj == null ||
              (obj is Double && obj.Equals(0.0)) ||
              (obj is String && !HasValue((string)obj)));
    }

    /// <summary>
    ///   Remove null object pairs from parallel arrays of objects.
    /// </summary>
    /// <exclude/>
    private static Array[] RemoveNullPairs(Object[] objects1, double[] objects2)
    {
      if (objects1.Length != objects2.Length)
        throw new ToolkitException("Parallel arrays should have same length");

      int count = 0;
      for (int i = 0; i < objects1.Length; i++)
        if (!IsEmpty(objects1[i]))
          count++;

      Array objs1 = Array.CreateInstance(objects1.GetType().GetElementType(), count);
      double[] objs2 = new double[count];
      for (int i = 0, idx = 0; i < objects1.Length; i++)
        if (!IsEmpty(objects1[i]))
        {
          objs1.SetValue(objects1[i], idx);
          objs2[idx++] = objects2[i];
        }
      return new Array[] { objs1, objs2 };
    }
    #endregion Helpers
  }
}
