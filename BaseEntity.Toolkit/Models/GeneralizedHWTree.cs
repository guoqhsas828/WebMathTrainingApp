/*
 * GeneralizedHullWhiteTree.cs
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.HullWhiteShortRates;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Models
{

  ///
  /// <summary>
  ///  Single factor Generalized Hull-White trinomial tree model.
  /// </summary>
  ///
  [Serializable]
  public class GeneralizedHWTree
  {

    #region Constructors

    /// <summary>
    /// Create a (Generalized) Hull-White tree (to be used for non-constant time steps)
    /// </summary>
    /// <param name="diffusionProcess">Diffusion Process</param>
    /// <param name="timeGrid">Time Grid (in years)</param>
    /// <param name="discountCurve">Discount Curve (used to calculate shifts)</param>
    /// <param name="survivalCurve">Survival Curve (for defaultable bonds)</param>
    /// <param name="treeStartDate">Tree Start Date (reference date)</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <returns>(Generalized) Hull-White tree</returns>
    public GeneralizedHWTree(
      DiffusionProcess diffusionProcess,
      double[] timeGrid,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      Dt treeStartDate,
      double recoveryRate)
    {
      // initialize tree
      treeStartDate_ = treeStartDate; // usually settle
      timeGrid_ = timeGrid;
      diffusionProcess_ = diffusionProcess;
      discountCurve_ = discountCurve;
      survivalCurve_ = survivalCurve;
      statePricesLimit_ = 0; // tracks the timeslice to which we have already computed stat prices
      x0_ = diffusionProcess_.X0(); // set (almost)always to zero;
      int nTimeSteps = TimeGrid.Length;
      k_ = new int[nTimeSteps + 1][]; // stores the node indices for each time step
      dx_ = new double[nTimeSteps + 1];// stores dx increments for each time step
      pu_ = new double[nTimeSteps][];
      pm_ = new double[nTimeSteps][];
      pd_ = new double[nTimeSteps][];
      r_ = new double[nTimeSteps + 1][];
      thetaShifts_ = new double[nTimeSteps + 1]; // stores calibrated shifts (means) of the Ornstein-Uhlenbeck process
      statePrices_ = new double[nTimeSteps][];
      statePrices_[0] = new double[1];
      statePrices_[0][0] = 1;
      recoveryRate_ = recoveryRate;
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Build the generalized trinomial tree by Hull-White algorithm
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Let <m>X(t)</m> be the state variable, 
    /// <m>0=t_0 \lt t_1 \lt \cdots \lt t_n</m> be a given set of time grids,
    /// <m>\Delta{t}_i = t_{i+1} - t_{i}</m>.
    /// </para>
    /// 
    /// <para>Let <m>J_i</m> be the set of integers representing the state space
    ///  at time <m>t_i</m>.
    /// For each <m>j \in J_i</m>, the corresponding state value is<math>
    ///   x_{i j} = x_0 + j\,\Delta{x}_i
    /// </math>where <m>\Delta{x}_i</m>, <m>i =1, \ldots, n</m>,
    ///  are a given sequence of the state grid sizes by time steps.
    /// </para>
    /// 
    /// <para>Let <m>m_{i j}</m> and <m>v^2_{i j}</m> be the conditional
    ///  expectation and variance, respectively<math>\begin{align}
    ///   m_{i j} &amp;\equiv \mathrm{E}[X(t_{i+1}) \mid X(t_i) = x_{i j}]
    ///   \\ v^2_{i j} &amp;\equiv \mathrm{Var}[X(t_{i+1}) \mid X(t_i) = x_{i j}]
    /// \end{align}</math>
    ///  We first find an integer <m>k_{i j}</m> such that <m>x_{i+1, k_{i j}}</m>
    ///  is closest to <m>m_{i j}</m> among all the possible <m>x_{i+1, j}</m>
    ///  with <m>j\,</m> ranging over all the integers.  The solution
    ///  is given by<math>
    ///    k_{i j} = \mathrm{integer}\left[
    ///     \frac{m_{i j} - x_0}{\Delta{x}_{i+1}} + 0.5
    ///   \right]
    /// </math>
    /// </para>
    /// 
    /// <para>Now we find the probabilities <m>p_{i j}^u</m>,
    ///  <m>p_{i j}^m</m> and <m>p_{i j}^d</m> of jumping from <m>j</m>
    ///  to the states <m>k_{i j}+1</m>, <m>k_{i j}</m> and <m>k_{i j} -1</m>,
    ///  respectively, to match both the mean <m>m_{i j}</m> and variance
    ///  <m>v^2_{i j}</m> simultaneously.
    ///  Define<math>
    ///   e_{i j} \equiv m_{i j} - x_{i+1, k_{i j}} = m_{i j} - x_0 - k_{i j}\,\Delta{x}_{i+1}
    /// </math><math>
    ///   \alpha_{i j} \equiv \frac{e_{i j}}{\Delta{x}_{i+1}}
    ///    = \frac{m_{i j} - x_0}{\Delta{x}_{i+1}} - k_{i j}
    /// </math>
    /// It can be shown that the required probabilities are given by
    /// <math>\begin{align}
    ///  p_{i j}^u &amp;= \frac{1}{2}\left[
    ///     \frac{v^2_{i j}}{\Delta{x}_{i+1}^2} + \alpha_{i j}^2 + \alpha_{i j}
    ///    \right]
    ///  \\ p_{i j}^d &amp;= \frac{1}{2}\left[
    ///     \frac{v^2_{i j}}{\Delta{x}_{i+1}^2} + \alpha_{i j}^2 - \alpha_{i j}
    ///    \right]
    ///  \\ p^m_{i j} &amp;= 1 - p^u_{i j} - p^d_{i j}
    /// \end{align}</math>
    ///  When <m>1 \leq 4 v^2_{i j}/\Delta{x}_{i+1}^2 \leq 5</m>,
    ///  all the probabilities are nonnegative since
    ///  <m>0.5 \leq \alpha_{i j} \leq 0.5</m> by construction.
    /// </para>
    /// 
    /// <para>The above formula work for many choices of <m>\Delta{x}_i</m>.
    ///  A special choice recommended by Hull and White is to pick
    ///  <m>\Delta{x}_{i+1} = \sqrt{3}\,\sigma_i</m>,
    ///  combining with the property <m>v^2_{i j} = \sigma^2_i</m>
    ///  of the BK model, produces the following formula<math>\begin{align}
    ///   p_{i j}^u &amp;= \frac{1}{6}\left[
    ///     1 + \frac{e_{i j}^2}{\sigma_i^2} + \frac{e_{i j}\sqrt{3}}{\sigma_i}
    ///   \right]
    ///  \\ p_{i j}^m &amp;= \frac{1}{3}\left[
    ///     2 - \frac{e_{i j}^2}{\sigma_i^2}
    ///   \right]
    ///  \\ p_{i j}^d &amp;= \frac{1}{6}\left[
    ///     1 + \frac{e_{i j}^2}{\sigma_i^2} - \frac{e_{i j}\sqrt{3}}{\sigma_i}
    ///   \right]
    /// \end{align}</math>which are used in our codes.
    /// </para>
    /// 
    /// <para>To build the tree for the full set of the time steps,
    ///  we construct the state space <m>J_i</m> recursively.<ul>
    ///  <li>Start with <m>J_0 = \{0\}</m>;</li>
    ///  <li>With <m>J_{i}</m> given, find <m>k_{i j}</m> for each <m>j \in J_i</m>.
    ///   Let <m>J_{i+1} = \{j: j_{\mathrm{min}} \leq j \leq j_{\mathrm{max}}\}</m>,
    ///   where<math>
    ///    j_{\mathrm{min}} \equiv \min_{j}\{k_{i j}\} - 1
    ///    ,\quad j_{\mathrm{max}} \equiv \max_{j}\{k_{i j}\} + 1
    ///   </math>
    ///   </li>
    /// </ul>
    /// Please note that in this construction, the state space <m>J_i</m> at time
    /// step <m>i</m> is defined through the jump target <m>k_{i-1,j}</m> at the
    /// previous step <m>i-1</m>.
    /// </para>
    /// </remarks>
    /// 
    /// <returns>Trinomial Tree</returns>
    public void BuildGeneralizedTree()
    {

      // Reset state prices limit
      statePricesLimit_ = 0; // tracks the last (time) slice of already calculated state prices 

      int nTimeSteps = TimeGrid.Length;
      int jMin = 0;//botton node at given timestep: bottomNode
      int jMax = 0;//top node at given timestep: topNode
      dx_[0] = 0;

      for (int i = 0; i < nTimeSteps - 1; ++i)
      {
        double t = TimeGrid[i];
        double deltaT = TimeGrid[i + 1] - TimeGrid[i];
        double v2 = DiffusionProcess.Variance(t, 0, deltaT);
        double v = Math.Sqrt(v2);
        dx_[i + 1] = v * Math.Sqrt(3);
        double[] pu = new double[jMax - jMin + 1];
        double[] pm = new double[jMax - jMin + 1];
        double[] pd = new double[jMax - jMin + 1];
        int[] k = new int[jMax - jMin + 1];
        int count = 0;

        for (int j = jMin; j <= jMax; ++j)
        {
          double x = x0_ + j * dx_[i];
          double mij = DiffusionProcess.Mean(t, x, deltaT);
          double tmp2 = (mij - x0_) / dx_[i + 1];
          int sign = Math.Sign(tmp2);
          int test = (int)Math.Truncate(tmp2 + sign * 0.5);
          k[count] = (int)Math.Floor(Math.Abs((tmp2 + sign * 0.5) / sign)) * sign;

          double e = mij - (x0_ + k[count] * dx_[i + 1]);
          double e2 = e * e;
          double e3 = e * Math.Sqrt(3);

          pd[count] = (1 + e2 / v2 - e3 / v) / 6;
          pm[count] = (2 - e2 / v2) / 3;
          pu[count] = (1 + e2 / v2 + e3 / v) / 6;

          count = count + 1;
        } // end of j loop
        pd_[i] = pd;
        pm_[i] = pm;
        pu_[i] = pu;
        k_[i] = k;

        // update jmax and jmin for next time slice (topNode/minNode index at given time slice)
        jMin = GetArrayMin(k) - 1;
        jMax = GetArrayMax(k) + 1;
      }// end of i (time step) loop

      // should be truncating negative rates (not doing any truncation in current version)
      if (diffusionProcess_ is HullWhiteProcess)
      {
        CalcThetaShiftsForHW();
        // r(i,j) = x(i,j) + theta(i)
        // r(i,j) = ShortRate(i,j)
        // d(i,j) = exp(-r(i,j)*deltaT(i)
      }
      else if (diffusionProcess_ is BlackKarasinskiProcess)
      {
        CalcThetaShiftsForBK();
        // r(i,j) = exp(x(i,j) + theta(i))
        // r(i,j) = ShortRate(i,j)
        // d(i,j) = exp(-r(i,j)*deltaT(i)
      }

      // construct probSliceDefault
      if (SurvivalCurve != null)
      {
        ProbSliceDefault = new double[nTimeSteps];
        double sp0 = SurvivalCurve.SurvivalProb(TimeGridDates[0]);
        if (sp0 < Double.Epsilon) sp0 = 1.0;
        for (int i = 0; i < nTimeSteps - 1; ++i)
        {
          double sp = SurvivalCurve.SurvivalProb(TimeGridDates[i + 1]);
          if (sp < Double.Epsilon) sp = 1.0;
          ProbSliceDefault[i] = 1 - sp / sp0;
          sp0 = sp;
        }
      }

    }

    /// <summary>
    ///   Descendant Node 
    /// </summary>
    /// 
    /// <param name="i">Time Index</param>
    /// <param name="index">Rate Index</param>
    /// <param name="branch">Branch(0 - lower, 1- middle, 2-upper)</param>
    ///
    /// <returns>Descendant Node</returns>
    public int Descendant(int i, int index, int branch)
    {
      int jMin = (int)GetArrayMin(k_[i]) - 1;
      int[] k = k_[i];
      int descendant = k[index] - jMin - 1 + branch;
      return descendant;
    }

    /// <summary>
    ///   Probability of a given node 
    /// </summary>
    /// 
    /// <param name="i">Time Index</param>
    /// <param name="index">Rate Index</param>
    /// <param name="branch">Branch Index(0 - lower, 1- middle, 2-upper)</param>
    ///
    /// <returns>Probability(pu, pm or pd)</returns>
    public double Probability(int i, int index, int branch)
    {
      if (branch == 0) // down branch
        return pd_[i][index];
      else if (branch == 1) // middle branch
        return pm_[i][index];
      else if (branch == 2) // up branch
        return pu_[i][index];
      else
        throw new ArgumentException("Invalid branch index. Has to be 0(d) ,1(m) or 2(u)");
    }

    /// <summary>
    ///   Number of nodes at given time slice 
    /// </summary>
    /// 
    /// <param name="i">Time Index</param>
    ///
    /// <returns>Number of nodes at time slice Ti</returns>
    public int Size(int i)
    {
      if (i == 0) // down branch
        return 1;
      int[] k = k_[i - 1];
      int jMin = GetArrayMin(k) - 1;
      int jMax = GetArrayMax(k) + 1;
      return (jMax - jMin + 1);
    }

    /// <summary>
    ///   Underlying x value(at given node in the tree)
    /// </summary>
    /// 
    /// <param name="i">Time Index</param>
    /// <param name="index">Rate Index</param>
    ///
    /// <returns>Size of State Prices Array</returns>
    public double Underlying(int i, int index)
    {
      if (i == 0)
        return x0_;
      int[] k = k_[i - 1];
      int jMin = GetArrayMin(k) - 1;
      double underlying = x0_ + (jMin + index) * dx_[i];
      return underlying;
    }

    /// <summary>
    ///   Discount factor at given node in the tree.
    /// </summary>
    /// 
    /// <remarks>It's the discount factor over a [Ti+1 - Ti] period: exp(-rij * [Ti+1 - Ti])</remarks>
    /// 
    /// <param name="i">Time Index</param>
    /// <param name="index">Rate Index</param>
    ///
    /// <returns>Size of State Prices Array</returns>
    public double DiscountF(int i, int index)
    {
      double x = Underlying(i, index);
      double deltaT = TimeGrid[i + 1] - TimeGrid[i];
      double t = TimeGrid[i];
      double r = ShortRate(t, x);
      double df = Math.Exp(-r * deltaT);
      return df;
    }

    /// <summary>
    ///   Compute State Prices (from statePricesLimit_ to given time slice)
    /// </summary>
    /// 
    /// <param name="until_idx">Time Index</param>
    ///
    public void ComputeStatePrices(int until_idx)
    {
      for (int i = statePricesLimit_; i < until_idx; ++i)
      {
        double[] statePrices = new double[Size(i + 1)];
        for (int j = 0; j < Size(i); ++j)
        {
          double df = DiscountF(i, j);
          double[] tmp = statePrices_[i];
          double statePrice = tmp[j];
          for (int l = 0; l < 3; ++l)
          {
            int desc = Descendant(i, j, l);
            statePrices[desc] = statePrices[desc] + statePrice * df * Probability(i, j, l);
          }
        }
        statePrices_[i + 1] = statePrices;
      }
      statePricesLimit_ = until_idx;
    }

    /// <summary>
    ///   State Prices at given time slice
    /// </summary>
    /// 
    /// <param name="i">Time Index</param>
    ///
    /// <returns>State price at time step i</returns>
    ///
    public double[] StatePricesAtSlice(int i)
    {
      if (i > statePricesLimit_)
        ComputeStatePrices(i);
      return statePrices_[i];
    }

    /// <summary>
    ///   Calculate Theta Shifts (to match zero coupon bond prices)
    ///   for Hull-White diffusion process
    /// </summary>
    /// 
    ///
    public void CalcThetaShiftsForHW()
    {
      int nTimeSteps = TimeGrid.Length;
      thetaShifts_ = new double[nTimeSteps - 1];

      double startDf = discountCurve_.DiscountFactor(TreeStartDate);
      for (int i = 0; i < nTimeSteps - 1; ++i)
      {
        double t = TimeGrid[i + 1];
        double deltaT = TimeGrid[i + 1] - TimeGrid[i];

        // Extract discount factor for the give time on the grid (from discount curve)
        Dt date = new Dt(TreeStartDate, t);
        double discountBond = discountCurve_.DiscountFactor(date)/startDf;

        double[] tmpStatePrices = StatePricesAtSlice(i);
        int tmpSize = Size(i); // no. nodes at time slice i
        double dx = dx_[i];
        double x = Underlying(i, 0); // x =  x0_ + jMin  * dx;
        double value = 0;
        for (int j = 0; j < tmpSize; ++j)
        {
          value = value + tmpStatePrices[j] * Math.Exp(-x * deltaT);
          x = x + dx;
        }
        value = (Math.Log(value / discountBond)) / deltaT;
        thetaShifts_[i] = value;

        // fill in IR rates
        r_[i] = new double[tmpSize];
        for (int j = 0; j < tmpSize; ++j)
        {
          r_[i][j] = ShortRate(TimeGrid[i], Underlying(i, j));
        }
      }
    }

    /// <summary>
    ///   Calculate Theta Shifts (to match zero coupon bond prices)
    ///   for Black-Karasinski diffusion process
    /// </summary>
    /// 
    ///
    public void CalcThetaShiftsForBK()
    {
      int nTimeSteps = TimeGrid.Length;
      thetaShifts_ = new double[nTimeSteps - 1];

      // Set up root finder
      Brent rf = new Brent();
      const double thetaMin = -100;
      const double thetaMax = 100;
      rf.setToleranceF(accuracyBK_);
      rf.setToleranceX(accuracyBK_);
      rf.setLowerBounds(thetaMin);
      rf.setUpperBounds(thetaMax);
      rf.setInitialPoint(0.0);

      double startDf = discountCurve_.DiscountFactor(TreeStartDate);
      double lastDf = 1.0;
      for (int i = 0; i < nTimeSteps - 1; ++i)
      {
        double t = TimeGrid[i + 1];
        double deltaT = TimeGrid[i + 1] - TimeGrid[i];
        int tmpSize = Size(i); // no. nodes at time slice i

        // Extract discount factor for the give time on the grid (from discount curve)
        Dt date = new Dt(TreeStartDate, t);
        double discountBond = discountCurve_.DiscountFactor(date)/startDf;

        // Check if the forward rate for this time grid is positive.
        if (discountBond < lastDf)
        {
          // For positive forward rates, it is possible to solve for a theta shift.
          double[] tmpStatePrices = StatePricesAtSlice(i);
          double dx = dx_[i];
          double x = Underlying(i, 0); // x =  x0_ + jMin  * dx;

          // Set up solver function
          SolverFn fn = new ThetaFittingFn(discountBond, x, dx, tmpSize, tmpStatePrices, deltaT);

          // Solve
          thetaShifts_[i] = rf.solve(fn, 0, thetaMin, thetaMax);
          lastDf = discountBond;
        }
        else
        {
          // For negative forward rates, there is no solution.
          // We simply set the theta shift to be a large negative value,
          // which implies a short rate very close to zero.
          thetaShifts_[i] = thetaMin;
        }
        // fill in IR rates
        r_[i] = new double[tmpSize];
        for (int j = 0; j < tmpSize; ++j)
        {
          r_[i][j] = ShortRate(TimeGrid[i], Underlying(i, j));
        }
      }
    }


    /// <summary>
    ///   Set Theta Shift value at time slice i
    ///  
    /// </summary>
    /// 
    /// <param name="i">Theta index</param>
    /// <param name="fittingValue">Fitting Value</param>
    ///
    public void SetFittedThetaShifts(int i, double fittingValue)
    {
      thetaShifts_[i] = fittingValue;
    }

    /// <summary>
    ///   Return Theta Shift at time slice i
    ///  
    /// </summary>
    /// 
    /// <param name="i">Theta index</param>
    /// 
    /// <returns>Theta Shift</returns>
    ///
    public double GetFittedThetaShifts(int i)
    {
      return thetaShifts_[i];
    }

    /// <summary>
    ///   Short Rate of diffusion process for tree
    /// </summary>
    ///
    /// <param name="t">time</param>
    /// <param name="x">x value</param>
    /// 
    /// <returns>Short Rate</returns>
    public virtual double ShortRate(double t, double x)
    {
      int nTimeSteps = TimeGrid.Length;
      double[] times = new double[nTimeSteps];
      for (int i = 0; i < nTimeSteps; ++i)
      {
        //times[i] = i*deltaT;
        times[i] = TimeGrid[i];
      }

      // find Index
      int index = FindIndex(times, t);

      double tmp = GetFittedThetaShifts(index);
      double shortRate = 0;
      if (diffusionProcess_ is HullWhiteProcess)
        shortRate = x + tmp;
      else if (diffusionProcess_ is BlackKarasinskiProcess)
        shortRate = Math.Exp(x + tmp);

      return shortRate;
    }

    /// <summary>
    ///   Step back in the tree
    /// </summary>
    ///
    /// <param name="i">time slice</param>
    /// <param name="valuesInSlice">(Product/Asset)values In Slice</param>
    /// 
    /// <returns>New Product/Asset Values (in rolled back tree slice</returns>
    public double[] StepBack(int i, double[] valuesInSlice)
    {
      double[] newValues = new double[Size(i)];
      for (int j = 0; j < Size(i); ++j)
      {
        double value = 0;
        for (int l = 0; l < 3; ++l)
        {
          value += Probability(i, j, l) * valuesInSlice[Descendant(i, j, l)];
        }

        if (SurvivalCurve != null)
        {
          double recoveryRate = RecoveryRate >= 0 ? RecoveryRate
            : SurvivalCurve.SurvivalCalibrator.RecoveryCurve.RecoveryRate(
              new Dt(TreeStartDate, TimeGrid[i]));
          value = DefaultAdjustedValue(
            value, DiscountF(i, j), ProbSliceDefault[i], recoveryRate);
        }
        else
        {
          value = value * DiscountF(i, j);
        }

        newValues[j] = value;

      }
      return newValues;
    }

    /// <summary>
    ///   Step back in the tree
    /// </summary>
    ///
    /// <param name="i">time slice</param>
    /// <param name="valuesInSlice">(Product/Asset)values In Slice</param>
    /// <param name="defaultAmtFn">Default amount function</param>
    /// 
    /// <returns>New Product/Asset Values (in rolled back tree slice</returns>
    private double[] StepBack(int i, double[] valuesInSlice,
      DefaultAmountFn defaultAmtFn)
    {
      double[] newValues = new double[Size(i)];
      for (int j = 0; j < Size(i); ++j)
      {
        double value = 0;
        for (int l = 0; l < 3; ++l)
        {
          value += Probability(i, j, l) * valuesInSlice[Descendant(i, j, l)];
        }

        if (defaultAmtFn != null && ProbSliceDefault != null)
        {
          double recovery = defaultAmtFn(i, j);
          value = DefaultAdjustedValue(
            value, DiscountF(i, j), ProbSliceDefault[i], recovery);
        }
        else
        {
          value = value * DiscountF(i, j);
        }

        newValues[j] = value;

      }
      return newValues;
    }

    private double DefaultAdjustedValue(double value, double discount,
      double probDflt, double recovery)
    {
      double avgDf = 1.0 - defaultTiming_ + defaultTiming_ * discount;
      return discount*(1 - probDflt)*value + avgDf*probDflt*recovery;
    }

    /// <summary>
    /// Roll Back in the tree
    /// </summary>
    /// <param name="treePricer">Tree Pricer</param>
    /// <param name="fromTime">The time from which to start.</param>
    /// <param name="values">The values to start with.</param>
    /// <param name="toTime">The destination time to roll back.</param>
    /// <returns>The values at the destination time</returns>
    public double[] RollBack(HWTreeModel treePricer,
      double fromTime, double[] values, double toTime)
    {
      if (fromTime > toTime)
      {
        // Find Index iFrom
        int iFrom = FindIndex(TimeGrid, fromTime);

        // Find Index  iTo
        int iTo = FindIndex(TimeGrid, toTime);

        for (int i = iFrom - 1; i >= iTo; --i)
        {
          
          values = StepBack(i, values);
          if (i >= iTo)
            treePricer.AdjustValues(values,i);
        }
      }
      return values;
    }

    /// <summary>
    /// Roll Back in the tree
    /// </summary>
    /// <param name="adjustValuesFn">The function to adjust values at the specified time slice.</param>
    /// <param name="from">The time grid index from which to start.</param>
    /// <param name="values">The values to start with.</param>
    /// <param name="to">The time grid index to roll back to.</param>
    /// <param name="defaultAmtFn">Default amount function</param>
    /// <returns>The values at the destination time</returns>
    public double[] RollBack(
      AdjustValuesFn adjustValuesFn,
      int from, double[] values, int to,
      DefaultAmountFn defaultAmtFn)
    {
      if (from > to)
      {
        for (int i = from - 1; i >= to; --i)
        {
          values = StepBack(i, values, defaultAmtFn);
          if (i > 0 && Observers[i].Enabled)
          {
            values = Observers[i].StepBackToObservation(this, i - 1, values);
          }
          adjustValuesFn(values, i);
        }
      }
      return values;
    }

    /// <exclude/>
    public delegate double DefaultAmountFn(int iStep, int iState);
    /// <exclude/>
    public delegate void AdjustValuesFn(double[] values, int iStep);
    #endregion

    #region Properties

    /// <summary>
    ///  Readonly recovery rate
    /// </summary>
    public double RecoveryRate
    {
      get { return recoveryRate_; }
    }
    /// <summary>
    ///  Time Grid array
    /// </summary>
    public double[] TimeGrid
    {
      get { return timeGrid_; }
      internal set { timeGrid_ = value;}
    }

    /// <summary>
    ///  Time Grid array
    /// </summary>
    public Dt[] TimeGridDates
    {
      get { return timeGridDates_; }
      internal set
      {
        if (value == null)
          throw new ArgumentException("Invalid Time Grid Dt Array. Cannot be null");
        timeGridDates_ = value;
      }
    }

    /// <summary>
    ///   Starting Value of diffusion process
    /// </summary>
    public double X0
    {
      get { return x0_; }
      set
      {
        if (value < 0)
          throw new ArgumentException("Invalid starting point. Cannot be < zero");
        x0_ = value;
      }
    }

    /// <summary>
    ///   Array of delta_x increments (constant for standard HW alg)
    /// </summary>
    public double[] DX
    {
      get { return dx_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid starting point. Cannot be null");
        dx_ = value;
      }
    }

    /// <summary>
    ///   Array of k's (index of nodes in a given slice)
    /// 
    /// </summary>
    public int[][] K
    {
      get { return k_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid (slice)nodes array. Cannot be null");
        k_ = value;
      }
    }

    /// <summary>
    ///   List of pu (up-branche probability) arrays (one array for each time step)
    /// </summary>
    public double[][] PU
    {
      get { return pu_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid pu list of arrays. Cannot be null");
        pu_ = value;
      }
    }

    /// <summary>
    ///   List of pm (middle-branche probability) arrays (one array for each time step)
    /// </summary>
    public double[][] PM
    {
      get { return pm_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid pm list of arrays. Cannot be null");
        pm_ = value;
      }
    }

    /// <summary>
    ///   List of pd (down-branche probability) arrays (one array for each time step)
    /// </summary>
    public double[][] PD
    {
      get { return pd_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid pd list of arrays. Cannot be null");
        pd_ = value;
      }
    }

    /// <summary>
    ///   State Prices (one for each node in the tree)
    /// </summary>
    public double[][] StatePrices
    {
      get { return statePrices_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid StatePrices list of arrays. Cannot be null");
        statePrices_ = value;
      }
    }

    /// <summary>
    ///   Diffusion Process
    /// </summary>
    public DiffusionProcess DiffusionProcess
    {
      get { return diffusionProcess_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid Diffusion. Cannot be null");
        diffusionProcess_ = value;
      }
    }

    /// <summary>
    ///   Theta Shifts (step 2 of HW algorithm = find (time-dependent) mean of diffusion procees
    /// </summary>
    public double[] ThetaShifts
    {
      get { return thetaShifts_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid ThetaShifts. Cannot be null");
        thetaShifts_ = value;
      }
    }

    /// <summary>
    ///   Default probability over each time slice <formula inline="true">Dp[T_{i-1}, T_i] = P[T_i] - P[T_{i-1}]</formula>
    /// </summary>
    public double[] ProbSliceDefault
    {
      get { return probSliceDefault_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid ProbSliceDefault. Cannot be null");
        probSliceDefault_ = value;
      }
    }

    /// <summary>
    ///   Discount Curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid DiscountCurve. Cannot be null");
        discountCurve_ = value;
      }
    }

    /// <summary>
    ///   Survival Curve
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get { return survivalCurve_; }
      set { survivalCurve_ = value; }
    }

    /// <summary>
    ///   State Prices Limit
    /// </summary>
    public int StatePricesLimit
    {
      get { return statePricesLimit_; }
      set
      {
        if (value < 0)
          throw new ArgumentException("Invalid StatePricesLimit. Cannot be < 0");
        statePricesLimit_ = value;
      }
    }

    /// <summary>
    ///   Tree Start Date (pricing settlement date usually)
    /// </summary>
    public Dt TreeStartDate
    {
      get { return treeStartDate_; }
      set { treeStartDate_ = value; }
    }

    internal double DefaultTiming
    {
      get{ return defaultTiming_;}
      set{ defaultTiming_ = value;}
    }

    internal TrinomialTreeObserver[] Observers { get; set; }

    #endregion //end Methods

    # region Helper

    /// <summary>
    ///  Min in Array 
    /// </summary>
    /// 
    /// <param name="arr">Array</param>
    ///
    /// <returns>Minimum array value</returns>
    public static int GetArrayMin(int[] arr)
    {
      int min = (int)Math.Pow(10, 5);
      for (int i = 0; i < arr.Length; ++i)
        if (arr[i] < min)
          min = arr[i];
      return min;
    }

    /// <summary>
    ///  Max in Array 
    /// </summary>
    /// 
    /// <param name="arr">Array</param>
    ///
    /// <returns>Minimum array value</returns>
    public static int GetArrayMax(int[] arr)
    {
      int max = ((int)Math.Pow(10, 5)) * (-1);
      for (int i = 0; i < arr.Length; ++i)
        if (arr[i] > max)
          max = arr[i];
      return max;
    }

    /// <summary>
    ///  Find Index in (sorted)array 
    /// </summary>
    /// 
    /// <param name="arr">Array</param>
    /// <param name="value">Value</param>
    ///
    /// <returns>Minimum array value</returns>
    public int FindIndex(double[] arr, double value)
    {
      int i = 0;
      if (value == arr[arr.Length - 1])
        return (arr.Length - 1);
      for (int j = 0; j < arr.Length; ++j)
      {
        if (value < arr[j])
        {
          i = j - 1;
          break;
        }
      }
      return i;
    }
    # endregion // Helper

    #region Data

    private double defaultTiming_ = 0.5;
    private double[] timeGrid_;
    private Dt[] timeGridDates_;
    private double x0_;
    private double[] dx_;
    private int[][] k_;
    private double[][] pu_;
    private double[][] pm_;
    private double[][] pd_;
    private double[][] statePrices_;
    private DiffusionProcess diffusionProcess_;
    private double[] thetaShifts_;
    private int statePricesLimit_;
    private DiscountCurve discountCurve_;
    private SurvivalCurve survivalCurve_;
    private Dt treeStartDate_;//, protectStartDate_;
    private double[] probSliceDefault_;
    private double[][] r_;
    private double recoveryRate_ = -1;

    private double accuracyBK_ = 1E-8;
    #endregion //Data

  } // class GHullWhiteTree


  #region Solvers

  /// <summary>
  ///   Theta Shift Solver for HW BK Tree
  /// </summary>
  /// 
  ///
  ///
  ///
  public class ThetaFittingFn : SolverFn
  {

    /// <summary>
    ///   Function Constructor 
    /// </summary>
    public ThetaFittingFn(double discountBond, double x, double dx, int sliceSize, double[] statePricesAtSlice, double deltaT)
    {
      discountBond_ = discountBond;
      x_ = x; //lowest node value in time slice
      dx_ = dx; //x -increment in slice
      statePricesAtSlice_ = statePricesAtSlice; // size of the stateprices vector = no. of nodes in (time)slice
      sliceSize_ = sliceSize;
      deltaT_ = deltaT;
    }

    /// <summary>
    ///   Evaluate Function 
    /// </summary>
    public override double evaluate(double theta)
    {

      // Calculate discount bond price based on stateprices
      double computedBondValue = 0;
      double x = x_; double dx = dx_;
      for (int j = 0; j < sliceSize_; ++j)
      {
        computedBondValue = computedBondValue + statePricesAtSlice_[j] * Math.Exp(-Math.Exp(theta + x) * deltaT_);
        x = x + dx;
      }
      double targetFcnValue = (discountBond_ - computedBondValue);

      return targetFcnValue;
    }

    private double discountBond_;
    private double x_;
    private double dx_;
    private double deltaT_;
    private double[] statePricesAtSlice_;
    private int sliceSize_;

  }

  #endregion

  /// <summary>
  /// Kind of diffusion process.
  /// </summary>
  public enum DiffusionProcessKind
  {
    /// <summary>
    ///  Hull-White process.
    /// </summary>
    HullWhite,
    /// <summary>
    ///  Black-Karasinski process.
    /// </summary>
    BlackKarasinski
  }

  ///
  /// <summary>
  ///  Diffusion process class 
  /// </summary>
  ///
  ///
  [Serializable]
  public class DiffusionProcess
  {

    #region Constructors

    /// <summary>
    ///   Constructor of a diffusion process
    /// </summary>
    ///
    /// <param name="meanReversion">Mean reversion parameter </param>
    /// <param name="sigma">Volatility</param>
    /// <param name="discountCurve">Discount Curve</param>
    ///
    /// <returns>DiffusionProcess</returns>
    /// 
    ///
    public DiffusionProcess(double meanReversion, double sigma, DiscountCurve discountCurve)
    {

      meanReversion_ = meanReversion;
      sigma_ = sigma;
      discountCurve_ = discountCurve;
      //trinomialTree_ = tree;
      //modelName_ = modelName;
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Mean value of diffusion process over a time increment
    /// </summary>
    ///
    /// <param name="t0">Starting time</param>
    /// <param name="x0">Starting point of diffusion process</param>
    /// <param name="deltaT">Time Interval</param>
    /// 
    /// <returns>Mean Value</returns>
    public double Mean(double t0, double x0, double deltaT)
    {
      return x0 * Math.Exp(-meanReversion_ * deltaT);
      //return x0 * (1 - meanReversion_ * deltaT);
    }

    /// <summary>
    ///   Variance of diffusion process over a time increment (time slice in the tree)
    /// </summary>
    ///
    /// <param name="t0">Starting time</param>
    /// <param name="x0">Starting point of diffusion process</param>
    /// <param name="deltaT">Time Interval</param>
    /// 
    /// <returns>Variance</returns>
    public double Variance(double t0, double x0, double deltaT)
    {
      if (sigma_ < 10e-6)
        sigma_ = 10e-6;

      if (meanReversion_ < 10e-6)
        return sigma_ * sigma_ * deltaT;
      else
        return 0.5 * sigma_ * sigma_ / meanReversion_ * (1 - Math.Exp(-2 * meanReversion_ * deltaT));
    }



    /// <summary>
    ///   x0 Starting value for process
    /// </summary>
    ///
    /// 
    /// <returns>x0</returns>
    public double X0()
    {
      return 0;
    }

    // add HullWhite, BK classes (which inherit diffsuion class) and overwrite only the shortRate function for the BK class
    #endregion

    #region Properties

    /// <summary>
    ///  Mean reversion parameter
    /// </summary>
    public double MeanReversion
    {
      get
      {
        return meanReversion_;
      }
    }

    /// <summary>
    ///  Volatility parameter
    /// </summary>
    public double Sigma
    {
      get
      {
        return sigma_;
      }
      set
      {
        sigma_ = value;
      }
    }

    /// <summary>
    ///  Term Structure
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get
      {
        return discountCurve_;
      }
    }

    #endregion

    #region Data

    private double meanReversion_;
    private double sigma_;
    private DiscountCurve discountCurve_;
    //private GHullWhiteTree trinomialTree_;
    //private string modelName;


    #endregion //Data

  } // class DiffusionProcess

  ///
  /// <summary>
  ///  Hull-White Diffusion process class 
  /// </summary>
  ///
  ///
  internal class HullWhiteProcess : DiffusionProcess
  {
    #region Constructors


    /// <summary>
    ///   Constructor for a Hull White Diffusion Process
    /// </summary>
    ///
    ///
    /// <param name="meanReversion">Mean reversion parameter </param>
    /// <param name="sigma">Volatility</param>
    /// <param name="discountCurve">DiscountCurve</param>
    ///
    /// <returns>Hull-White DiffusionProcess</returns>
    ///
    public
    HullWhiteProcess(double meanReversion, double sigma, DiscountCurve discountCurve)
      : base(meanReversion, sigma, discountCurve)
    {
    }
    #endregion // Constructors

    #region Methods

    #endregion

  } // class Hull-White DiffusionProcess

  ///
  /// <summary>
  ///  Black-Karasinski Diffusion process class 
  /// </summary>
  ///
  ///
  [Serializable]
  internal class BlackKarasinskiProcess : DiffusionProcess
  {
    #region Constructors


    /// <summary>
    ///   Constructor for a Hull White Diffusion Process
    /// </summary>
    ///
    ///
    /// <param name="meanReversion">Mean reversion parameter </param>
    /// <param name="sigma">Volatility</param>  
    /// <param name="discountCurve">DiscountCurve</param>
    ///
    /// <returns>Black-Karasinski DiffusionProcess</returns>
    ///
    public
    BlackKarasinskiProcess(double meanReversion, double sigma, DiscountCurve discountCurve)
      : base(meanReversion, sigma, discountCurve)
    {
    }
    #endregion // Constructors

    #region Methods

    #endregion

  } // class Black-Karasinski DiffusionProcess
  
}
