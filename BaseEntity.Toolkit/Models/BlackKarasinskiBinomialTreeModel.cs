/*
 * BlackKarasinskiBinomialTreeModel.cs
 *
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Black Karasinski short interest rate model
  /// </summary>
  [Serializable]
  public class BlackKarasinskiBinomialTreeModel : IBinomialShortRateTreeModel
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BlackKarasinskiBinomialTreeModel));

    #region Constructor

    /// <summary>
    ///  Constructor for BlackKarasinskiBinomialTreeModel
    /// </summary>
    /// <param name="kappa">Mean reversion speed</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="start">Start date of the interest rate binomial tree</param>
    /// <param name="maturity">Maturity of the tree</param>
    /// <param name="n">Number of layers of the tree</param>
    /// <param name="curve">Initial discount curve used to adjust rate tree</param>
    public BlackKarasinskiBinomialTreeModel(double kappa, double sigma, Dt start, Dt maturity, int n, DiscountCurve curve)
    {
      asOf_ = curve.AsOf;
      start_ = start;
      if (start_ < asOf_)
        start_ = asOf_;
      maturity_ = maturity;
      T_ = maturity_.ToDouble() - start_.ToDouble();
      n_ = n;
      
      // there're n+1 layers in the tree
      treeDates_ = new Dt[n_+1];
      dt_ = T_ / n_;

      // pop up the tree dates
      double doubleDate = start_.ToDouble();
      for (int i = 0; i <= n_; i++)
      {        
        treeDates_[i] = new Dt(doubleDate);
        doubleDate += dt_;
      }

      sqrtDt_ = Math.Sqrt(dt_);
      kappa_ = kappa;
      sigma_ = sigma;
      discountCurve_ = curve;
      CalcInitialDiscountFactors();
      CalcConditionalProbabilities();
      BuildStarTree();
      AdjustStarTree();
    }
    
    /// <summary>
    ///  Constructor for BlackKarasinskiBinomialTreeModel
    /// </summary>
    /// <param name="kappa">Mean reversion speed</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="T">Time to maturity of the tree</param>
    /// <param name="n">Number of layers of the tree</param>
    /// <param name="curve">Initial discount curve used to adjust rate tree</param>
    public BlackKarasinskiBinomialTreeModel(double kappa, double sigma, double T, int n, DiscountCurve curve)
    {
      T_ = T;
      n_ = n;
      asOf_ = curve.AsOf;
      treeDates_ = new Dt[n_+1];
      dt_ = T_/n_;
      
      // pop up the tree dates
      double doubleDate = asOf_.ToDouble();
      for(int i = 0; i <= n_; i++)
      {
        doubleDate += dt_;
        treeDates_[i] = new Dt(doubleDate);
      }

      sqrtDt_ = Math.Sqrt(dt_);
      kappa_ = kappa;
      sigma_ = sigma;
      discountCurve_ = curve;      
      CalcInitialDiscountFactors();
      CalcConditionalProbabilities();
      BuildStarTree();
      AdjustStarTree();
    }
    #endregion Constructor

    #region Methods
    private void BuildStarTree()
    {
      starTree_ = new List<double[]>();
      starTree_.Add(new double[]{0});

      double sigmaSqrtDt = sigma_*sqrtDt_;
      double kappaDt = kappa_*dt_;
      for (int k = 1; k <= n_; k++)
      {
        // Do integral using left end point
        starTree_.Add(new double[k + 1]);
        for (int l = 0; l <= k; l++)
        {
          double p = conditionalProbabilities_[k][l], q = 1 - p;
          double up = l >= k ? 0 : starTree_[k - 1][l];
          double dn = l < 1 ? 0 : starTree_[k - 1][l - 1];
          double val = p*((1 - kappaDt)*up - sigmaSqrtDt) +
                       q*((1 - kappaDt)*dn + sigmaSqrtDt);
          starTree_[k][l] = val;
        }
        // Do it again using average of left and right end points
        for (int l = 0; l <= k; l++)
        {
          double p = conditionalProbabilities_[k][l], q = 1 - p;
          double upLeft = (l >= k ? 0 : starTree_[k - 1][l]);
          double dnLeft = (l < 1 ? 0 : starTree_[k - 1][l - 1]);
          double upRight = (l >= k ? 0 : starTree_[k][l]);
          double dnRight = (l < 1 ? 0 : starTree_[k][l - 1]);
          double val = p*((1 - kappaDt/2)*upLeft - kappaDt/2*upRight - sigmaSqrtDt) +
                       q*((1 - kappaDt/2)*dnLeft - kappaDt/2*dnRight + sigmaSqrtDt);
          starTree_[k][l] = val;
        }
      }
    }

    /// <summary>
    /// Shift tree nodes for each time slice to match initial forward rates 
    /// </summary>
    private void AdjustStarTree()
    {
      // Initialize the qNodes and set up the first Q(0,0) = 1
      // Q(k, l) = price of zero coupon bond maturing at k if node (k, l) is reached
      qNodes_ = new List<double[]>();
      qNodes_.Add(new double[] {1.0}); 

      // Set the first beta to be logarithm of first forward rate
      beta_ = new double[n_];
      beta_[0] = initialForwardRates_[0];

      double qUp = 0, qDown = 0, starUp = 0, starDown = 0, val = 0;
      // Iteratively solve the beta and compute Q nodes
      for (int k = 1; k < n_; k++)
      {
        // Compute Q[k][] using beta[k-1]
        qNodes_.Add(new double[k + 1]);
        for (int l = 0; l <= k; l++)
        {
          qUp = l >= k ? 0 : qNodes_[k - 1][l];
          qDown = l <= 0 ? 0 : qNodes_[k - 1][l - 1];
          starUp = l >= k ? 0 : Math.Round(starTree_[k - 1][l], 9);
          starDown = l <= 0 ? 0 : Math.Round(starTree_[k - 1][l - 1], 9);
          val = qUp*0.5*Math.Exp(-dt_*Math.Exp(starUp)*beta_[k - 1]) +
                qDown*0.5*Math.Exp(-dt_*Math.Exp(starDown)*beta_[k - 1]);
          qNodes_[k][l] = val;
        }

        // set up solver for beta[k]
        double lower = 0, upper = 10;
        Brent rf = new Brent();
        rf.setToleranceX(1e-9);
        rf.setToleranceF(1e-9);

        Double_Double_Fn fn_ = (double x, out string str) =>
        {
          double s = 0;
          for (int m = 0; m <= k; m++)
          {
            double b = Math.Exp(starTree_[k][m]);
            s += qNodes_[k][m]*Math.Exp(-dt_*b*x);
          }
          str = null;
          return s - initialDiscoutnFactors_[k];
        };
        DelegateSolverFn solverFn_ = new DelegateSolverFn(fn_, null);
        double res = Double.NaN;
        bool findSolution = false;
        try
        {
          res = rf.solve(solverFn_, 0, lower, upper);
          if(!Double.IsNaN(res))
            findSolution = true;
        }
        catch (Exception)
        {
          ;
        }
        finally
        {
          if(findSolution)
            beta_[k] = res;
          else
          {
            throw new ToolkitException("Cannot fit Black-Karasinski rate tree to initial term structure");
          }
        }
      }
      // Adjust the starTree to hold the real forward rates
      discFactors_ = new List<double[]>();
      for (int k = 0; k < n_; k++)
      {
        double[] fac = new double[k+1];
        for (int l = 0; l <= k; l ++)
        {
          starTree_[k][l] = Math.Exp(starTree_[k][l]) * beta_[k];
          if (starTree_[k][l] > 1.0)
            starTree_[k][l] = 1.0;
          fac[l] = Math.Exp(-dt_*starTree_[k][l]);
        }
        // compute the discount factors
        discFactors_.Add(fac);
      }
      rateTreeDone_ = true;

      return;
    }

    /// <summary>
    ///  Static method calculates a binomial tree for the conditional probabilities:
    ///  <m>P_H</m> and <m>P_L</m>.
    ///  where<math>\begin{align}
    ///   P_H &amp;= \Pr\!\left( W_{k-1} = (m+1) \sqrt{dt} \mid W_k = m \sqrt{dt} \right)
    ///   \\ P_L &amp;= \Pr\!\left( W_{k-1} = (m-1) \sqrt{dt} \mid W_k = m \sqrt{dt} \right)
    ///  \end{align}</math> 
    ///  here <m>d t</m> is time interval, <m>\sqrt{d t}</m> is half interval
    ///  between adjacent nodes in a 
    ///  binomial tree of Brownian motion, <m>k = 0, 1, 2, \ldots</m>, is the time index,
    ///  and <m>m</m> is level of node. 
    /// </summary>
    /// <returns>A conditional probability tree</returns>
    private void CalcConditionalProbabilities()
    {
      conditionalProbabilities_ = new List<double[]>(n_+1);
      conditionalProbabilities_.Add(new double[] { 0.0 });
      for (int i = 1; i <= n_; i++)
      {
        double[] condProbs = new double[i + 1];
        for (int j = 0; j < i + 1; j++)
          condProbs[j] = (double)(i-j)/(double)i;
          conditionalProbabilities_.Add(condProbs);
      }
    }

    /// <summary>
    ///  Compute discount factors from initial term structure
    ///  used to determine the shifts to the log-rate tree
    /// </summary>
    private void CalcInitialDiscountFactors()
    {
      initialDiscoutnFactors_ = new double[n_];
      initialForwardRates_ = new double[n_];
      forwardRates_ = new double[n_];
      for(int i = 0; i < n_; i++)
      {
        initialForwardRates_[i] = discountCurve_.F(start_, treeDates_[i+1], DayCount.ActualActual, Frequency.Continuous);
        initialDiscoutnFactors_[i] = Math.Exp(-initialForwardRates_[i]*(i + 1)*dt_);
        forwardRates_[i] = (i == 0)
                              ? initialForwardRates_[i]
                              : discountCurve_.F(treeDates_[i], treeDates_[i + 1], DayCount.ActualActual,
                                                 Frequency.Continuous);                              
      }
      return;
    }
    
    /// <summary>
    ///  Clone a BlackKarasinskiBinomialTreeModel
    /// </summary>
    /// <returns>Copy of BlackKarasinskiBinomialTreeModel</returns>
    internal BlackKarasinskiBinomialTreeModel Clone()
    {
      BlackKarasinskiBinomialTreeModel cloneModel = new BlackKarasinskiBinomialTreeModel(
        this.Kappa, this.Sigma, this.Start, this.Maturity, this.N-1, this.DiscountCurve);
      return cloneModel;
    }

    /// <summary>
    ///  Clone the rate tree numbers
    /// </summary>
    /// <returns>Cloned interest binomial tree</returns>
    internal List<double[]> CloneRateTree()
    {
      var clonedRateTree = new List<double[]>();
      for(int i = 0; i < RateTree.Count; i++)
      {
        clonedRateTree.Add((double[])RateTree[i].Clone());
      }
      return clonedRateTree;
    }

    /// <summary>
    ///  Clone the discount factor tree
    /// </summary>
    /// <returns>Cloned discount factor binomial tree</returns>
    internal List<double[]> CloneDiscFacTree()
    {
      var clonedDiscFacTree = new List<double[]>();
      for (int i = 0; i < discFactors_.Count; i++)
      {
        clonedDiscFacTree.Add((double[])RateTree[i].Clone());
      }
      return clonedDiscFacTree;
    }
    /// <summary>
    ///  Update the RateTree with aRateTree 
    /// </summary>
    /// <param name="aRateTree">A rate tree</param>
    public void SetRateTree(IReadOnlyList<double[]> aRateTree)
    {
      RateTree = new List<double[]>();
      for (int i = 0; i < aRateTree.Count; i++ )
        RateTree.Add((double[])aRateTree[i].Clone());

      // also update the discount factor      
      if (discFactors_ == null)
        discFactors_ = new List<double[]>(n_);
      for (int k = 0; k < n_; k++)
      {
        if (discFactors_[k] == null)
          discFactors_[k] = new double[k + 1];        
        for (int l = 0; l <= k; l++)
        {
          discFactors_[k][l] = Math.Exp(-dt_ * RateTree[k][l]);
        }
      }

      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///  Get the upper and lower bound of interest rate tree
    /// </summary>
    public double[,] RatesBounds
    {
      get
      {
        var bounds = new double[2, n_];
        if (!rateTreeDone_)
        {
          BuildStarTree();
          AdjustStarTree();
        }
        for (int k = 0; k < n_; k++)
        {
          bounds[0, k] = starTree_[k][0];
          bounds[1, k] = starTree_[k][k];
        }
        return bounds;
      }
    }

    /// <summary>
    ///  Compute and return the constructed short rate tree
    /// </summary>
    /// <returns>Short rate for Black Karasinski model</returns>
    public List<double[]> GetRateTree()
    {
      if (!rateTreeDone_)
      {
        BuildStarTree();
        AdjustStarTree();
      }
      return starTree_;
    }

    /// <summary>
    ///  The mean reversion speed
    /// </summary>
    public double Kappa
    {
      get
      {
        return kappa_;
      }
    }
    
    /// <summary>
    ///  Volatility
    /// </summary>
    public double Sigma
    {
      get { return sigma_; }
    }

    /// <summary>
    ///  Get the start date of interest binomial tree
    /// </summary>
    public Dt Start
    {
      get { return start_; }
    }

    /// <summary>
    ///  Number of tree layers
    /// </summary>
    public int N
    {
      get { return n_+1; }
    }

    /// <summary>
    /// Life of the tree in years 
    /// </summary>
    public double T
    {
      get { return T_; }
    }

    /// <summary>
    /// Maturity of product
    /// </summary>
    public Dt Maturity
    {
     get { return maturity_; }
    }

    /// <summary>
    /// Time interval of the tree 
    /// </summary>
    public double DeltaT
    {
      get{ return dt_;}
    }

    /// <summary>
    /// 
    /// </summary>
    public List<double[]> RateTree
    {
      get 
      {
        if (starTree_ == null || !rateTreeDone_)
        {
          BuildStarTree();
          AdjustStarTree();
        }
        return starTree_;
      }
      set
      {
        starTree_ = value;
      }
    }
    /// <summary>
    ///  Get discount factor tree
    /// </summary>
    public List<double[]> DiscountFactorTree
    {
      get
      {
        if(starTree_ == null || !rateTreeDone_)
        {
          BuildStarTree();
          AdjustStarTree();
        }
        return discFactors_;
      }
    }
    /// <summary>
    ///  Initial discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
    }

    /// <summary>
    ///  Get the initial forward rates term structure
    /// </summary>
    public double[] InitialForwardrates
    {
      get
      {
        if(initialForwardRates_ == null)
          CalcInitialDiscountFactors();
        return initialForwardRates_;
      }
    }
    
    /// <summary>
    ///  Get the forward short rates for each time interval
    /// </summary>
    public double[] ForwardRates
    {
      get
      {
        if(forwardRates_ == null)
          CalcInitialDiscountFactors();
        return forwardRates_;
      }
    }
    
    /// <summary>
    ///  Get the initial discount factors
    /// </summary>
    public double[] InitialDiscountFactors
    {
      get
      {
        if(initialDiscoutnFactors_ == null)
          CalcInitialDiscountFactors();
        return initialDiscoutnFactors_;
      }
    }

    /// <summary>
    ///  Get the conditional moving probabilities 
    /// </summary>
    public List<double[]> ConditionalProbs
    {
      get
      {
        if(conditionalProbabilities_ == null)
          CalcConditionalProbabilities();
        return conditionalProbabilities_;
      }
    }

    #endregion Properties

    #region data
    // mean reverting speed for Balck Karasinski model
    private double kappa_ = 0;
    // interest rate volatility
    private double sigma_ = 0;
    // time to maturity
    private double T_ = 0;
    // asof date from discount curve
    private Dt asOf_ = Dt.Empty;
    // start date of the binomial tree
    private Dt start_ = Dt.Empty;
    // maturity of the binomial tree
    private Dt maturity_ = Dt.Empty;
    // number of time intervals in the binomial tree
    private int n_ = 0;
    // time interval
    private double dt_ = 0;
    // dates on each time step along the tree
    private Dt[] treeDates_ = null;
    // square root of time interval
    private double sqrtDt_ = 0;
    // tree nodes
    private List<double[]> starTree_ = null;
    private List<double[]> discFactors_ = null; 
    private List<double[]> qNodes_ = null;
    // beta (or theta) parameter to be adjusted to initial term structure
    private double[] beta_ = null;
    // indicator
    private bool rateTreeDone_ = false;
    // initial term structure
    private DiscountCurve discountCurve_ = null;
    // initial discount factors computed from ir curve
    private double[] initialDiscoutnFactors_ = null;
    // initial forward rates computed from ir curve
    private double[] initialForwardRates_ = null;
    private double[] forwardRates_ = null;
    private List<double[]> conditionalProbabilities_ = null;
    #endregion data

    #region IBinomialRateTreeModel members

    IReadOnlyList<double[]> IBinomialShortRateTreeModel.GetRateTree()
    {
      return RateTree;
    }

    IReadOnlyList<double[]> IBinomialShortRateTreeModel.GetDiscountFactorTree()
    {
      return DiscountFactorTree;
    }

    IBinomialShortRateTreeModel IBinomialShortRateTreeModel.BumpSigma(double bumpSize)
    {
      return new BlackKarasinskiBinomialTreeModel(
        Kappa, Sigma + bumpSize, Start,
        Maturity, N - 1, DiscountCurve);
    }
    #endregion
  }
}
