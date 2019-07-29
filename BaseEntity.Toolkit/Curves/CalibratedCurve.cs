/*
 * CalibratedCurve.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Linq;
using System.ComponentModel;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves.Bump;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Abstract base class for calibrated curves
  /// </summary>
  /// <remarks>
  ///   <para>Adds typical information required for calibration to simple Curve
  ///   class.</para>
  /// </remarks>
  [Serializable]
  public abstract class CalibratedCurve : Curve
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(CalibratedCurve));

    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Interpolation defaults to flat continuously compounded forward rates.
    ///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    ///   Compounding frequency is Continuous.</para>
    /// </remarks>
    ///
    /// <param name="asOf">As-of date</param>
    ///
    protected CalibratedCurve(Dt asOf)
      : this(new Curve(asOf, Frequency.Continuous))
    {
    }

    /// <summary>
    ///   Constructor for flat forward rate curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Interpolation defaults to flat continuously compounded forward rates.
    ///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    ///   Compounding frequency is Continuous.</para>
    /// </remarks>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="value">Forward rate for whole curve</param>
    ///
    protected CalibratedCurve(Dt asOf, double value)
      : this(new Curve(asOf, Frequency.Continuous, value))
    {
    }

    /// <summary>
    ///   Constructor given calibrator
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Interpolation defaults to flat continuously compounded forward rates.
    ///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    ///   Compounding frequency is Continuous.</para>
    /// </remarks>
    ///
    /// <param name="calibrator">Calibrator</param>
    ///
    protected CalibratedCurve(Calibrator calibrator)
      : this(new Curve(calibrator.AsOf, Frequency.Continuous), calibrator)
    {
    }

    /// <summary>
    ///   Constructor given calibrator and interpolation
    /// </summary>
    ///
    /// <param name="calibrator">Calibrator</param>
    /// <param name="interp">Interpolation method</param>
    /// <param name="dc">Daycount for interpolation</param>
    /// <param name="freq">Compounding frequency for interpolation</param>
    ///
    protected CalibratedCurve(Calibrator calibrator, Interp interp, DayCount dc, Frequency freq)
      : this(new Curve(calibrator.AsOf, interp, dc, freq), calibrator)
    {
    }

    /// <summary>
    /// Constructor of a standard overlay curve
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="freq">Frequency</param>
    /// <param name="overlay">Overlay curve</param>
    protected CalibratedCurve(Dt asOf, Frequency freq, Curve overlay)
      : this(new Curve(asOf, freq, overlay))
    {
    }

    /// <summary>
    /// Constructor of a standard overlay curve
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="interp">Interpolation</param>
    /// <param name="dayCount">Daycount</param>
    /// <param name="freq">Frequency</param>
    protected CalibratedCurve(Dt asOf, Interp interp, DayCount dayCount, Frequency freq)
      : this(new Curve(asOf, interp, dayCount, freq))
    {
    }

    /// <summary>
    /// Constructor to build a calibrated curve from a native curve
    /// </summary>
    /// <param name="nativeCurve">Native curve</param>
    /// <param name="calibrator">Calibrator</param>
    protected CalibratedCurve(Native.Curve nativeCurve,
      Calibrator calibrator = null) : base(nativeCurve)
    {
      tenors_ = new CurveTenorCollection();
      calibrator_ = calibrator;
      initDerivativesWrtQuotes_ = false;
    }

    /// <summary>
    ///   Clone object for CalibratedCurve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that the underlying calibrator is NOT cloned. References to dependent curves and parent curves are maintained</para>
    /// </remarks>
    ///
    public override object Clone()
    {
      var obj = (CalibratedCurve)base.Clone();
      obj.tenors_ = (CurveTenorCollection)tenors_.Clone();
      obj.initDerivativesWrtQuotes_ = false;
      obj.DependentCurves = CloneUtil.Clone(dependentCurves_);
      return obj;
    }

    /// <summary>
    ///   Clone CalibratedCurve and clone calibrator
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that the underlying calibrator IS cloned.</para>
    /// </remarks>
    ///
    public object CloneWithCalibrator()
    {
      var obj = (CalibratedCurve)Clone();
      if (calibrator_ != null)
        obj.calibrator_ = (Calibrator)calibrator_.Clone();
      obj.initDerivativesWrtQuotes_ = false;
      return obj;
    }

    #endregion Constructors
    
    #region Methods
    ///
    /// <summary>
    ///   Add product (tenor) to calibration
    /// </summary>
    ///
    /// <param name="product">Product to price for this curve tenor</param>
    /// <param name="marketPv">Market (observed) full price</param>
    /// <param name="coupon">Current coupon for floating rate securities (if necessary)</param>
    /// <param name="finSpread">Individual financing spread for this tenor</param>
    /// <param name="weight">Weighting for product</param>
    ///
    /// <returns>New curve tenor</returns>
    ///
    public CurveTenor Add(IProduct product, double marketPv, double coupon, double finSpread, double weight)
    {
      string name = product.Description;
      if (String.IsNullOrEmpty(name))
        name = String.Format("{0}", product.Maturity);
      var tenor = new CurveTenor(name, product, marketPv, coupon, finSpread, weight);
      tenors_.Add(tenor);
      initDerivativesWrtQuotes_ = false;
      return tenor;
    }

    ///
    /// <summary>
    ///   Add product (tenor) to calibration
    /// </summary>
    ///
    /// <param name="product">Product to price for this curve tenor</param>
    /// <param name="marketPv">Market (observed) full price</param>
    ///
    /// <returns>New curve tenor</returns>
    ///
    public CurveTenor Add(IProduct product, double marketPv)
    {
      initDerivativesWrtQuotes_ = false;
      return Add(product, marketPv, 0.0, 0.0, 1.0);
    }

    /// <summary>
    ///   Fit (calibrate) this curve to the specified market data.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Fits whole curve from scratch. After the curve has
    ///   been fitted, Refit may be called with care to improve
    ///   performance.</para>
    ///
    ///   <para>Clear the curves and calls FitFrom(0).</para>
    /// </remarks>
    ///
    public void Fit()
    {
      initDerivativesWrtQuotes_ = false;
      calibrator_.Fit(this);
      OriginalSpread = Spread; // record the spread used to fit the curve.
    }

    /// <summary>
    ///   Refit individual curve from the specified tenor point
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Care should be taken when using this method as it assumes
    ///   that the calibration has previously been run and may take
    ///   liberties and assumptions of pre-calculated values for speed.</para>
    ///
    ///   <para>Assumes nothing in the calibration has changed. If any
    ///   parameters have changed, Fit() should be called.</para>
    ///
    ///   <para>Does some validation and housekeeping then calls FitFrom(fromIdx).</para>
    /// </remarks>
    ///
    /// <param name="fromIdx">Index to start fit from</param>
    ///
    public void ReFit(int fromIdx)
    {
      if (logger.IsDebugEnabled)
      {
        logger.DebugFormat("Begin Refit {0}/{1} {2}", Name, Id, GetType());
      }
      RefitRecursive(this, Tenors, fromIdx);
      if (logger.IsDebugEnabled)
      {
        logger.DebugFormat("End Refit {0}/{1} {2}", Name, Id, GetType());
      }
    }

    private static void RefitRecursive(CalibratedCurve crv, CurveTenorCollection tenors, int fromIdx)
    {
      if (logger.IsDebugEnabled)
        logger.DebugFormat("Refit {0}/{1} {2}",
          crv.Name, crv.Id, crv.GetType());
      crv.initDerivativesWrtQuotes_ = false;
      double savedSpread = crv.Spread;
      try
      {
        crv.Spread = crv.OriginalSpread;
        crv.calibrator_.ReFit(crv, fromIdx);
      }
      finally
      {
        crv.Spread = savedSpread;
      }
      if (crv.dependentCurves_.Count <= 0)
        return;
      if(logger.IsDebugEnabled)
        logger.DebugFormat("Begin RefitRecursive {0}/{1} {2}",
          crv.Name,crv.Id, crv.GetType());
      foreach (var dc in crv.dependentCurves_.Values.ToList())
      {
        for (int i = 0; i < tenors.Count; i++)
        {
          // Update quotes which should move together.
          if (dc.Tenors.ContainsTenor(tenors[i].Name))
          {
            dc.Tenors[tenors[i].Name].SetQuote(tenors[i].CurrentQuote.Type,
                                               dc.Tenors[tenors[i].Name].OriginalQuote.Value +
                                               (tenors[i].CurrentQuote.Value - tenors[i].OriginalQuote.Value));
          }
        }
        RefitRecursive(dc, tenors, fromIdx);
      }
      if (logger.IsDebugEnabled)
        logger.DebugFormat("End RefitRecursive {0}/{1} {2}",
          crv.Name, crv.Id, crv.GetType());
    }

    /// <summary>
    ///   Price a product using the calibration pricing for this curve.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Care should be taken when using this method as it assumes
    ///   that the calibration has previously been run and may take
    ///   liberties and assumptions of pre-calculated values for speed.</para>
    /// </remarks>
    ///
    /// <param name="product">Product to price</param>
    ///
    public double Pv(IProduct product)
    {
      IPricer pricer = calibrator_.GetPricer(this, product);
      return pricer.Pv();
    }

    /// <summary>
    ///   Copy all data from another curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This function makes a shallow copy of both Tenors and Calibrator,
    ///   in addition to Curve.Set(), when the other curve is a Calibrated curve.
    ///   In sensivity calculations, this function makes the infomation of tenor products
    ///   (market premium and so on) in the bumped curves available to pricers.
    ///   </para>
    /// </remarks>
    ///
    /// <param name="curve">Curve to copy</param>
    ///
    /// <exclude />
    public virtual void Copy(Curve curve)
    {
      if (curve == this)
        return;
      Set(curve);
      this.Interp = curve.Interp;
      if (curve is CalibratedCurve)
      {
        var ccurve = (CalibratedCurve) curve;
        tenors_ = ccurve.tenors_;
        calibrator_ = ccurve.calibrator_;
        foreach (var dc in ccurve.DependentCurves)
        {
          CalibratedCurve cv;
          if (DependentCurves.TryGetValue(dc.Key, out cv)
            && !(cv.Calibrator is IndirectionCalibrator))
          {
            cv.Set(dc.Value);
            cv.Interp = dc.Value.Interp;
            cv.Tenors = dc.Value.Tenors;
            cv.Calibrator = dc.Value.Calibrator;
          }
          else if(logger.IsDebugEnabled)
          {
            logger.DebugFormat("Not found in clone: {0}/{1} {2}",
              dc.Value.Name, dc.Value.Id, dc.Value.GetType());
          }
        }
      }
      return;
    }

    /// <summary>
    /// Enumerates the component curves.
    /// </summary>
    /// <returns>IEnumerable&lt;CalibratedCurve&gt;.</returns>
    public virtual IEnumerable<CalibratedCurve> EnumerateComponentCurves()
    {
      return Calibrator?.EnumerateParentCurves();
    }

    /// <summary>
    ///   Find the first curve tenor on or after a given date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Finds the first curve tenor on or after <paramref name="date"/>.
    ///   If no tenor is on or after the date, the last tenor in the curve is returned.</para>
    /// </remarks>
    /// 
    /// <param name="date">date</param>
    /// 
    /// <returns>Curve tenor on or after specified date</returns>
    /// 
    public CurveTenor TenorAfter(Dt date)
    {
      if (Tenors == null || Tenors.Count <= 0)
        throw new ToolkitException("No curve tenor!");

      foreach (CurveTenor t in Tenors)
        if (t.Maturity >= date)
          return t;

      return Tenors[Tenors.Count - 1];
    }

    /// <summary>
    /// Initializes the coefficients of the system of equations satisfied respectively by the gradient and hessian of each of the curve ordinates w.r.t the market quotes of the products in 
    /// tenors_, used for calibration of the curve. In particular, let <m>q_i</m> be the <m>i^th</m> quote, the, once the curve is calibrated the following holds:
    /// <m>q_i = f_i(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))</m>, where the functions <m>f_i, i = 0,\dots,n </m> are known 
    /// (for instance, if <m>q_i</m> is the market spread of a swap on Libor, then
    /// <m>\\</m>
    ///  <m>f_i = \frac{FloatingLeg(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))}{FixedLeg(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))}</m> 
    /// <m>\\</m>
    /// <m>y_i(q_0,q_1, \dots, q_n)</m> are forward libor rates, that are function of the quotes since they have been calibrated to them. Taking derivatives, we obtain the following <m>n</m> systems, one for every <m>j</m>, 
    /// of n equations for <m>\partial_{q_j} y_i, i = 1, \dots,n :</m>
    /// <m>\\</m>
    /// <m>\partial_{q_j} q_i = \sum_k \partial_{y_k} f_i \partial_{q_j}y_k, i = 1,\dots,n.   </m>
    /// <m>\\</m>
    /// By differentiating again , we obtain the following <m>n(n+1)/2</m> systems of <m>n</m> equations for <m> \partial_{q_jq_k}y_i, i = 1, \dots, n</m>
    /// <m>
    /// \\
    /// </m>
    /// <m>\partial_{q_m q_n} q_i = \sum_{k,j} \partial_{y_k y_j} f_i (\partial_{q_m}y_k \partial_{q_n}y_j) + \sum_k \partial_{y_k}f_i \partial_{q_m q_n}y_k , i = 1,\dots,n. </m> 
    /// </summary>
    /// <param name="gradients">the vectors gradients[i] are filled with <m>\partial_{y_k}f_i \, k = 0,\dots,n</m>  </param>
    /// <param name="hessians">the vectors hessians[i] are filled with <m>\partial_{y_k y_j} f_i</m> with hessians[i][k*(k+1)/2 + j] = \partial_{y_k y_j} f_i </param>
    public virtual void InitializeDerivativesWrtQuotes(double[][] gradients, double[][] hessians)
    {
      throw new NotImplementedException("Not implemented for this CalibratedCurve type");
    }

    /// <summary>
    /// Solves the system of equations satisfied respectively by the gradient and hessian of each of the curve ordinates w.r.t the market quotes of the products in 
    /// tenors_, used for calibration of the curve. In particular, let <m>q_i</m> be the <m>i^th</m> quote, the, once the curve is calibrated the following holds:
    /// <m>q_i = f_i(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))</m>, where the functions <m>f_i, i = 0,\dots,n </m> are known 
    /// (for instance, if <m>q_i</m> is the market spread of a swap on Libor, then
    /// <m>\\</m>
    ///  <m>f_i = \frac{FloatingLeg(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))}{FixedLeg(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))}</m> 
    /// <m>\\</m>
    /// <m>y_i(q_0,q_1, \dots, q_n)</m> are forward libor rates, that are function of the quotes since they have been calibrated to them. Taking derivatives, we obtain the following <m>n</m> systems, one for every <m>j</m>, 
    /// of n equations for <m>\partial_{q_j} y_i, i = 1, \dots,n :</m>
    /// <m>\\</m>
    /// <m>\partial_{q_j} q_i = \sum_k \partial_{y_k} f_i \partial_{q_j}y_k, i = 1,\dots,n.   </m>
    /// <m>\\</m>
    /// By differentiating again , we obtain the following <m>n(n+1)/2</m> systems of <m>n</m> equations for <m> \partial_{q_jq_k}y_i, i = 1, \dots, n</m>
    /// <m>
    /// \\
    /// </m>
    /// <m>\partial_{q_m q_n} q_i = \sum_{k,j} \partial_{y_k y_j} f_i (\partial_{q_m}y_k \partial_{q_n}y_j) + \sum_k \partial_{y_k}f_i \partial_{q_m q_n}y_k , i = 1,\dots,n. </m> 
    /// </summary>
    /// <remarks> The system coefficients are the same for gradients and hessians, therefore we only need to compute the LU factorization of the matrix of system coefficients 
    /// once and then solve n times by backward substitution for the gradients and n*(n+1)/2 times for the hessians. </remarks>

    public virtual void EvaluateDerivativesWrtQuotes()
    {

      if (initDerivativesWrtQuotes_ == false)
      {
        int nt = tenors_.Count;
        int n = (nt > 0) ? nt : this.Count;
        gradientsWrtQuotes_ = new double[n][];
        hessiansWrtQuotes_ = new double[n][];
        var gradients = new double[n][];
        var hessians = new double[n][];
        var gradientRhs = new double[n][];
        var systemCoeffs = new double[n, n];
        var hessianRhs = new double[n * (n + 1) / 2][];
        for (int i = 0; i < n; i++)
        {
          gradientsWrtQuotes_[i] = new double[n];
          hessiansWrtQuotes_[i] = new double[n * (n + 1) / 2];
          gradients[i] = new double[n];
          hessians[i] = new double[n * (n + 1) / 2];
          gradientRhs[i] = new double[n];
        }
        for (int j = 0; j < n * (n + 1) / 2; j++)
          hessianRhs[j] = new double[n];
        if (nt == 0)
          return;
        InitializeDerivativesWrtQuotes(gradients, hessians);
        //Setting up system coefficients
        for (int i = 0; i < n; i++)
        {
          for (int j = 0; j < n; j++)
            systemCoeffs[i, j] = gradients[i][j];
        }
        double sum = 0;
        bool isLowerTriangular = false;
        for (int i = 0; i < n; i++)
          for (int j = i + 1; j < n; j++)
            sum += Math.Abs(systemCoeffs[i, j]);
        if (sum < 1e-8)
          isLowerTriangular = true;
        var piv = new int[n];
        if (!isLowerTriangular)
        {
          LinearSolvers.FactorizeLU(systemCoeffs,piv);
        }

        Parallel.For(0, n, delegate(int i)
                               {
                                 var x = new double[n];
                                 for (int j = 0; j < n; j++)
                                   gradientRhs[i][j] = (i == j) ? 1 : 0;
                                 if (isLowerTriangular)
                                 {
                                   LinearSolvers.SolveForward(systemCoeffs, gradientRhs[i], x);
                                 }
                                 else
                                 {
                                   LinearSolvers.SolveLU(systemCoeffs, piv, gradientRhs[i], x);
                                 }

                                 //Storing results in the correct order in gradientsWrtQuotes
                                 for (int j = 0; j < n; j++)
                                   gradientsWrtQuotes_[j][i] = x[j];
                               }
                               );

        //Solving systems for hessians wrt quotes and storing in hessiansWrtQuotes
        Parallel.For(0, n,
                     delegate(int i)
                     {
                       for (int j = 0; j <= i; j++)
                       {
                         for (int mm = 0; mm < n; mm++)
                         {
                           hessianRhs[i * (i + 1) / 2 + j][mm] = 0;
                           int kk = 0;
                           for (int ii = 0; ii < n; ii++)
                           {
                             for (int jj = 0; jj <= ii; jj++)
                             {
                               hessianRhs[i * (i + 1) / 2 + j][mm] -= (ii == jj)
                                                                      ? hessians[mm][kk] *
                                                                        (gradientsWrtQuotes_[ii][i
                                                                             ] *
                                                                         gradientsWrtQuotes_[jj][j
                                                                             ])
                                                                      : hessians[mm][kk] *
                                                                        (gradientsWrtQuotes_[ii][i
                                                                             ] *
                                                                         gradientsWrtQuotes_[jj][j
                                                                             ] +
                                                                         gradientsWrtQuotes_[ii][j
                                                                             ] *
                                                                         gradientsWrtQuotes_[jj][i
                                                                             ]);
                               kk++;
                             }
                           }
                         }
                         var x = new double[n];
                         if (isLowerTriangular)
                         {
                           LinearSolvers.SolveForward(systemCoeffs, hessianRhs[i * (i + 1) / 2 + j], x);
                         }
                         else
                         {
                           LinearSolvers.SolveLU(systemCoeffs, piv, hessianRhs[i * (i + 1) / 2 + j], x);
                         }
                         for (int jj = 0; jj < n; jj++)
                           hessiansWrtQuotes_[jj][i * (i + 1) / 2 + j] = x[jj];
                       }
                     });
        initDerivativesWrtQuotes_ = true;
      }
    }

    /// <summary>
    ///  The effective date (included for inflation zero curves).
    /// </summary>
    /// <value>The spot calendar.</value>
    public virtual Dt GetCurveDate(Dt date)
    {
      return date;
    }

    #endregion Methods

    #region Curve Shifts
    public CurveShifts CurveShifts
    {
      get { return curveShifts_; }
      set { curveShifts_ = value; }
    }
    public Curve ShiftOverlay
    {
      get { return shiftOverlay_; }
      set { shiftOverlay_ = value; }
    }

    [NonSerialized, Mutable] private Curve shiftOverlay_;
    [NonSerialized, Mutable] private CurveShifts curveShifts_;
    #endregion

    #region Properties

    /// <summary>
    ///   ArrayList of curve tenor points
    /// </summary>
    [Category("Base")]
    public CurveTenorCollection Tenors
    {
      get { return tenors_; }
      set { tenors_ = value; }
    }

    /// <summary>
    ///   Calibrator for this curve
    /// </summary>
    [Category("Base")]
    public Calibrator Calibrator
    {
      get { return calibrator_; }
      set { calibrator_ = value; }
    }

    /// <summary>
    ///   Convert to string
    /// </summary>
    public override string ToString()
    {
      // return calibrator_.ToString() + tenors_.ToString() + base.ToString();
      return Name 
        + (Calibrator != null ? ("[" + Calibrator.GetType().Name + "]") : "")
          + base.ToString() + tenors_.ToString();
    }

    /// <summary>
    /// 
    /// Accessor for the gradients of the curve's ordinates w.r.t the given market quotes. 
    /// These are stored in a jagged array of n arrays of dimension n, where n is the number of tenors in the curve.
    /// If we let <m>\{y_0, y_1, \dots, y_n\}</m> be the calibrated curve's ordinates, and
    /// <m>\{q_0, q_1, \dots, q_n\}</m> are the corresponding market quotes, then GradientsWrtQuotes[i][j] gives
    /// <m>\frac{\partial_{y_i}}{\partial_{q_j}}</m>  
    /// </summary>
    public double[][] GradientsWrtQuotes
    {
      get { return gradientsWrtQuotes_; }
      set { gradientsWrtQuotes_ = value; }
    }

    /// <summary>
    ///  Accessor for the hessians of the curve's ordinates w.r.t the given market quotes. 
    /// These are stored in a jagged array of n arrays of dimension n*(n+1)/2, where n is the number of tenors in the curve.
    /// If we let <m>\{y_0, y_1, \dots, y_n\}</m> be the calibrated curve's ordinates, and
    /// <m>\{q_0, q_1, \dots, q_n\}</m> are the corresponding market quotes, then HessiansWrtQuotes[i][k] gives
    /// <m>\frac{\partial^2_{y_i}}{\partial_{q_j}\partial_{q_m}}</m> with <m>k = n j + m</m>, <m>j = 0 \dots n</m>
    /// and <m>m \leq j </m>  
    /// </summary>
    public double[][] HessiansWrtQuotes
    {
      get { return hessiansWrtQuotes_; }
      set { hessiansWrtQuotes_ = value; }
    }

    /// <summary>
    /// True if the derivatives w.r.t to quotes have been initialized
    /// </summary>
    public bool InitDerivativesWrtQuotes
    {
      get { return initDerivativesWrtQuotes_; }
      set { initDerivativesWrtQuotes_ = value; }
    }


    /// <summary>
    /// Accessor for dependent curves
    /// </summary>
    [Browsable(false)]
    public Dictionary<long, CalibratedCurve> DependentCurves
    {
      get { return dependentCurves_; }
      set { dependentCurves_ = value; }
    }


    /// <summary>
    /// Accessor for dependent curves
    /// </summary>
    public CalibratedCurve[] DependentCurveList
    {
      get { return dependentCurves_.Values.ToArray(); }
    }

    /// <summary>
    /// Reference index to which the curve is attached(null if no reference index is used for calibration)
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; set; }

    /// <summary>
    ///  Number of business days between the trade date and the curve as-of date.
    /// </summary>
    /// <value>The spot days.</value>
    public int SpotDays { get; set; }

    /// <summary>
    ///  The holiday calendar for spot days.
    /// </summary>
    /// <value>The spot calendar.</value>
    public Calendar SpotCalendar { get; set; }

    /// <summary>
    /// Gets or sets the original spread which should be used to refit the curve.
    /// </summary>
    /// <remarks>
    /// This property is set in the Fit method and used in the ReFit method.
    /// The user should not call Fit when a ReFit is intended.
    /// </remarks>
    /// <value>The original spread.</value>
    public double OriginalSpread { get; set; }
    #endregion Properties

    #region Data

    private CurveTenorCollection tenors_;
    private Calibrator calibrator_;

    //TODO: this should be removed.
    private Dictionary<long, CalibratedCurve> dependentCurves_
      = new Dictionary<long, CalibratedCurve>();
    
    [Mutable]
    private double[][] gradientsWrtQuotes_;
    [Mutable]
    private double[][] hessiansWrtQuotes_;
    /// <summary>
    /// True if the derivatives w.r.t to quotes have been initialized
    /// </summary>
    [Mutable]
    private bool initDerivativesWrtQuotes_;
    #endregion Data
  }

  interface ICalibratedCurveContainer
  {
    CalibratedCurve TargetCurve { get; }
  }
}