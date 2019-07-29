//
//   2017. All rights reserved.
//
using System;
using System.Diagnostics;
using System.Text;
using BaseEntity.Toolkit.Base;
using TrinomialTree = BaseEntity.Toolkit.Models.GeneralizedHWTree;
using static BaseEntity.Toolkit.Models.GeneralizedHWTree;

namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  /// <summary>
  ///  Inserts observation time points before the specified time nodes
  ///   of an existing Hull-White trinomial tree.
  ///  Used by callable bonds with notification days.
  /// </summary>
  /// 
  /// <remarks>
  /// <para>Let <m>X_t</m> be the state variables, <m>Y_t</m> be a
  /// state-dependent variable.  This class provides a way to 
  /// calculate the conditional expectation
  /// <m>\mathrm{E}[Y_t \mid X_{t'}]</m>
  /// for some <m>t' \lt t</m>, where <m>t</m> is a time node
  /// of the existing tree, and <m>t'</m> is not a node on the tree.</para>
  /// 
  /// <para>
  /// Assuming that only a single jump occurs from <m>t'</m> to <m>t</m>.
  /// Let <m>J_{t'}</m> and <m>J_t</m> represent the discrete state spaces
  /// at time <m>t'</m> and <m>t</m>, respectively.
  /// For each <m>j \in J_{t'}</m>,
  /// we find an appropriate state <m>k \in J_t</m> and calculate the
  /// probabilities <m>p_j^u</m>, <m>p_j^m</m> and <m>p_j^d</m> of jumping
  /// from <m>j</m> to the states <m>k+1</m>, <m>k</m> and <m>k-1</m>,
  /// respectively,
  /// based on the usual Hull-White trinomial tree building algorithm.
  /// </para>
  /// 
  /// <para>Let <m>y_k</m> be the value of <m>Y_t</m> in state <m>k</m>.
  /// Then the conditional expectations are given by<math>
  ///   \mathrm{E}[Y_t \mid X_{t'} = x_j]
  ///   = p_j^u\, y_{k+1} + p_j^m\,y_{k} + p_j^d\,y_{k-1}
  /// </math></para>
  /// 
  /// <para>Since <m>t'</m> is assumed close to <m>t</m> (only a few days apart),
  /// we pick the
  /// state space <m>J_{t'}</m> to be the same as <m>J_t</m>.
  /// Our approach amounts to calculate the changes of the probability
  /// distribution over the same state spaces between <m>t'</m> and <m>t</m>.</para>
  /// </remarks>
  public struct TrinomialTreeObserver
  {
    /// <summary>
    /// Initializes the observers on the specified tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="timeGrid">The time grid.</param>
    internal static void Initialize(
      GeneralizedHWTree tree, TimeGrid timeGrid)
    {
      var noticeDates = timeGrid.NoticeDates;
      var observers = new TrinomialTreeObserver[noticeDates.Length];
      for (int i = 1; i < noticeDates.Length; ++i)
      {
        Dt noticeDate = noticeDates[i];
        if (noticeDate.IsEmpty() || noticeDate== timeGrid.Dates[i])
          continue;
        observers[i].Initialize(noticeDate, tree, i);
      }
      tree.Observers = observers;
    }

    #region The expectations on the observation date

    /// <summary>
    ///  Calculate the expectations on the observation date.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="timeIndex">Index of the time.</param>
    /// <param name="values">The values.</param>
    /// <returns>System.Double[].</returns>
    internal double[] StepBackToObservation(
      GeneralizedHWTree tree, int timeIndex, double[] values)
    {
      return StepBack(_probabilities,
        tree.StatePrices[timeIndex + 1],
        GetArrayMin(tree.K[timeIndex]) - 1,
        values);
    }

    public static double[] StepBack(
      TrinomialJump[] probabilities,
      double[] statePrices,
      int kMin,
      double[] values)
    {
      if (probabilities == null)
        return values;

      int kLast = values.Length - 1,
        count = probabilities.Length;
      Debug.Assert(count == values.Length);

      Debug.Assert(count == statePrices.Length);

      double mean = 0, newMean = 0;
      double[] newValues = new double[count];
      for (int j = 0; j < count; ++j)
      {
        var prob = probabilities[j];
        var k = prob.Index - kMin;
        Debug.Assert(k >= 0 && k < values.Length);

        // Calculate the expectation in T-measure
        double weight = prob.Middle*statePrices[k],
          statePrice = weight,
          value = weight*values[k];
        if (k > 0)
        {
          weight = prob.Down*statePrices[k - 1];
          statePrice += weight;
          value += weight*values[k - 1];
        }
        if (k < kLast)
        {
          weight = prob.Up*statePrices[k + 1];
          statePrice += weight;
          value += weight*values[k + 1];
        }
        newValues[j] = value = statePrice.Equals(0.0) ? 0 : (value/statePrice);

        newMean += value*statePrices[j];
        mean += values[j]*statePrices[j];
      }
      if (newMean.Equals(0.0)) return newValues;

      // Now adjust for any remaining discrepancy in the unconditional expectation
      double scale = mean/newMean;
      for (int j = 0; j < count; ++j)
      {
        newValues[j] *= scale;
      }
      return newValues;
    }

    #endregion

    #region Initialize the probability distribution

    /// <summary>
    /// Initializes the transition probabilities from the observation
    ///   date to the next tree node date
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="tree">The tree.</param>
    /// <param name="timeIndex">Index of the time.</param>
    internal void Initialize(Dt date, GeneralizedHWTree tree, int timeIndex)
    {
      Debug.Assert(timeIndex > 0);

      var deltaT = (tree.TimeGridDates[timeIndex] - date)/365.0;
      _probabilities = CalcalateProbabilities(tree, timeIndex, deltaT);
    }

    /// <summary>
    /// To calculate the observation probabilities
    /// </summary>
    /// <param name="tree">The tree that has been built</param>
    /// <param name="timeIndex">The timeIndex we would like to calculate</param>
    /// <param name="deltaT"> the time difference between the 
    /// grid date and the notice date</param>
    /// 
    /// <remarks>
    /// <para>
    /// Let <m>X(t)</m> be the state variable,
    /// <m>J</m> the discrete state space at time <m>t</m>,
    /// <m>x_{j}</m> the value of <m>X(t)</m> in the states <m>j \in J</m>
    /// given by<math>
    ///   x_{j} = x_0 + j\,\Delta{x}
    /// </math>
    /// We calculate the transition probabilities between time
    /// <m>t' \lt t</m> and <m>t</m>, i.e., the probabilities
    /// of jumping from a state <m>j' \in J</m> at time <m>t'</m>
    ///  to a state <m>j \in J</m> at time <m>t</m>.
    /// </para>
    /// 
    /// <para>Let <m>m_{j}</m> and <m>v_j^2</m> be the conditional
    ///  expectation and variance, respectively<math>\begin{align}
    ///   m_{j} &amp;\equiv \mathrm{E}[X(t) \mid X(t') = x_{j}]
    ///   \\ v^2_{j} &amp;\equiv \mathrm{Var}[X(t) \mid X(t') = x_{j}]
    /// \end{align}</math>
    ///  Define<math>\begin{align}
    ///    k_{j} &amp;\equiv \mathrm{integer}\left[
    ///     \frac{m_{j} - x_0}{\Delta{x}} + 0.5
    ///   \right]
    ///   \\ \alpha_{j} &amp;\equiv \frac{m_{j} - x_0}{\Delta{x}} - k_{j}
    ///   \\ j_{\mathrm{min}} &amp;= \min\,J
    ///    ,\quad j_{\mathrm{max}} = \max\,J
    /// \end{align}</math>
    /// </para>
    /// 
    /// <para>When <m>j_{\mathrm{min}} \lt k_j \lt j_{\mathrm{max}}</m>,
    /// we find the probabilities <m>p^u_j</m>, <m>p^m_j</m> and
    /// <m>p^d_j</m> of jumping from state <m>j</m> to the states
    /// <m>k_j + 1</m>, <m>k_j</m> and <m>k_j-1</m>,
    /// respectively, such that both the mean <m>m_j</m> and
    /// the variance <m>v^2_j</m> are matched.
    /// They are given by
    /// <math>\begin{align}
    ///  p_{j}^u &amp;= \frac{1}{2}\left[
    ///     \frac{v^2_j}{\Delta{x}^2} + \alpha_{j}^2 + \alpha_{j}
    ///    \right]
    ///  \\ p_{j}^d &amp;= \frac{1}{2}\left[
    ///     \frac{v^2_j}{\Delta{x}^2} + \alpha_{j}^2 - \alpha_{j}
    ///    \right]
    ///  \\ p^m_{j} &amp;= 1 - p^u_j - p^d_j
    /// \end{align}</math>
    /// </para>
    /// 
    /// <para>When <m>k_j</m> is on or above the top edge,
    ///  <m>k_j \geq j_{\mathrm{max}}</m>,
    ///  we have <m>-0.5 \leq \alpha_j \leq 0</m>,
    ///  and we match the mean only by setting<math>
    ///     p_{j}^u = 0, 
    ///  ,\quad p_{j}^d = -\alpha_j
    ///  ,\quad p_{j}^m = 1 - p_{j}^d
    /// </math>
    /// while at or below the bottom edge,
    ///  <m>k_j \leq j_{\mathrm{min}}</m>,
    ///  we have <m>0 \leq \alpha_j \leq 0.5</m>,
    ///  and we set<math>
    ///     p_{j}^u = \alpha_j
    ///  ,\quad p_{j}^m = 1 - p_{j}^u
    ///  ,\quad p_{j}^d = 0
    /// </math></para>
    /// </remarks>
    /// <returns>Array of Jump object</returns>
    private static TrinomialJump[] CalcalateProbabilities(
      GeneralizedHWTree tree, int timeIndex, double deltaT)
    {
      if (deltaT <= 0) return null;

      double t = tree.TimeGrid[timeIndex], x0 = tree.X0,
        dx = tree.DX[timeIndex], dx0 = dx;
      var diffusionProcess = tree.DiffusionProcess;
      double v2 = diffusionProcess.Variance(t, 0, deltaT);

      //! <br />
      //! By construction, the state space <m>J_i</m> at time <m>t_i</m>
      //! is based on the previous <m>k_{i-1, j}</m>, with
      //! <math>
      //!    \min J_i = \min_{j}\{k_{i-1, j}\} - 1
      //!    ,\quad \max J_i = \max_{j}\{k_{i-1, j}\} + 1
      //! </math>
      int[] k = tree.K[timeIndex - 1];
      int jMin = GetArrayMin(k) - 1, jMax = GetArrayMax(k) + 1;
      var probs = new TrinomialJump[jMax - jMin + 1];
      for (int j = jMin; j <= jMax; ++j)
      {
        double x = x0 + j*dx0;
        double mij = diffusionProcess.Mean(t, x, deltaT) - x0;
        int sign = Math.Sign(mij);
        int index = (int) Math.Floor(Math.Abs(mij/dx + sign*0.5))*sign;

        double pu, pd;
        if (index >= jMax)
        {
          // binomial node at the upper edge
          index = jMax;
          pu = 0;
          pd = index - mij/dx;
        }
        else if (index <= jMin)
        {
          // binomial node at the lower edge
          index = jMin;
          pu = mij/dx - index;
          pd = 0;
        }
        else
        {
          // trinomial nodes in the middle
          double alpha = mij/dx - index;

          var vd = v2/dx/dx;
          pu = index == jMax ? 0 : (0.5*(vd + alpha*(alpha + 1)));
          if (pu <= 0)
          {
            Debug.Assert(alpha <= 0);
            pu = 0;
            pd = -alpha;
          }
          else
          {
            pd = index == jMin ? 0 : (0.5*(vd + alpha*(alpha - 1)));
            if (pd <= 0)
            {
              Debug.Assert(alpha >= 0);
              pu = alpha;
              pd = 0;
            }
            else
            {
              IterativeImprove(index, mij/dx, vd, ref pu, ref pd);
            }
          }
        }
        var pm = 1 - pu - pd;
        ValidateMean(index, mij/dx, pu, pm, pd);
        probs[j - jMin] = new TrinomialJump(index, pu, pm, pd);
      } // end of j loop

      return probs;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="k"></param>
    /// <param name="m"></param>
    /// <param name="v2"></param>
    /// <param name="pu"></param>
    /// <param name="pd"></param>
    /// <remarks>
    ///  <papa><math>\begin{align}
    ///    (p^u + d_1)(k+1)+(p^m-d_1-d_2)k+(p^d+d_2)(k-1) &amp;= m
    ///   \\ (p^u + d_1)(k+1)^2+(p^m-d_1-d_2)k^2+(p^d+d_2)(k-1)^2 &amp;= m^2 + v^2
    ///  \end{align}</math>
    ///  which implies
    ///  <math>\begin{align}
    ///    d_1 - d_2 &amp;= \Delta{m}
    ///   \\ d_1(2k+1) + d_2(2k-1) &amp;= \Delta{v^2}
    ///  \end{align}</math>
    /// hence<math>\begin{align}
    ///    d_1 - d_2 &amp;= \Delta{m}
    ///   \\ d_1 + d_2 &amp;= \frac{\Delta{v^2}-\Delta{m}}{2k}
    ///  \end{align}</math>
    ///  </papa>
    /// </remarks>
    private static void IterativeImprove(
      int k, double m, double v2,
      ref double pu, ref double pd)
    {
      if (k == 0) return;

      for (int i = 0; i < 5; ++i)
      {
        var pm = 1 - (pu + pd);
        var dm = m - (pu*(k + 1) + pm*k + pd*(k - 1));
        if (Math.Abs(dm/k) < Tolerance) return;
        var dv2 = v2 + m*m - (pu*(k + 1)*(k + 1) + pm*k*k + pd*(k - 1)*(k - 1));
        var d1 = ((dv2 - dm)/2/k + dm)/2;
        var d2 = d1 - dm;
        pu += d1;
        pd += d2;
      }
    }

    [Conditional("DEBUG")]
    private static void ValidateMean(int k, double expect,
      double pu, double pm, double pd)
    {
      var mean = pu*(k + 1) + pm*k + pd*(k - 1);
      var diff = (mean - expect)/(k == 0 ? 1 : k);
      Debug.Assert(Math.Abs(diff) < Tolerance);
    }

    private const double Tolerance = 1E-15;
    #endregion

    public bool Enabled => _probabilities != null;

    private TrinomialJump[] _probabilities;
  }

  public struct TrinomialJump
  {
    public TrinomialJump(int index,
      double pu, double pm, double pd)
    {
      Index = index;
      Up = pu;
      Middle = pm;
      Down = pd;
    }

    // The index of the middle node
    internal readonly int Index;

    // The jump probabilities
    internal readonly double Up, Middle, Down;
  }
}
