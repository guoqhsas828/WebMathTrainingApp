/*
 * HWTreeModel.cs
 *
 * 
 */

using System;
using System.Collections;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;


namespace BaseEntity.Toolkit.Models
{
  ///
  /// <summary>
  ///   Hull White Tree based pricer
  /// </summary>
  ///
  /// <remarks>
  ///   <para>See "The General Hull-White Model and Super Calibration,"  Aug 2000 </para>
  ///   <para>The IR diffusion process used is a log-normally mean-reverting process with time-varying mean reversions
  ///   and volatilities (Black-Karasinski process. It is used (along with a survival curve) to price (corporate) callable bonds 
  ///   (or options on bonds). 
  ///   </para>
  ///   <para>This model covers processes with dynamics described by
  ///   <formula>
  ///     dx_t = - \kappa(t) x_t ) dt + \sigma(t) dW_t 
  ///   </formula>
  ///  
  ///   where <formula inline="true">x_t = \log(r_t)</formula> or
  ///   even more general <formula inline="true">x_t = f(r_t)</formula> and
  ///   <formula inline="true">r_t</formula> is the short interest rate</para>
  ///
  ///   <para>Requires as inputs a term structure of values (<formula inline="true">\kappa(t)</formula>)
  ///   for a grid of maturities) and associated volatilities(<formula inline="true">\sigma(t)</formula>). 
  ///   The current implementation however only works with single(constant) mean reversion and 
  ///   short-rate volatility (sigma)
  ///   </para>
  /// </remarks>
  ///
  [Serializable]
  public abstract class HWTreeModel : BaseEntityObject
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(HWTreeModel));

    #region Constructors
    
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="meanReversion">Short rate (constant) mean-reversion </param>
    /// <param name="sigma">Short rate (constant) volatility</param>
    /// <param name="timeGrid">Time Grid</param>
    /// <param name="timeGridDates">Time Grid</param>
    /// <param name="diffusionType">Normal or log-normal diffusion process</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// 
    public HWTreeModel(Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, double meanReversion, double sigma,
      double[] timeGrid, Dt[] timeGridDates, string diffusionType,
      double recoveryRate)
    {
      // Set data, using properties to include validation
      DiscountCurve = discountCurve;
      SurvivalCurve = survivalCurve;
      VolatilityCurve = new VolatilityCurve(asOf, sigma); // build flat vol curve
      MeanReversionCurve = new Curve(asOf, meanReversion);

      DiffusionProcess diffusionProcess = null;
      if (diffusionType == "HullWhite")
      {
        //throw new System.ArgumentOutOfRangeException(String.Format("Invalid Process: HW process not currently supported"));
        diffusionProcess = new HullWhiteProcess(meanReversion, sigma, discountCurve);
      }
      else if (diffusionType == "BlackKarasinski")
        diffusionProcess = new BlackKarasinskiProcess(meanReversion, sigma, discountCurve);
      else
        diffusionProcess = new HullWhiteProcess(meanReversion, sigma, discountCurve);
      
      recoveryRate_ = recoveryRate;
      
      // Tree Constructor
      hwTree_ = new GeneralizedHWTree(diffusionProcess, timeGrid,
        discountCurve, survivalCurve, settle, recoveryRate);
      hwTree_.TimeGridDates = timeGridDates;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    ///
    public override object Clone()
    {
      throw new ToolkitException("Not implemented yet");
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (this.discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Invalid discount curve. Cannot be null");
      if (this.meanReversionCurve_ == null)
        InvalidValue.AddError(errors, this, "MeanReversionCurve", "Invalid mean reversion curve. Cannot be null");
      if (this.volatilityCurve_ == null)
        InvalidValue.AddError(errors, this, "VolatilityCurve", "Invalid volatility curve. Cannot be null");

      return;
    }

    /// <summary>
    /// Adjust product values at current time slice
    /// </summary>
    /// <param name="values">The values at the current time slice.</param>
    /// <param name="index">Index of the current time slice.</param>
    public abstract void AdjustValues(double[] values, int index);

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Discount curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set { discountCurve_ = value; }
    }

    /// <summary>
    ///   Survival curve used for pricing. May be null.
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get { return survivalCurve_; }
      set { survivalCurve_ = value; }
    }


    /// <summary>
    ///   Recovery curve used for pricing
    /// </summary>
    public Curve MeanReversionCurve
    {
      get { return meanReversionCurve_; }
      set { meanReversionCurve_ = value; }
    }

    /// <summary>
    ///   Short Rate volatilities for building the tree and pricing.
    /// </summary>
    public VolatilityCurve VolatilityCurve
    {
      get { return volatilityCurve_; }
      set { volatilityCurve_ = value; }
    }

    ///
    /// <summary>
    ///   Time to node, in years (matching the mu/sigma inputs)
    /// </summary>
    public GeneralizedHWTree HWTree
    {
      get { return hwTree_; }
    }

    /// <summary>
    ///  Readonly recovery rate
    /// </summary>
    public double RecoveryRate
    {
      get { return recoveryRate_; }
      internal set { recoveryRate_ = value; }
    }
    #endregion Properties

    #region Data

    private DiscountCurve discountCurve_;
    private SurvivalCurve survivalCurve_;
    private Curve meanReversionCurve_;
    private VolatilityCurve volatilityCurve_;
    private readonly GeneralizedHWTree hwTree_;

    private double recoveryRate_ = -1.0;
    #endregion Data

  } // class HWTreeModel

}
