/*
 * Solver.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Base class of all solvers
  /// </summary>
  ///
  /// <remarks>
  ///   <para>The abstract base class for all solver classes.
  ///   It provides an interface through which a user can
  ///   specify a function, solve x where
  ///   <formula inline="true"> f(x) = y_{target} </formula>, and afterward
  ///   access some diagnostic information about the process.
  ///   To create a valid Solver class, subclasses
  ///   need only override the protected doSolve() method.</para>
  ///   <seealso cref="T:BaseEntity.Toolkit.Numerics.SolverFn">SolverFn</seealso>
  ///
  ///   <para>Solver methods take a specified function
  ///   <formula inline="true"> f(x) </formula>
  ///   (and optionally it's derivative, <formula inline="true"> f'(x) </formula>,
  ///   along with either a bracketing interval
  ///   <formula inline="true"> [x_{low}, x_{high}] </formula> or a starting point
  ///   <formula inline="true"> x_0 </formula> and searches for a point
  ///   <formula inline="true"> x^* </formula> where
  ///   <formula inline="true"> f(x^*) \approx 0. </formula></para>
  ///
  ///   <para>Supported methods include:</para>
  ///   <list type="bullet">
  ///     <item><description>
  ///       Brent: requires only function evaluations.  Works on smooth functions.
  ///     </description></item><item><description>
  ///       Newton: requires function and derivative evaluations.
  ///       Works on smooth functions. Very fast, but requires a good starting
  ///       point.
  ///     </description></item><item><description>
  ///       Bisubsection: requires only function evaluations. Very robust, but
  ///       also slow compared with other methods.
  ///     </description></item>
  ///   </list>
  /// 
  ///   <para>The solver class <see cref="Generic">Generic</see>
  ///   automatically chooses an appropriate method for a specified function.
  ///   It is most appropriate for general use.</para>
  /// 
  ///   <para>All of these methods require two things:</para>
  ///
  ///   <list type="bullet">
  ///   <item><description>
  ///     The function <formula inline="true"> f </formula> be  continuous,
  ///   </description></item><item><description>
  ///     bracketing interval <formula inline="true">  [x_{low}, x_{high}] </formula>
  ///     is specified or can be located that actually contains a point
  ///     <formula inline="true"> x </formula> such that <formula inline="true"> f(x) = 0 </formula>.
  ///     The only way to verify this for certain is if
  ///     <formula inline="true"> f(x_{low}) * f(x_{high}) = 0 </formula>
  ///     and <formula inline="true"> x_{low} \lt x \lt x_{high} </formula>.  If the user
  ///     does not specify input data so that this condition
  ///     is met, all algorithms will fail.
  ///   </description></item>
  ///   </list>
  ///
  /// </remarks>
  [Serializable]
  public abstract class Solver : BaseEntityObject
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(Solver));

    #region Config
    private const int DefaultBracketSteps = 50;   // Default number of bracket steps
    private const bool keepBracket_ = true;
    #endregion // Config

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected Solver()
		{
		  xCurrent_ = 0.0;               // Current solution estimate
			fCurrent_ = 0.0;               // Current f(x)
			lBracket_ = 1.0;               // Default x lower bracket to force reset in solve()
			uBracket_ = -1.0;              // Default x upper bracket to force reset in solve()
			xLB_ = Double.MinValue;        // Default x lower bound
			xUB_ = Double.MaxValue;        // Default x upper bound
			tolX_ = 10e-6;                 // Default x tolerance
			tolF_ = 10e-7;                 // Default f(x) tolerance
			maxEvaluations_ = 500;         // Default max evaluations
			maxIterations_ = 100;          // Default max iterations
			numEvaluations_ = 0;
			numIterations_ = 0;
			bracketStep_ = 1.6;
			growByMultiply_ = true;

      bracketFnReady_ = false;
      lBracketF_ = uBracketF_ = Double.NaN;
    }

    /// <summary>
    ///   Constructor with the ability to tune the bracket finding method.
    /// </summary>
    /// <param name="growByMultiply">Boolean: if true, bracks grow by multiplying the width by bracketStep, false means add bracketStep</param>
    /// <param name="bracketStep">Size parameter for growing the bracket.  Default is 1.6.</param>
    protected Solver(bool growByMultiply, double bracketStep)
		{
		  xCurrent_ = 0.0;               // Current solution estimate
			fCurrent_ = 0.0;               // Current f(x)
			lBracket_ = 1.0;               // Default x lower bracket to force reset in solve()
			uBracket_ = -1.0;              // Default x upper bracket to force reset in solve()
			xLB_ = Double.NegativeInfinity;// Default x lower bound
			xUB_ = Double.PositiveInfinity;// Default x upper bound
			tolX_ = 10e-6;                 // Default x tolerance
			tolF_ = 10e-7;                 // Default f(x) tolerance
			maxEvaluations_ = 500;         // Default max evaluations
			maxIterations_ = 100;          // Default max iterations
			numEvaluations_ = 0;
			numIterations_ = 0;
			bracketStep_ = bracketStep;
			growByMultiply_ = growByMultiply;

      bracketFnReady_ = false;
      lBracketF_ = uBracketF_ = Double.NaN;
    }
		#endregion // Constructor

		#region Methods
    /// <summary>
    ///   Pure virtual function doing the real job.
    /// </summary>
    /// <remarks>
    ///   Subclasses implement this function.  It can be assumed that
    ///   the interval <formula inline="true"> [x_{low},x_{high}] </formula> contains at least one solution, meaning
    ///   <formula inline="true"> (f(x_{low})-y_{target})*(f(x_{high})-y_{target}) \lt 0 </formula>.
    ///   Return values are via arguments and are updated as the
    ///   solution finder progresses.
    ///
    ///   <para>This method may throw an exception if the maximum number of
    ///   iterations or evaluations re reached.</para>
    /// </remarks>
    /// <param name="fn">Target function <formula inline="true"> f(x) </formula></param>
    /// <param name="yTarget">Target value</param>
    /// <param name="x">Returns found solution</param>
    /// <param name="fx">Returns f evaluated at solution - <formula inline="true">y_{Target}</formula>. Ie. <formula inline="true">f(x) - y_{Target}</formula></param>
    /// <param name="numIter">Returns number of iterations</param>
    /// <param name="numEvals">Returns number of evaluations</param>
    public abstract void doSolve( SolverFn fn, double yTarget,
																	ref double x, ref double fx,
																	ref int numIter, ref int numEvals );

    /// <summary>
    ///   Solve for a target y value starting at the current
    ///   x position.
    /// </summary>
    /// <remarks>
    ///   If current x is within valid range and
    ///   current bracket is valid (brackets target),
    ///   continues solver from current position.
    ///   If current x is not valid or bracket has not
    ///   been set, restarts solver from current
    ///   position by calling restart() to reset counters
    ///   and findBracket() to find an intial bracket.
    ///   If necessary, guesses initial bracket as max of current
    ///   position +-10% or current position +- x tolerance * 10e4.
    ///   Initial estimate of bracketing is not perfect and
    ///   if possible you should call solve() with an initial
    ///   guess.
    /// </remarks>
    /// <param name="fn">Target function <formula inline="true"> f(x) </formula></param>
    /// <param name="yTarget">Target y value</param>
    /// <returns>found <formula inline="true"> x </formula> where <formula inline="true"> f(x) = y_{target} </formula></returns>
    /// <exception cref="T:BaseEntity.Toolkit.Numerics.SolverException">Thrown if solution not found.</exception>
    public double solve(SolverFn fn, double yTarget)
		{
      // Check if current position out of bounds
      if( xCurrent_ < xLB_ || xCurrent_ >= xUB_ )
			{
				// restart at a reasonable guess.
				restart();
				xCurrent_ = xLB_/2.0 + xUB_/2.0;
			}

			// Check if current position outside of bracket or bracket not set
			if( xCurrent_ < lBracket_ || xCurrent_ > uBracket_ )
			{
				// restart with a new bracket.
				double delta = Math.Max(Math.Abs(xCurrent_)*0.1, tolX_*1e4);
				restart();
				lBracket_ = Math.Max(xLB_, xCurrent_ - delta);
				uBracket_ = Math.Min(xUB_, xCurrent_ + delta);
			}

			// Find solution bracket starting with current guess
      bracketFnReady_ = false;
      if (findBracket(fn, yTarget, ref lBracket_, ref lBracketF_, ref uBracket_, ref uBracketF_, DefaultBracketSteps))
        bracketFnReady_ = keepBracket_; // set only when KeepBracket is true
      else
        throw new SolverException("Cannot bracket a solution"); // SolverExceptionRange
			
			// If the low bracket is the same as the upper bracket, we've already got a solution
			if( lBracket_ == uBracket_ )
				return lBracket_;

			// Find solution
			doSolve(fn, yTarget, ref xCurrent_, ref fCurrent_, ref numIterations_, ref numEvaluations_);

			// Save current f(x) rather than difference
			fCurrent_ += yTarget;

			#if SANITY_CHECK
			// Do a few sanity checks on found solution

			// Note: We test for both now. This is a bit sensitive and needs review. RTD Mar'06
			double F = Math.Abs(fCurrent_ - yTarget);
			double eps = tolX_;
			double df = Math.Abs((fn.evaluate( min(xUB_, xCurrent_+eps) ) -
														fn.evaluate( max(xLB_, xCurrent_-eps) ))) /
				(2.0*eps) + 2.0*Double.Epsilon ;

			if( !(F < (2.0*tolF_)) && ((F/df) > (10.0*tolX_)) )
			{
			  logger.DebugFormat("Found solution is suspicious. f(x) too large ({0} > {1})", F, 2.0*tolF_);
			  logger.DebugFormat("Found solution is suspicious. f(x)/f'(x) too large ({0} > {1})", F/df, 10.0*tolX_);
			}
			#endif

			return xCurrent_;
		}

    internal bool doBracket(SolverFn fn, double yTarget)
    {
      // Find solution bracket starting with current guess
      bracketFnReady_ = false;
      if (findBracket(fn, yTarget, ref lBracket_, ref lBracketF_, ref uBracket_, ref uBracketF_, DefaultBracketSteps))
        bracketFnReady_ = keepBracket_; // set only when KeepBracket is true
      return bracketFnReady_;
    }

    internal double doSolve(SolverFn fn, double yTarget)
    {
      if (lBracket_ == uBracket_)
        return lBracket_;

      doSolve(fn, yTarget, ref xCurrent_, ref fCurrent_, ref numIterations_, ref numEvaluations_);

      // Save current f(x) rather than difference
      fCurrent_ += yTarget;

      return xCurrent_;
    }


    /// <summary>
    ///   Solve for a target y value given an initial starting
    ///   guess xInitial for x.
    /// </summary>
    /// <remarks>
    ///   If xInitial is within valid range and current bracket is
    ///   valid (brackets target), continues solve from xInitial.
    ///   If xInitial is not valid or bracket has not been set,
    ///   restarts solve from xInitial by calling restart() to reset
    ///   counters and findBracket() to find an intial bracket.
    ///   If necessary, guesses initial bracket as max of current
    ///   position +-10% or current position +- x tolerance * 10e4.
    ///   Initial estimate of bracketing is not perfect and
    ///   if possible you should call solve() with an initial
    ///   guess.
    /// </remarks>
    /// <param name="fn">Target function <formula inline="true"> f(x) </formula></param>
    /// <param name="yTarget">Target y value</param>
    /// <param name="xInitial">initial guess for x</param>
    /// <returns>found <formula inline="true"> x </formula> where <formula inline="true"> f(x) = y_{target} </formula></returns>
    /// <exception cref="T:BaseEntity.Toolkit.Numerics.SolverException">Thrown if solution not found.</exception>
		public double solve(SolverFn fn, double yTarget, double xInitial)
		{
      if( xInitial < xLB_ || xInitial > xUB_ )
				throw new ArgumentException( String.Format("Invalid starting value ({0}) outside of valid range ({1}-{2})",
          xInitial, xLB_, xUB_) );

			xCurrent_ = xInitial;
			return solve(fn, yTarget);
		}


    /// <summary>
    ///   Solve for a target y value given an initial guess for
    ///   a bracket.
    ///   If current x is within valid range and within specified
    ///   bracket, continues solver from current position.
    ///   If current x is not valid or outside bracket
    ///   restarts solver from current position by calling
    ///   restart() to reset counters and findBracket() to
    ///   find an intial bracket.
    /// </summary>
    /// <param name="fn">Target function <formula inline="true"> f(x) </formula></param>
    /// <param name="yTarget">Target value</param>
    /// <param name="xLower">starting lower bound on bracketing interval</param>
    /// <param name="xUpper">starting upper bound on bracketing interval</param>
    /// <returns>found <formula inline="true"> x </formula> where <formula inline="true"> f(x) = y_{target} </formula></returns>
    /// <exception cref="T:BaseEntity.Toolkit.Numerics.SolverException">if solution not found.</exception>
		public double solve(SolverFn fn, double yTarget, double xLower, double xUpper)
		{
      if( (xLower < xLB_) || (xLower > xUB_) )
				throw new SolverException(String.Format("Lower ({0}) bracket out of bounds ({1}-{2})", xLower, xLB_, xUB_));
			if( (xUpper < xLB_) || (xUpper > xUB_) ) 
				throw new SolverException(String.Format("Upper ({0}) bracket out of bounds ({1}-{2})", xUpper, xLB_, xUB_));

			lBracket_ = xLower;
			uBracket_ = xUpper;

			// Check if current position outside of bracket
			if( xCurrent_ < xLower || xCurrent_ > xUpper )
			{
				// restart with a new current position
				restart();
				xCurrent_ = xLower/2.0 + xUpper/2.0;
			}

			return solve(fn, yTarget);
		}

    /// <summary>
    ///   Convenient interface to solver for two vectors containing the x and
    ///   y values.
    /// </summary>
    /// <param name="x">Array containing x values</param>
    /// <param name="y">Array containing y values</param>
    /// <param name="interp">The interpolation method to use over the array</param>
    /// <param name="yTarget">Target y value</param>
    /// <returns>found <formula inline="true"> x </formula> where <formula inline="true"> f(x) = y_{target} </formula></returns>
    /// <example>
    ///   <code language="C++">
    ///   // Define data to be interpolated
    ///   Array&lt;double&gt; x(4);
    ///   Array&lt;double&gt; y(4);
    ///   x[0] = 1; x[1] = 2; x[2] = 3; x[3] = 4;
    ///   y[0] = 3; y[1] = 4; y[2] = 5; y[3] = 6;
    ///
    ///   // Create interpolation method we want
    ///   Interps::Linear interp;
    ///
    ///   // Create solver method we want
    ///   Solvers::Generic solver;
    ///
    ///   // Now solve
    ///   cout &lt;&lt; "Solving for f(x) = 3.5 gives ", solver.solve(x, y, interp, 3.5) &lt;&lt; cout;
    ///   </code>
    /// </example>
    public double solve( double [] x, double [] y, Interp interp, double yTarget)
		{
		  SolverFn fn = new ArraySolverFn(x, y, interp);
			return solve( fn, yTarget );
		}

    /// <summary>
    ///   Restart solver
    ///   Resets all evaluation counters.
    /// </summary>
		public void restart()
		{
      // Reset counters
      numEvaluations_ = 0;
			numIterations_ = 0;
		}

    /// <summary>
    ///  Find an interval that
    ///   contains a target, assuming continuity of the underlying function.
    /// </summary>
    /// <remarks>
    ///   Starting from a specified point, attempts to find an interval that
    ///   contains a target y where the function <formula inline="true"> f(x) = y_{target} </formula>.
    ///   In other words, this method searches for <formula inline="true"> x1 \lt x2 </formula> such that
    ///   <formula inline="true"> (f(x1)-y_{target}) (f(x2)-y_{target}) \lt 0 </formula>. It starts by looking
    ///   at the interval <formula inline="true"> [x1,x2] </formula> and widening the interval until
    ///   a bracket is found or the maximium number of steps is reached.
    ///
    ///   <para>If current x is within specified bracket, continues findBracket from
    ///   the current position. Otherwise restarts bracket find with an initial
    ///   guess at the midpoint of the specified bracket.</para>
    ///
    ///   <para>FindBracket() may fail even if the function in question has a valid solution.
    ///   If this is the case, and you are sure your function has a solution, try
    ///   making the distance between <formula inline="true"> x_{lower} </formula> and <formula inline="true"> x_{upper} </formula>
    ///   smaller and locating them near to your best guess of where the
    ///   solution may be located.  You may also need to increase the value
    ///   of the <c>numSteps</c> parameter.</para>
    /// </remarks>
    /// <param name="fn">Target function <formula inline="true"> f(x) </formula></param>
    /// <param name="yTarget">Target value</param>
    /// <param name="xLower">defines lower bound of initial interval.
    ///   On return it contains a lower bound on a bracketing interval.</param>
    /// <param name="xUpper">defines upper bound of initial interval.
    ///   On return it contains an upper bound on a bracketing interval.</param>
    /// <param name="numSteps">number of search iterations.  If non-positive, a default value is used.</param>
    /// <returns><c>true</c> if a valid bracket was found, <c>false</c> if no bracket could be found.</returns>
    public bool findBracket(SolverFn fn, double yTarget, ref double xLower, ref double xUpper, int numSteps)
    {
      double fLower = 0, fUpper = 0;
      return findBracket(fn, yTarget, ref xLower, ref fLower, ref xUpper, ref fUpper, numSteps);
    }

    /// <summary>
    ///  Find an interval that
    ///   contains a target, assuming continuity of the underlying function.
    /// </summary>
    /// <remarks>
    ///   Starting from a specified point, attempts to find an interval that
    ///   contains a target y where the function <formula inline="true"> f(x) = y_{target} </formula>.
    ///   In other words, this method searches for <formula inline="true"> x1 \lt x2 </formula> such that
    ///   <formula inline="true"> (f(x1)-y_{target}) (f(x2)-y_{target}) \lt 0 </formula>. It starts by looking
    ///   at the interval <formula inline="true"> [x1,x2] </formula> and widening the interval until
    ///   a bracket is found or the maximium number of steps is reached.
    ///
    ///   <para>If current x is within specified bracket, continues findBracket from
    ///   the current position. Otherwise restarts bracket find with an initial
    ///   guess at the midpoint of the specified bracket.</para>
    ///
    ///   <para>FindBracket() may fail even if the function in question has a valid solution.
    ///   If this is the case, and you are sure your function has a solution, try
    ///   making the distance between <formula inline="true"> x_{lower} </formula> and <formula inline="true"> x_{upper} </formula>
    ///   smaller and locating them near to your best guess of where the
    ///   solution may be located.  You may also need to increase the value
    ///   of the <c>numSteps</c> parameter.</para>
    /// </remarks>
    /// <param name="fn">Target function <formula inline="true"> f(x) </formula></param>
    /// <param name="yTarget">Target value</param>
    /// <param name="xLower">defines lower bound of initial interval.
    ///   On return it contains a lower bound on a bracketing interval.</param>
    /// <param name="fLower">the function value at lower bound of initial interval</param>
    /// <param name="xUpper">defines upper bound of initial interval.
    ///   On return it contains an upper bound on a bracketing interval.</param>
    /// <param name="fUpper">the function value at upper bound of initial interval</param>
    /// <param name="numSteps">number of search iterations.  If non-positive, a default value is used.</param>
    /// <returns><c>true</c> if a valid bracket was found, <c>false</c> if no bracket could be found.</returns>
    public bool findBracket(
      SolverFn fn, double yTarget,
      ref double xLower, ref double fLower,
      ref double xUpper, ref double fUpper,
      int numSteps)
    {
      // Do tests here so that we can freely set parameters in any order
      if (xLower >= xUpper)
        throw new ArgumentException(String.Format("Invalid bracket (lower ({0}) >= upper ({1}))", xLower, xUpper));
      if (xLB_ >= xUB_)
        throw new ArgumentException(String.Format("Invalid bounds (lower ({0}) >= upper ({1}))", xLB_, xUB_));
      if ((xLower < xLB_) || (xLower > xUB_))
        throw new ArgumentException(String.Format("Lower ({0}) bracket out of bounds ({1}-{2})", xLower, xLB_, xUB_));
      if ((xUpper < xLB_) || (xUpper > xUB_))
        throw new ArgumentException(String.Format("Upper ({0}) bracket out of bounds ({1}-{2})", xUpper, xLB_, xUB_));
      if ((numSteps <= 0) || (numSteps > 10000))
        throw new ArgumentException(String.Format("Invalid numSteps ({0} <= 0 or > 10000)", numSteps));

      // Reset starting point if we are out of bounds
      if (xCurrent_ <= xLower || xCurrent_ >= xUpper)
      {
        setInitialPoint(xLower / 2.0 + xUpper / 2.0);
      }

      // adapted from numerical recipes
      double bracketStep_ = 1.6;
      int j;
      double f1, f2;

      f1 = fn.evaluate(xLower) - yTarget;
      f2 = fn.evaluate(xUpper) - yTarget;
      for (j = 1; j <= numSteps; j++)
      {
        if ((f1 * f2) <= 0.0)
        {
          fLower = f1;
          fUpper = f2;
          return true;
        }
        if (Math.Abs(f1) < Math.Abs(f2))
        {
          // check if we've found a solution
          if (Math.Abs(f1) <= tolF_)
          {
            xCurrent_ = xUpper = xLower;
            fCurrent_ = f1;
            return true;
          }
          // Step xLower bracket out
          if (xLower <= xLB_)
            break;
          if (growByMultiply_)
            xLower += bracketStep_ * (xLower - xUpper);
          else
            xLower -= bracketStep_;
          // Range check
          if (xLower >= xUB_)
            xLower = xUB_;
          if (xLower <= xLB_)
            xLower = xLB_;
          f1 = fn.evaluate(xLower) - yTarget;
        }
        else
        {
          // check if we've found a solution
          if (Math.Abs(f2) <= tolF_)
          {
            xCurrent_ = xLower = xUpper;
            fCurrent_ = f2;
            return true;
          }
          // Step xUpper bracket out
          if (xUpper >= xUB_)
            break;
          if (growByMultiply_)
            xUpper += bracketStep_ * (xUpper - xLower);
          else
            xUpper += bracketStep_;
          // Range check
          if (xUpper >= xUB_)
            xUpper = xUB_;
          if (xUpper <= xLB_)
            xUpper = xLB_;
          f2 = fn.evaluate(xUpper) - yTarget;
        }
      }
      logger.DebugFormat("Unable to find bracket within {0} steps. Have xLower {1} ({2}), xUpper {2} ({3})",
                          xLower, f1 + yTarget, xUpper, f2 + yTarget);

      // return good error information in xCurrent and fCurrent
      if (Math.Abs(f1) < Math.Abs(f2))
      {
        xCurrent_ = xLower;
        fCurrent_ = f1;
      }
      else
      {
        xCurrent_ = xUpper;
        fCurrent_ = f2;
      }

      return false;
    }

    /// <summary>
    /// Find root of a function.
    /// </summary>
    /// <param name="evaluate">The function value evaluator.</param>
    /// <param name="derivative">The derivative evaluator (may be null).</param>
    /// <param name="yTarget">The y target.</param>
    /// <returns>The root found.</returns>
    /// <remarks>This functions accepts inline lambda expressions.</remarks>
    /// <example>
    /// <code>
    ///    Brent rf = new Brent();
    ///    double root = rf.solve((x) => x * x - 1, null, 0.0);
    /// </code>
    /// </example>
    public double solve(Func<double, double> evaluate,
      Func<double, double> derivative, double yTarget)
    {
      SolverFn fn = new SolverFnAdapter(evaluate, derivative);
      return solve(fn, yTarget);
    }

    /// <summary>
    /// Find root of a function.
    /// </summary>
    /// <param name="evaluate">The function value evaluator.</param>
    /// <param name="derivative">The derivative evaluator (may be null).</param>
    /// <param name="yTarget">The y target.</param>
    /// <param name="xInitial">The initial x value to try.</param>
    /// <returns>The root found.</returns>
    /// <remarks>This functions accepts inline lambda expressions.</remarks>
    /// <example>
    /// <code>
    ///    Brent rf = new Brent();
    ///    double root = rf.solve((x) => x * x - 1, null, 0.0, 0.5);
    /// </code>
    /// </example>
    public double solve(Func<double, double> evaluate,
      Func<double, double> derivative, double yTarget, double xInitial)
    {
      SolverFn fn = new SolverFnAdapter(evaluate, derivative);
      return solve(fn, yTarget, xInitial);
    }

    /// <summary>
    /// Find root of a function.
    /// </summary>
    /// <param name="evaluate">The function value evaluator.</param>
    /// <param name="derivative">The derivative evaluator (may be null).</param>
    /// <param name="yTarget">The y target.</param>
    /// <param name="xLower">The initial lower bracket.</param>
    /// <param name="xUpper">The initial upper bracket.</param>
    /// <returns>The root found.</returns>
    /// <remarks>This functions accepts inline lambda expressions.</remarks>
    /// <example>
    /// <code>
    ///    Brent rf = new Brent();
    ///    double root = rf.solve((x) => x * x - 1, null, 0.0, 0.5, 1.5);
    /// </code>
    /// </example>
    public double solve(Func<double, double> evaluate,
      Func<double, double> derivative, double yTarget, double xLower, double xUpper)
    {
      SolverFn fn = new SolverFnAdapter(evaluate, derivative);
      return solve(fn, yTarget, xLower, xUpper);
    }

    #region Solvefn Adapters
    [Serializable]
    private class SolverFnAdapter : SolverFn
    {
      internal SolverFnAdapter(Func<double, double> eval,
        Func<double, double> deriv)
      {
        eval_ = eval; deriv_ = deriv;
      }
      public override double evaluate(double x)
      {
        return eval_(x);
      }
      public override double derivative(double x)
      {
        if (deriv_ != null) return deriv_(x);
        return base.derivative(x);
      }
      public override bool isDerivativeImplemented()
      {
        return deriv_ != null;
      }
      private readonly Func<double, double> eval_;
      private readonly Func<double, double> deriv_;
    }
    #endregion Solvefn Adapters

    #endregion // Methods

    #region Properties
    /// <exclude />
    public double LowerBracketF
    {
      get { return lBracketF_; }
      set { lBracketF_ = value; }
    }

    /// <exclude />
    public double UpperBracketF
    {
      get { return uBracketF_; }
      set { uBracketF_ = value; }
    }

    /// <exclude />
    public double LowerBracket
    {
      get { return lBracket_; }
      set { lBracket_ = value; }
    }

    /// <exclude />
    public double UpperBracket
    {
      get { return uBracket_; }
      set { uBracket_ = value; }
    }

    /// <exclude />
    public bool BracketFnReady
    {
      get { return bracketFnReady_; }
      set { bracketFnReady_ = value; }
    }
		#endregion Properties

		#region Data
    private double xCurrent_;
    private double fCurrent_;
    private double lBracket_;
    private double uBracket_;
    private double xLB_;
    private double xUB_;
    private double tolX_;
    private double tolF_;
    private int maxEvaluations_;
    private int maxIterations_;
    private int numEvaluations_;
    private int numIterations_;
    private double bracketStep_;
    private bool growByMultiply_;

    private double lBracketF_;
    private double uBracketF_;
    private bool bracketFnReady_;
    #endregion Data

		#region Helpers
		class ArraySolverFn : SolverFn
		{
			internal ArraySolverFn( double [] xa, double [] ya, Interp interp )
			{
        interp_ = new Interpolator(interp, xa, ya);
			}

			/// <summary>
			///  Core method providing target function values.
			/// </summary>
			/// <returns>evaluated objective function f(x)</returns>
			public override double evaluate( double x )
			{
			  double y = interp_.evaluate(x);
				return y;
			}

			private Interpolator interp_;
		} // ArraySolverFn
		#endregion // Helpers

		#region LegacyObsolete

		/// <exclude />
		public int getMaxIterations()
		{
      return maxIterations_;
		}

		/// <exclude />
    public void setMaxIterations(int N)
		{
		  if (N <= 0)
		    throw new ArgumentOutOfRangeException("N", "Max iterations must be +ve");
		  maxIterations_ = N;
		}

    /// <exclude />
		public int getMaxEvaluations()
		{
      return maxEvaluations_;
		}
    
		/// <exclude />
		public void setMaxEvaluations(int N)
		{
      if( N <= 0 )
				throw new ArgumentException( String.Format("Invalid max evaluations ({0} <= 0)", N) );
			maxEvaluations_ = N;
		}

		/// <exclude />
		public int getNumIterations()
		{
      return numIterations_;
		}

		/// <exclude />
		public int getNumEvaluations()
		{
      return numEvaluations_;
		}

		/// <exclude />
		public void setInitialPoint(double xInitial)
		{
      xCurrent_ = xInitial;
		}

		/// <exclude />
    public double getLowerBounds()
		{
      return xLB_;
		}

		/// <exclude />
		public void setLowerBounds(double x)
		{
      xLB_ = x;
		}

		/// <exclude />
		public void setUpperBounds(double x)
		{
      xUB_ = x;
		}

		/// <exclude />
		public double getUpperBounds()
		{
      return xUB_;
		}

    /// <exclude />
    public void setLowerBracket(double x)
    {
      lBracket_ = x;
    }

    /// <exclude />
    public double getLowerBracket()
    {
      return lBracket_;
    }

		/// <exclude />
		public void setUpperBracket(double x)
		{
      uBracket_ = x;
		}

		/// <exclude />
		public double getUpperBracket()
		{
      return uBracket_;
		}

    /// <exclude />
		public void setToleranceX(double tolX)
		{
      if( tolX <= 0.0 )
				throw new ArgumentException( String.Format("Invalid x tolerance ({0} <= 0)", tolX));
			tolX_ = tolX;
		}

		/// <exclude />
		public double getToleranceX()
		{
      return tolX_;
		}

		/// <exclude />
		public void setToleranceF(double tolF)
		{
      if( tolF <= 0.0 )
				throw new ArgumentException( String.Format("Invalid f(x) tolerance ({0} <= 0)", tolF));
			tolF_ = tolF;
		}

		/// <exclude />
		public double getToleranceF()
		{
      return tolF_;
		}

		/// <exclude />
		public double getCurrentSolution()
		{
      return xCurrent_;
		}

		/// <exclude />
		public double getCurrentF()
		{
      return fCurrent_;
		}

    /// <exclude />
    public void setLowerBracketF(double f)
    {
      lBracketF_ = f;
    }

    /// <exclude />
    public double getLowerBracketF()
    {
      return lBracketF_;
    }

    /// <exclude />
    public void setUpperBracketF(double f)
    {
      uBracketF_ = f;
    }

    /// <exclude />
    public double getUpperBracketF()
    {
      return uBracketF_;
    }

    /// <exclude />
    public bool getBracketFnReady()
    {
      return bracketFnReady_;
    }
    #endregion // LegacyObsolete
	}

}
