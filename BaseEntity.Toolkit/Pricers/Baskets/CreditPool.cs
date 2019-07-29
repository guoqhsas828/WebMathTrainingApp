/*
 * CreditPool.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///   A read-only pool of credits.
  ///   <preliminary>For internal use only.</preliminary>
  /// </summary>
  /// <exclude/>
  [Serializable]
  public class CreditPool : BaseEntityObject
  {
    #region Constructors and Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="CreditPool"/> class.
    /// </summary>
    /// <param name="participations">Participation weights.</param>
    /// <param name="survivalCurves">Credit curves.</param>
    /// <param name="asLcdsPool">
    ///   If true, treat this as a pool of LCDS and retrieve refinance information
    ///   from the survival curves; otherwise, as a regular CDS pool.</param>
    /// <param name="earlyMaturities">Early maturity dates by names (null if none).</param>
    public CreditPool(
      double[] participations,
      SurvivalCurve[] survivalCurves,
      bool asLcdsPool,
      Dt[] earlyMaturities)
      : this(participations, survivalCurves, null,
        null, null, asLcdsPool, earlyMaturities)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CreditPool"/> class.
    /// </summary>
    /// <param name="survivalCurves">Credit curves.</param>
    /// <param name="recoveryCurves">Recovery curves (null to use curves embedded in credit curves).</param>
    /// <param name="participations">Participation weights.</param>
    /// <param name="refinanceCurves">Refinance curves (null to use curves embedded in credit curves).</param>
    /// <param name="refinanceCorrelations">Refinance correlations (null to use correlations embedded in credit curves).</param>
    /// <param name="asLcdsPool">
    ///   If true, treat this as a pool of LCDS and retrieve refinance info
    ///   from the survival curves; otherwise, as a regular CDS pool.</param>
    /// <param name="earlyMaturities">Early maturity dates by names (null if none).</param>
    internal protected CreditPool(
      double[] participations,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      SurvivalCurve[] refinanceCurves,
      double[] refinanceCorrelations,
      bool asLcdsPool,
      Dt[] earlyMaturities)
    {
      survivalCurves_ = survivalCurves;
      recoveryCurves_ = recoveryCurves;
      participations_ = participations;
      refinanceCurves_ = refinanceCurves;
      refinanceCorrelations_ = refinanceCorrelations;
      maturities_ = earlyMaturities;
      asLcdsPool_ = asLcdsPool;
      Validate();
    }

    /// <summary>
    /// Return a new object that is a deep copy of this instance
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the lifecycle of the referenced object).
    /// </remarks>
    public override object Clone()
    {
      CreditPool obj = (CreditPool)base.Clone();
      obj.participations_ = ShallowClone(participations_);
      obj.survivalCurves_ = ShallowClone(survivalCurves_);
      obj.recoveryCurves_ = ShallowClone(recoveryCurves_);
      obj.maturities_ = ShallowClone(maturities_);
      obj.refinanceCorrelations_ = ShallowClone(refinanceCorrelations_);
      obj.refinanceCurves_ = ShallowClone(refinanceCurves_);
      return obj;
    }

    internal static T[] ShallowClone<T>(T[] a)
    {
      // Array are cloned with the elements copied by references.
      return a == null ? a : ((T[])a.Clone());
    }

    internal CreditPool DeepClone()
    {
      CreditPool obj = (CreditPool)base.Clone();
      obj.participations_ = CloneUtil.Clone(participations_);
      obj.survivalCurves_ = CloneUtil.Clone(survivalCurves_);
      obj.recoveryCurves_ = CloneUtil.Clone(recoveryCurves_);
      obj.maturities_ = CloneUtil.Clone(maturities_);
      obj.refinanceCorrelations_ = CloneUtil.Clone(refinanceCorrelations_);
      obj.refinanceCurves_ = CloneUtil.Clone(refinanceCurves_);
      return obj;
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      // Validate the survival curves
      if (survivalCurves_ == null || survivalCurves_.Length == 0)
      {
        InvalidValue.AddError(errors, this, "SurvivalCurves",
          "SurvivalCurves cannot be empty.");
        return;
      }
      int creditCount = survivalCurves_.Length;

      // Validate the participations
      if (participations_ == null || participations_.Length == 0)
        InvalidValue.AddError(errors, this, "Participations",
          "Participations cannot be empty.");
      else
      {
        int weightCount = creditCount *
          (participations_.Length / creditCount);
        if (participations_.Length != weightCount)
          InvalidValue.AddError(errors, this, "Participations",
            String.Format("Participations (len={0}) and credit count ({1}) not match",
            participations_.Length, creditCount));
      }

      // Validate other arrays
      if (recoveryCurves_ != null && recoveryCurves_.Length != creditCount)
        InvalidValue.AddError(errors, this, "RecoveryCurves",
          String.Format("RecoveryCurves (len={0}) and credit count ({1}) not match",
          recoveryCurves_.Length, creditCount));
      if (maturities_ != null && maturities_.Length != creditCount)
        InvalidValue.AddError(errors, this, "EarlyMaturities",
          String.Format("EarlyMaturities (len={0}) and credit count ({1}) not match",
          maturities_.Length, creditCount));
      if (refinanceCurves_ != null && refinanceCurves_.Length != creditCount)
        InvalidValue.AddError(errors, this, "RefinanceCurves",
          String.Format("RefinanceCurves (len={0}) and credit count ({1}) not match",
          refinanceCurves_.Length, creditCount));
      if (refinanceCorrelations_ != null && refinanceCorrelations_.Length != creditCount)
        InvalidValue.AddError(errors, this, "RefinanceCorrelations",
          String.Format("RefinanceCorrelations (len={0}) and credit count ({1}) not match",
          refinanceCorrelations_.Length, creditCount));

      // Valid basket type 
      if (refinanceCurves_ != null && !asLcdsPool_)
        throw new ArgumentException("AsPoolOfLcds must be true with nonempty refinance curves");

      return;
    }

    #endregion Constructors and Methods

    #region Properties

    /// <summary>
    ///  Number of credits in the pool.
    /// </summary>
    /// <value>The count.</value>
    public int CreditCount
    {
      get { return survivalCurves_.Length; }
    }

    /// <summary>
    /// Number of portfolios on the pool.
    /// </summary>
    /// <remarks>
    ///   A portfolio is defined as a set of participation weights,
    ///   one for each name.  Hence the total number of participation weights
    ///   equals the credit count times the portfolio count.
    /// </remarks>
    /// <value>The portfolio count.</value>
    public int PortfolioCount
    {
      get { return participations_.Length / survivalCurves_.Length; }
    }

    /// <summary>
    /// Gets the participation weights.
    /// </summary>
    /// <value>Participation weights.</value>
    public double[] Participations
    {
      get { return participations_; }
    }

    /// <summary>
    /// Gets the survival curves.
    /// </summary>
    /// <value>Survival curves.</value>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return survivalCurves_; }
    }

    /// <summary>
    /// Gets the early maturity dates.
    /// </summary>
    /// <value>The early maturity dates.</value>
    public Dt[] EarlyMaturities
    {
      get { return maturities_; }
    }

    /// <summary>
    /// Whether to treat this as a pool of LCDS.
    /// </summary>
    /// <value><c>true</c> if as a pool of LCDS; otherwise, <c>false</c>.</value>
    public bool AsPoolOfLCDS
    {
      get { return asLcdsPool_; }
    }

    /// <summary>
    /// Gets the recovery curves.
    /// </summary>
    /// <value>Recovery curves.</value>
    internal RecoveryCurve[] RecoveryCurves
    {
      get { return recoveryCurves_; }
    }

    /// <summary>
    /// Gets the refinance curves.
    /// </summary>
    /// <value>Refinance curves.</value>
    internal SurvivalCurve[] RefinanceCurves
    {
      get { return refinanceCurves_; }
    }

    /// <summary>
    /// Gets the refinance correlations.
    /// </summary>
    /// <value>Refinance correlations.</value>
    internal double[] RefinanceCorrelations
    {
      get { return refinanceCorrelations_; }
    }
    #endregion Properties

    #region Data

    // Survival curves and principals
    private double[] participations_;
    private SurvivalCurve[] survivalCurves_;

    // Recoveries
    //  (1) If recoveryCurves_ is not null, it is a user input;
    //  (2) Otherwise, we should extract recovery curves  from the survival curves.
    private RecoveryCurve[] recoveryCurves_;

    // Early maturities
    private Dt[] maturities_; // early maturity dates by names

    // Refinance data
    private bool asLcdsPool_;
    private SurvivalCurve[] refinanceCurves_; // refinance curves
    private double[] refinanceCorrelations_;  // refinance correlations

    #endregion Data

    #region Builder
    /// <summary>
    ///   Credit Pool builder, which creates a shallow copy of an
    ///   existing pool and allows modifying the underlying
    ///   pool through properties.
    /// </summary>
    internal class Builder
    {
      #region Methods and Data
      /// <summary>
      /// Initializes a new instance of the <see cref="Builder"/> class.
      /// </summary>
      /// <param name="pool">The pool.</param>
      public Builder(CreditPool pool)
      {
        basket_ = (CreditPool)pool.MemberwiseClone();
      }

      private CreditPool basket_;

      #endregion Methods and Data

      #region Properties

      /// <summary>
      /// Gets the result CreditPool.
      /// </summary>
      /// <value>Basket.</value>
      public CreditPool CreditPool
      {
        get
        {
          basket_.Validate();
          return (CreditPool)basket_.MemberwiseClone();
        }
      }

      /// <summary>
      /// Gets the count of credits in the pool.
      /// </summary>
      /// <value>The count.</value>
      public int CreditCount
      {
        get { return basket_.survivalCurves_.Length; }
      }

      /// <summary>
      ///  Get the number of portfolios.
      /// </summary>
      /// <value>The portfolio count.</value>
      public int PortfolioCount
      {
        get
        {
          return basket_.participations_.Length
            / basket_.survivalCurves_.Length;
        }
      }

      /// <summary>
      /// Gets/Sets the participation weights.
      /// </summary>
      /// <value>Participation weights.</value>
      public double[] Participations
      {
        get { return basket_.participations_; }
        set { basket_.participations_ = value; }
      }

      /// <summary>
      /// Gets/Sets the survival curves.
      /// </summary>
      /// <value>Survival curves.</value>
      public SurvivalCurve[] SurvivalCurves
      {
        get { return basket_.survivalCurves_; }
        set { basket_.survivalCurves_ = value; }
      }

      /// <summary>
      /// Gets/Sets the recovery curves.
      /// </summary>
      /// <value>Recovery curves.</value>
      public RecoveryCurve[] RecoveryCurves
      {
        get { return basket_.recoveryCurves_; }
        set { basket_.recoveryCurves_ = value; }
      }

      /// <summary>
      /// Gets/Sets the refinance curves.
      /// </summary>
      /// <value>Refinance curves.</value>
      public SurvivalCurve[] RefinanceCurves
      {
        get { return basket_.refinanceCurves_; }
        set { basket_.refinanceCurves_ = value; }
      }

      /// <summary>
      /// Gets/Sets the refinance correlations.
      /// </summary>
      /// <value>Refinance correlations.</value>
      public double[] RefinanceCorrelations
      {
        get { return basket_.refinanceCorrelations_; }
        set { basket_.refinanceCorrelations_ = value; }
      }

      /// <summary>
      /// Gets/Sets the early maturity dates.
      /// </summary>
      /// <value>The early maturity dates.</value>
      public Dt[] EarlyMaturities
      {
        get { return basket_.maturities_; }
        set { basket_.maturities_ = value; }
      }

      /// <summary>
      /// Gets/Sets a value indicating whether this is a pool of LCDS.
      /// </summary>
      /// <value><c>true</c> if as a pool of LCDS; otherwise, <c>false</c>.</value>
      public bool AsPoolOfLCDS
      {
        get { return basket_.asLcdsPool_; }
        set { basket_.asLcdsPool_ = value; }
      }
      #endregion Properties
    }
    #endregion Builder
  }
}
