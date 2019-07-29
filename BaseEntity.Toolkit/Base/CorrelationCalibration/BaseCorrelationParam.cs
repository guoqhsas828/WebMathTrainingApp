using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{

    #region Model Interfaces
    /// <summary>
    ///   General interface for plug-in different basket models
    /// </summary>
    /// <exclude/>
    public interface IBasketModel
    {
      /// <summary>
      ///   Construct a pricing model from an input basket
      /// </summary>
      /// <param name="input">Input</param>
      /// <param name="param">Param</param>
      /// <returns></returns>
      BasketPricer CreateBasket(BasketPricer input, BaseCorrelationParam param);

      /// <summary>
      ///   Name of the object
      /// </summary>
      string Name { get; }
    }

    #endregion Model Interfaces

  /// <summary>
  ///   Base correlation parameter set
  /// </summary>
  /// 
  /// <remarks>
  ///   <para>The parameter set is used to as an aid to extend the arguments passed to the
  ///   base correlation calibration routines beyond the current XL limit.</para>
  /// </remarks>
  ///
  public class BaseCorrelationParam
  {
    #region Fields and Properties

    private CopulaType copulaType_ = CopulaType.ExtendedGauss;
    private Copula copula_;
    /// <exclude />
    private int dfCommon_ = 2;
    /// <exclude />
    public int DfIdiosyncratic = 2;
    /// <exclude />
    private int stepSize_ = 3;
    /// <exclude />
    private TimeUnit stepUnit_ = TimeUnit.Months;
    /// <exclude />
    private int quadraturePoints_ = 0;
    /// <exclude />
    private double accuracyLevel_ = 1E-5;
    /// <exclude />
    private double gridSize_ = 0.0;
    /// <exclude />
    private double toleranceF_ = 0.0;
    /// <exclude />
    private double toleranceX_ = 0.0;
    /// <exclude />
    private InterpMethod strikeInterp_ = InterpMethod.PCHIP;
    /// <exclude />
    private ExtrapMethod strikeExtrap_ = ExtrapMethod.Smooth;
    /// <exclude />
    private InterpMethod tenorInterp_ = InterpMethod.Linear;
    /// <exclude />
    private ExtrapMethod tenorExtrap_ = ExtrapMethod.Const;
    /// <exclude />
    private RecoveryCorrelationModel rcmodel_ = RecoveryCorrelationModel.Default;
    /// <exclude />
    public bool InterpOnFactors = false;
    /// <exclude />
    public double Min = 0.0;
    /// <exclude />
    public double Max = 2.0;
    /// <exclude />
    public int SampleSize = 10000;
    /// <exclude />
    public int Seed = 0;

    /// <exclude />
    public BaseCorrelationCalibrationMethod CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;
    /// <exclude />
    public BaseCorrelationStrikeMethod MappingMethod = BaseCorrelationStrikeMethod.Unscaled;
    /// <exclude />
    public BaseCorrelationMethod Method = BaseCorrelationMethod.ArbitrageFree;
    /// <exclude />
    public IBasketModel BasketModel = null;


    /// <exclude />
    private bool discardRecoveryDispersion_ = true;
    /// <exclude />
    private double overrideRecoveryrate_ = Double.NaN;

    /// <exclude/>
    private bool BottomUpCalibration = true;
    // Properties or alternative names

    /// <exclude/>
    public bool BottomUp
    {
      get { return BottomUpCalibration; }
      set { BottomUpCalibration = value; }
    }

    /// <exclude />
    public Copula Copula
    {
      get
      {
        if (copula_ == null)
        {
          if (copulaType_ == CopulaType.Gauss && Max > 1)
          {
            copulaType_ = CopulaType.ExtendedGauss;
            rcmodel_.ModelChoice.ExtendedCorreltion = true;
          }
          copula_ = new Copula(copulaType_, dfCommon_, DfIndividual);
        }
        return copula_;
      }
      set
      {
        copula_ = value;
        rcmodel_.ModelChoice.ExtendedCorreltion =
          (value.CopulaType == CopulaType.ExtendedGauss || Max > 1);
      }
    }

    /// <exclude />
    [Browsable(false)]
    public CopulaType CopulaType
    {
      get { return copulaType_; }
      set
      {
        copulaType_ = value;
        rcmodel_.ModelChoice.ExtendedCorreltion =
          (value == CopulaType.ExtendedGauss || Max > 1);
      }
    }

    /// <exclude />
    [Browsable(false)]
    public int DfCommon
    {
      get { return dfCommon_; }
      set { dfCommon_ = value; }
    }

    /// <exclude />
    [Browsable(false)]
    public int DfIndividual
    {
      get { return DfIdiosyncratic; }
      set { DfIdiosyncratic = value; }
    }

    /// <exclude />
    public int StepSize
    {
      get { return stepSize_; }
      set { stepSize_ = value; }
    }

    /// <exclude />
    public TimeUnit StepUnit
    {
      get { return stepUnit_; }
      set { stepUnit_ = value; }
    }

    /// <exclude />
    public int QuadraturePoints
    {
      get { return quadraturePoints_; }
      set { quadraturePoints_ = value; }
    }

    /// <exclude />
    public double AccuracyLevel
    {
      get { return accuracyLevel_; }
      set { accuracyLevel_ = value; }
    }

    /// <exclude />
    public double GridSize
    {
      get { return gridSize_; }
      set { gridSize_ = value; }
    }

    /// <exclude />
    public double ToleranceF
    {
      get { return toleranceF_; }
      set { toleranceF_ = value; }
    }

    /// <exclude />
    public double ToleranceX
    {
      get { return toleranceX_; }
      set { toleranceX_ = value; }
    }

    /// <exclude />
    public InterpMethod StrikeInterp
    {
      get { return strikeInterp_; }
      set { strikeInterp_ = value; }
    }

    /// <exclude />
    public ExtrapMethod StrikeExtrap
    {
      get { return strikeExtrap_; }
      set { strikeExtrap_ = value; }
    }

    /// <exclude />
    public InterpMethod TenorInterp
    {
      get { return tenorInterp_; }
      set { tenorInterp_ = value; }
    }

    /// <exclude />
    public ExtrapMethod TenorExtrap
    {
      get { return tenorExtrap_; }
      set { tenorExtrap_ = value; }
    }

    /// <exclude />
    [Browsable(false)]
    public InterpMethod StrikeInterpolation
    {
      get { return strikeInterp_; }
      set { strikeInterp_ = value; }
    }

    /// <exclude />
    [Browsable(false)]
    public ExtrapMethod StrikeExtrapolation
    {
      get { return strikeExtrap_; }
      set { strikeExtrap_ = value; }
    }

    /// <exclude />
    [Browsable(false)]
    public InterpMethod TenorInterpolation
    {
      get { return tenorInterp_; }
      set { tenorInterp_ = value; }
    }

    /// <exclude />
    [Browsable(false)]
    public ExtrapMethod TenorExtrapolation
    {
      get { return tenorExtrap_; }
      set { tenorExtrap_ = value; }
    }

    /// <exclude />
    public double MaxCorrelation
    {
      get { return Max; }
      set { Max = value; }
    }

    /// <exclude />
    public double MinCorrelation
    {
      get { return Min; }
      set { Min = value; }
    }

    /// <exclude />
    public BaseCorrelationMethod BCMethod
    {
      get { return Method; }
      set { Method = value; }
    }

    /// <exclude />
    public object Model
    {
      get
      {
        return BasketModel != null ? (object)BasketModel : (object)ModelType;
      }
      set
      {
        if (value is BasketModelType)
        {
          ModelType = (BasketModelType)value;
          BasketModel = null;
        }
        else if (value is IBasketModel)
        {
          ModelType = BasketModelType.Default;
          BasketModel = (IBasketModel)value;
        }
        else
          throw new ArgumentException("Invalid basket model: " + value.ToString());
      }
    }

    /// <exclude />
    public bool DiscardRecoveryDispersion
    {
      get { return discardRecoveryDispersion_; }
      set { discardRecoveryDispersion_ = value; }
    }

    /// <summary>
    ///   Whether to use correlated recovery model
    ///   (currently only works with semi-analytic model)
    ///   <preliminary/>
    /// </summary>
    public bool WithCorrelatedRecovery
    {
      get { return rcmodel_.ModelChoice.WithCorrelatedRecovery; }
      set { rcmodel_.ModelChoice.WithCorrelatedRecovery = value; }
    }

    /// <summary>
    ///   Correlated recovery model to use.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    public RecoveryCorrelationType QCRModel
    {
      get { return rcmodel_.ModelChoice.QCRModel; }
      set { rcmodel_.ModelChoice.QCRModel = value; }
    }

    /// <exclude />
    public double MaxRecovery
    {
      get { return rcmodel_.MaxRecovery; }
      set { rcmodel_.MaxRecovery = value; }
    }

    /// <exclude />
    public double MinRecovery
    {
      get { return rcmodel_.MinRecovery; }
      set { rcmodel_.MinRecovery = value; }
    }

    /// <exclude />
    internal RecoveryCorrelationModel RecoveryCorrelationModel
    {
      get { return rcmodel_; }
      set { rcmodel_ = value; }
    }

    /// <summary>
    ///   Basket model type.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    public BasketModelType ModelType
    {
      get { return rcmodel_.ModelChoice.BasketModel; }
      set { rcmodel_.ModelChoice.BasketModel = value; }
    }

    /// <exclude />
    [Browsable(false)]
    public bool WithRecoveryDispersion
    {
      get { return !discardRecoveryDispersion_; }
      set { discardRecoveryDispersion_ = !value; }
    }

    /// <exclude />
    [Browsable(false)]
    public double OverrideRecoveryRate
    {
      get { return overrideRecoveryrate_; }
      set { overrideRecoveryrate_ = value; }
    }

    #endregion Fields and Properties
  };

}
