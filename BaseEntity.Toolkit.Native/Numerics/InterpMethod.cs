//
// InterpMethod.cs
// Copyright (c)   2002-2008. All rights reserved.
//

using System;
using System.Globalization;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics
{

  /// <summary>
  /// Enumeration of defined interpolation routines.
  /// Custom interpolation methods may be defined outside of this
  /// enumeration.
  /// </summary>
  public enum InterpMethod
  {
    /// <summary>
    /// Linear
    /// <para>Also known as linear on rates.</para>
    /// <para>Assume that two pairs of points are known <formula inline="true">(x_1,y_1)</formula> and
    /// <formula inline="true">(x_2,y_2)</formula>, and an intermediate value is desired. The following
    /// relationship defines interpolated values between the two known points when using Linear Interpolation:</para>
    /// <formula>
    ///   f(x)=y_1+\frac{y_2-y_1}{x_2-x_1}(x-x_1)
    /// </formula>
    /// <para>This generalizes to many data points by employing the above strategy between any two
    /// adjacent points in the data set.</para>
    /// </summary>
    Linear,
    /// <summary>
    /// LogLinear
    /// <para><see cref="Linear"/> interpolation in log space.</para>
    /// </summary>
    LogLinear,
    /// <summary>
    /// Flat
    /// <para>Flat interpolation</para>
    /// </summary>
    Flat,
    /// <summary>
    /// Weighted
    /// <para>Also known as linear on log discount factors.</para>
    /// <para>Weighted interpolation is more appropriately known as time-weighted interpolation.
    /// The goal is to provide an interpolation scheme for values that are, more or less, driven by
    /// exponential decay dynamics.  Importantly, the points should be monotonically decreasing.
    /// Further, in this setting, the desire is to provide a mechanism where values between knots are
    /// governed by a flat hazard rate.
    /// Again, assume two points are known <formula inline="true">(t_1,p_1)</formula> and
    /// <formula inline="true">(t_2,p_2)</formula>. The relationship between these points can
    /// define a hazard rate:</para>
    /// <formula>
    ///   p_2=p_1 e^{-r(t_2-t_1)}
    /// </formula>
    /// <para>Or, solving for the hazard rate,</para>
    /// <formula>
    ///   r=-\frac{log⁡(p_2/p_1 )}{(t_2-t_1 )}
    /// </formula>
    /// <para>This expression shows the time weighting. By using this hazard rate to define
    /// the interpolated values for time <formula inline="true">∈(t_1,t_2 )</formula>, we can say</para>
    /// <formula>
    /// f(t)=p_1 e^{-r(t-t_1 )}
    /// </formula>
    /// <para>Here the hazard rate is defined as above. This is the default and recommended
    /// interpolation method for survival probabilities and discount factors. Again, this generalizes
    /// to many data points by employing the above strategy between any two adjacent points in the
    /// data set.</para>
    /// </summary>
    Weighted,
    /// <summary>
    /// LogWeighted
    /// <para><see cref="Weighted"/> interpolation in log space.</para>
    /// </summary>
    LogWeighted,
    /// <summary>
    /// Cubic
    /// <para>The curious reader is directed to Wikipedia, where a nice discussion of
    /// cubic interpolation is available. See
    /// <a href="http://en.wikipedia.org/wiki/Spline_interpolation">Spline Interpolation</a>
    /// for more details. Cubic spline interpolation strives to fit a cubic form (third
    /// order polynomial) that will connect the knots in a way that provides for a
    /// continuous curve, a continuous first derivative, and a continuous second
    /// derivative. That is, the slope at the right endpoint of a particular segment
    /// needs to match the slope at the left point of the next segment, as do the curvatures.</para>
    /// </summary>
    Cubic,
    /// <summary>
    /// LogCubic
    /// <para><see cref="Cubic"/> interpolation in log space.</para>
    /// </summary>
    LogCubic,
    /// <summary>
    /// Quadratic
    /// <para>The curious reader is directed to Wikipedia, where a nice discussion of spline
    /// interpolation is available. See
    /// <a href="http://en.wikipedia.org/wiki/Spline_interpolation">Spline Interpolation</a>
    /// for more details. Quadratic spline interpolation strives to fit a quadratic form that
    /// will connect the knots in a way that provides for both a continuous curve and a
    /// continuous first derivative. That is, the slope at the right endpoint of a particular
    /// segment needs to match the slope at the left point of the next segment.</para>
    /// </summary>
    Quadratic,
    /// <summary>
    /// LogQuadratic
    /// <para><see cref="Quadratic"/> interpolation in log space.</para>
    /// </summary>
    LogQuadratic,
    /// <summary>
    /// PCHIP
    /// <para>Also known as Hermite/Besel.</para>
    /// <para>PCHIP is an acronym for Piecewise Cubic Hermite Interpolating Polynomials. Cubic
    /// Hermite polynomials are orthogonal polynomials of the third order, where orthogonality
    /// is defined by the common L^2 norm.</para>
    /// <para>This approach is similar to Cubic Interpolation,
    /// but relaxes the requirement that the second derivatives be continuous at the knots such
    /// that it is possible for the overall shape of the underlying data to be preserved by the
    /// interpolated curve. Normal cubic methods can oscillate profoundly.
    /// The Hermite basis functions of the third order on the unit interval
    /// <formula inline="true">(0,1)</formula> are:</para>
    /// <formula>h_{00}(t)=(1+2t)(1-t)^2</formula>
    /// <formula>h_{10}(t)=t(1-t)^2</formula>
    /// <formula>h_{01}(t)=t^2(3-2t)</formula>
    /// <formula>h_{11}(t)=t^2(t-1)</formula>
    /// <para>Each segment will have a functional form that is a weighted average of these
    /// basis functions, scaled so that the left endpoint corresponds to zero and the right
    /// endpoint corresponds to 1.  A detailed discussion of how to choose boundary conditions
    /// at the knots to achieve the shape preserving aspect is available at
    /// <a href="http://en.wikipedia.org/wiki/Monotone_cubic_interpolation">Monotone Cubic Interpolation</a>.</para>
    /// </summary>
    PCHIP,
    /// <summary>
    /// Tension spline
    /// <para>Tension spline can be thought as the mixing of cubic and linear interpolations.
    /// Intuitively, it is generated by adding a pulling force (tension) to each end point
    /// of a cubic spline; as the force increases, excess convexity and extraneous inflection
    /// points gradually reduces and eventually the curve approaches a linear spline.</para>
    /// <para>With appropriately selected tension factors, the interpolated curve is smooth and is
    /// able to preserve the positivity, monotonicity and convexity in the original data points.</para>
    /// <para>Technically, let <formula inline="true">(x_k,y_k), k=1,2,…,n</formula>, be a set
    /// of points to interpolate. A tension spline is a continuous differentiable function
    /// <formula inline="true">f(x)</formula> such that (a) 
    /// <formula inline="true">y_k=f(x_k)</formula>, and (b) <formula inline="true">f^{''}(x)-\sigma_k</formula>.
    /// <formula inline="true">f(x)</formula> is linear inside the interval
    /// <formula inline="true">(x_k, x_{k+1})</formula>. If <formula inline="true">f^{''}(x)</formula>
    /// is continuous at all the points <formula inline="true">x_k</formula>, then we call
    /// it a <formula inline="true">C^2</formula> tension spline. Otherwise, it is called
    /// <formula inline="true">C^1</formula> tension spline.</para>
    /// <para>Toolkit supports both <formula inline="true">C^2</formula> tension
    /// spline and <formula inline="true">C^1</formula> tension spline.
    /// The user can supply preselected tension factors, or the Toolkit can automatically
    /// select a set of tension factors such that the positivity, monotonicity and convexity
    /// in the data are persevered, using the algorithm developed by
    /// Renka 1987, “Interpolatory tension splines with automatic selection of tension factors”,
    /// Siam J. ScI. Stat. Comput., Vol. 8, No. 3, May 1987.</para>
    /// </summary>
    Tension,
    /// <summary>
    /// Delegate interpolation
    /// </summary>
    Delegate,
    /// <summary>
    /// Custom interpolation
    /// </summary>
    Custom,
    /// <summary>
    /// SquareLinear
    /// <para>Linear interpolation in weighted squares, useful for the interpolation
    ///  of Black-Scholes volatilities.</para> 
    /// <para>Suppose we have two pairs of known points
    ///  <m>(x_1, y_1)</m> and <m>(x_2, y_2)</m>. Then the interpolation is given by<math>
    ///   f(x) = \sqrt{\frac{1}{x}\left(
    ///     y_1^2 x_1 + \frac{y_2^2 x_2 - y_1^2 x_1}{x_2 - x_1}(x - x_1)
    ///   \right)}
    /// </math></para>
    /// </summary>
    Squared,
  }

  /// <summary>
  ///   The order of interpolatory function.
  /// </summary>
  public enum InterpOrder
  {
    /// <summary>
    ///   Unspecified or not applicable.  The default order is used.
    /// </summary>
    None,
    /// <summary>
    ///   Defferentiable up to the first order.
    /// </summary>
    First,
    /// <summary>
    ///   Defferentiable up to the second order.
    /// </summary>
    Second,
  }

  /// <summary>
  ///   Interface for conversion between native Interp and managed InterpScheme
  /// </summary>
  public interface IInterpSchemeConverter
  {
    /// <summary>
    ///   Convert Interp to InterpScheme.
    /// </summary>
    /// <returns></returns>
    InterpScheme ToInterpScheme();
    /// <summary>
    ///   Convert from InterScheme.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns></returns>
    Interp FromInterpScheme(InterpScheme scheme);
  }

  /// <summary>
  ///   Interpolator parameters.
  /// </summary>
  [Serializable]
  public class InterpScheme : BaseEntityObject
  {
    private int _flags = 0;
    private const int WeightedFlag = 1;
    private const int LogarithmicFlag = 2;

    /// <summary>
    ///   Gets or sets the interp type name.
    /// </summary>
    /// <value>The interp type name.</value>
    public string TypeName { get; private set; }
    /// <summary>
    ///   Gets or sets the interp method.
    /// </summary>
    /// <value>The interp method.</value>
    public InterpMethod Method { get; set; }
    /// <summary>
    ///   Gets or sets the upper extrap method.
    /// </summary>
    /// <value>The upper extrap method.</value>
    public ExtrapScheme UpperExtrapScheme
    {
      get { return upperExtrap_; }
      set { upperExtrap_ = value; }
    }
    /// <summary>
    ///   Gets or sets the lower extrap method.
    /// </summary>
    /// <value>The lower extrap method.</value>
    public ExtrapScheme LowerExtrapScheme
    {
      get { return lowerExtrap_; }
      set { lowerExtrap_ = value; }
    }
    /// <summary>
    ///   Gets or sets the interp order.
    /// </summary>
    /// <value>The interp order.</value>
    public InterpOrder Order { get; set; }
    /// <summary>
    ///   Gets or sets the upper end condition.
    /// </summary>
    public Interp.EndCondition UpperEndCondition { get; set; }
    /// <summary>
    ///   Gets or sets the lower end condition.
    /// </summary>
    public Interp.EndCondition LowerEndCondition { get; set; }
    /// <summary>
    ///   Gets or sets a value indicating whether the interpolation is weghted adapted.
    /// </summary>
    /// <value><c>true</c> if weighted; otherwise, <c>false</c>.
    /// </value>
    public bool IsWeighted
    {
      get { return (_flags & WeightedFlag) != 0; }
      set
      {
        if (value) _flags |= WeightedFlag;
        else _flags &= ~WeightedFlag;
      }
    }
    /// <summary>
    ///   Gets or sets a value indicating whether the interpolation is logarithmic adapted.
    /// </summary>
    /// <value><c>true</c> if weighted; otherwise, <c>false</c>.
    /// </value>
    public bool IsLogarithmic
    {
      get { return (_flags & LogarithmicFlag) != 0; }
      set
      {
        if (value) _flags |= LogarithmicFlag;
        else _flags &= ~LogarithmicFlag;
      }
    }

    public double[] TensionFactors { get; set; }

    /// <summary>
    ///   Construct an InterpScheme from a string.
    /// </summary>
    /// <param name="interpMethod">The interpolation method specification.</param>
    /// <param name="upperExtrapMethod">The upper extrapolation method.</param>
    /// <param name="lowerExtrapMethod">The lower extrapolation method.</param>
    /// <returns>InterpScheme, or null if the parameter <c>interpMethod</c> is empty or it has the value <c>None</c>.</returns>
    /// <remarks>
    ///   <para>The format of <c>interpMethod</c> can be either <c>"InterpSpec"</c>, <c>"InterpSpec; ExtrapSpec"</c>,
    ///     or <c>"InterpSpec; LowerExtrapSpec/UpperExtrapSpec"</c>, where <c>InterpSpec</c> is any supported
    ///     interpolation method names, and <c>ExtrapSpec</c>, <c>LowerExtrapSpec</c> and <c>UpperExtrapSpec</c>LowerExtrapSpec/UpperExtrapSpec
    ///     are supported extrapoltion method names.</para>
    ///   <para>The supported interpolation method names include the standard methods,
    ///     <c>Flat</c>,
    ///     <c>Linear</c>,
    ///     <c>Quadratic</c>,
    ///     <c>Cubic</c>,
    ///     <c>Squared</c>,
    ///     <c>TensionC1</c>,
    ///     <c>TensionC2</c>,
    ///     <c>Weighted</c>,
    ///     <c>WeightedQuadratic</c>,
    ///     <c>WeightedCubic</c>,
    ///     <c>WeightedTensionC1</c>,
    ///     <c>WeightedTensionC2</c>,
    ///     <c>WeightedTensionC2</c>,
    ///     as well as the customized interpolation method names.</para>
    ///   <para>The supported extrap method names include <c>None</c>, <c>Const</c> and <c>Smooth</c>.</para>
    /// </remarks>
    public static InterpScheme FromString(string interpMethod,
      ExtrapMethod upperExtrapMethod = ExtrapMethod.None,
      ExtrapMethod lowerExtrapMethod = ExtrapMethod.None,
      double min = double.MinValue,
      double max = double.MaxValue)
    {
      if (String.IsNullOrEmpty(interpMethod) || String.Compare(
        interpMethod, "None", StringComparison.OrdinalIgnoreCase) == 0)
      {
        return null;
      }
      ParseInput(ref interpMethod, ref upperExtrapMethod, ref lowerExtrapMethod);

      InterpMethod method;
      bool weighted = false, logrithm = false, firstOrder = false;
      if (interpMethod.StartsWith("Weighted", false, null))
      {
        interpMethod = interpMethod.Substring(8);
        weighted = true;
      }
      else if (interpMethod.StartsWith("Log", false, null))
      {
        interpMethod = interpMethod.Substring(3);
        logrithm = true;
      }
      if (String.IsNullOrEmpty(interpMethod))
      {
        method = InterpMethod.Weighted;
      }
      else if (interpMethod == "Squared")
      {
        method = InterpMethod.Custom;
        // Never use literal string for type name.
        interpMethod = typeof(SquareLinearVolatilityInterp).FullName;
      }
      else
      {
        int n = interpMethod.Length - 1;
        if (n >= 2 && ((interpMethod[n] == '1' || interpMethod[n] == '2')
          && (interpMethod[n - 1] == 'C' || interpMethod[n - 1] == 'c')))
        {
          firstOrder = interpMethod[n] == '1';
          interpMethod = interpMethod.Substring(0, n - 1);
        }
        method = (InterpMethod)Enum.Parse(typeof(InterpMethod), interpMethod);
      }
      return new InterpScheme
      {
        TypeName = interpMethod,
        Method = method,
        IsWeighted = weighted,
        IsLogarithmic = logrithm,
        Order = firstOrder ? InterpOrder.First
          : (method == InterpMethod.Tension
            ? InterpOrder.Second : InterpOrder.None),
        upperExtrap_ = new ExtrapScheme { Method = upperExtrapMethod, MaxValue = max, MinValue = min},
        lowerExtrap_ = new ExtrapScheme { Method = lowerExtrapMethod, MaxValue = max, MinValue = min }
      };
    }

    ///<summary>
    ///</summary>
    ///<param name="interpMethod"></param>
    ///<param name="riskExtrapMethod"></param>
    ///<returns></returns>
    public static InterpScheme FromString(string interpMethod,
      string riskExtrapMethod)
    {
      switch (riskExtrapMethod)
      {
        case "SmoothConst":
          return Parse(String.Format("{0}; {1}/{2}",interpMethod, ExtrapMethod.Smooth, ExtrapMethod.Const));
        case "ConstSmooth":
          return Parse(String.Format("{0}; {1}/{2}",interpMethod, ExtrapMethod.Const, ExtrapMethod.Smooth));
        default:
          return Parse(String.Format("{0}; {1}/{2}", interpMethod, riskExtrapMethod, riskExtrapMethod));
      }
    }

    /// <summary>
    ///   Parse the interp scheme from a string.
    /// </summary>
    /// <param name="input">The input string</param>
    /// <returns>The interpolation scheme</returns>
    public static InterpScheme Parse(string input)
    {
      return FromString(input, ExtrapMethod.None, ExtrapMethod.None);
    }

    /// <summary>
    ///   Parses the method input, allowing the formats "InterpMethod",
    ///   "InterpMethod; ExtrapMethod", or "InterpMethod; LowerExtrap/UpperExtrap".
    /// </summary>
    /// <param name="interpMethod">The interp method.</param>
    /// <param name="upperExtrapMethod">The upper extrap method if not specified in <paramref name="interpMethod" />.</param>
    /// <param name="lowerExtrapMethod">The lower extrap method if not specified in <paramref name="interpMethod" />.</param>
    private static void ParseInput(ref string interpMethod,
      ref ExtrapMethod upperExtrapMethod, ref ExtrapMethod lowerExtrapMethod)
    {
      var pos = interpMethod.IndexOf(';');
      if (pos <= 0) return;

      var extrapMethod = interpMethod.Substring(pos + 1).Trim();
      interpMethod = interpMethod.Substring(0, pos).Trim();
      if (String.IsNullOrEmpty(extrapMethod)) return;

      string low = extrapMethod, upp = extrapMethod;
      if (extrapMethod.IndexOf('/') > 0)
      {
        var s = extrapMethod.Split('/');
        low = s[0].Trim();
        upp = s[1].Trim();
      }
      if (!String.IsNullOrEmpty(low))
      {
        Enum.TryParse<ExtrapMethod>(low, true, out lowerExtrapMethod);
      }
      if (!String.IsNullOrEmpty(upp))
      {
        Enum.TryParse<ExtrapMethod>(upp, true, out upperExtrapMethod);
      }
      return;
    }

    /// <summary>
    ///   Create an InterpScheme from an Interp object.
    /// </summary>
    /// <param name="interp">The interp.</param>
    /// <returns>InterpScheme</returns>
    public static InterpScheme FromInterp(Interp interp)
    {
      if (interp == null)
        throw new NullReferenceException("Interpolator cannot be null.");

      bool weighted = false, logadpt = false;

      if (interp is WeightedAdapter)
      {
        interp = ((WeightedAdapter)interp).getInterp();
        weighted = true;
      }
      else if (interp is LogAdapter)
      {
        interp = ((LogAdapter)interp).getInterp();
        logadpt = true;
      }

      double[] tensionFactors = null;
      InterpOrder order = InterpOrder.None;
      InterpMethod method;
      if (interp is Flat)
        method = InterpMethod.Flat;
      else if (interp is Linear)
        method = logadpt ? InterpMethod.LogLinear : InterpMethod.Linear;
      else if (interp is Weighted)
        method = logadpt ? InterpMethod.LogWeighted : InterpMethod.Weighted;
      else if (interp is Cubic)
        method = logadpt ? InterpMethod.LogCubic : InterpMethod.Cubic;
      else if (interp is Quadratic)
        method = logadpt ? InterpMethod.LogQuadratic : InterpMethod.Quadratic;
      else if (interp is PCHIP)
        method = InterpMethod.PCHIP;
      else if (interp is Tension)
      {
        method = InterpMethod.Tension;
        order = ((((Tension)interp).Flags & Tension.InterpFlags.FirstOrder)
          != 0) ? InterpOrder.First : InterpOrder.Second;
        tensionFactors = ((Tension)interp).TensionFactors;
      }
      else if (interp is DelegateCurveInterp)
        method = InterpMethod.Delegate;
      else if (interp is IInterpSchemeConverter)
      {
        var scheme = ((IInterpSchemeConverter)interp).ToInterpScheme();
        scheme.TypeName = interp.GetType().FullName;
        scheme.Method = InterpMethod.Custom;
        return scheme;
      }
      else
        throw new ArgumentException("Invalid interpolation obj");

      return new InterpScheme
      {
        TypeName = interp.GetType().FullName,
        Method = method,
        IsWeighted = weighted,
        IsLogarithmic = logadpt,
        Order = order,
        UpperEndCondition = interp.UpperEnd,
        LowerEndCondition = interp.LowerEnd,
        TensionFactors = tensionFactors,
        upperExtrap_ = ExtrapScheme.FromExtrap(interp.getUpperExtrap()),
        lowerExtrap_ = ExtrapScheme.FromExtrap(interp.getLowerExtrap())
      };
    }

    /// <summary>
    ///   Create an Interp object based on the Interp scheme.
    /// </summary>
    /// <returns>InterpObject.</returns>
    public Interp ToInterp()
    {
      Func<ExtrapScheme, Extrap> toExtrap = (e) => e == null ? null : e.ToExtrap();
      using (Extrap upper = toExtrap(UpperExtrapScheme))
      {
        using (Extrap lower = toExtrap(LowerExtrapScheme))
        {
          return MakeInterp(upper, lower);
        }
      }
    }

    private Interp MakeInterp(Extrap upper, Extrap lower)
    {
      Interp interp;
      switch (Method)
      {
        case InterpMethod.Flat:
          interp = new Flat(Double.Epsilon);
          break;
        case InterpMethod.Linear:
        case InterpMethod.LogLinear:
          interp = new Linear(lower, upper);
          break;
        case InterpMethod.Weighted:
        case InterpMethod.LogWeighted:
          interp = new Weighted(lower, upper);
          break;
        case InterpMethod.Cubic:
        case InterpMethod.LogCubic:
          interp = new Cubic(lower, upper);
          break;
        case InterpMethod.Quadratic:
        case InterpMethod.LogQuadratic:
          interp = new Quadratic(lower, upper);
          break;
        case InterpMethod.PCHIP:
          interp = new PCHIP(lower, upper);
          break;
        case InterpMethod.Tension:
          {
            Tension t = new Tension(lower, upper);
            if (Order == InterpOrder.First)
              t.Flags |= Tension.InterpFlags.FirstOrder;
            if (TensionFactors != null && TensionFactors.Length > 0)
              t.TensionFactors = TensionFactors;
            interp = t;
          }
          break;
        case InterpMethod.Delegate:
          {
            return new DelegateCurveInterp(null, null);
          }
        case InterpMethod.Custom:
          {
            var type = Type.GetType(TypeName);
            var cvt = Activator.CreateInstance(type) as IInterpSchemeConverter;
            if (cvt == null)
            {
              throw new ArgumentException(String.Format(
                "Invalid interpolation type: {0}", TypeName));
            }
            interp = cvt.FromInterpScheme(this);
          }
          break;
        default:
          throw new ArgumentException(String.Format(
            "Cannot make interpolator for method: {0}", Method));
      }
      interp.UpperEnd = UpperEndCondition;
      interp.LowerEnd = LowerEndCondition;

      var logarithmic = IsLogarithmic;
      switch (Method)
      {
        case InterpMethod.LogLinear:
        case InterpMethod.LogWeighted:
        case InterpMethod.LogCubic:
        case InterpMethod.LogQuadratic:
          logarithmic = true;
          break;
        default:
          break;
      }

      if (logarithmic)
        interp = new LogAdapter(lower, upper, interp, true);
      else if (IsWeighted && Method != InterpMethod.Weighted)
        interp = new WeightedAdapter(lower, upper, interp, true);
      return interp;
    }

    // The following 2 function have been copied from class InterpAddin in Interp.cs
    /// <summary>
    /// 
    /// </summary>
    /// <param name="interpMethod"></param>
    /// <param name="extrapMethod"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static Interp InterpFromName(
      object interpMethod, ExtrapMethod extrapMethod,
      double min, double max)
    {
      return InterpFromName(interpMethod, extrapMethod, min, max,
                            new Interp.EndCondition(), new Interp.EndCondition());
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="interpMethod"></param>
    /// <param name="extrapMethod"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <param name="lowerEnd"></param>
    /// <param name="upperEnd"></param>
    /// <returns></returns>
    public static Interp InterpFromName(
      object interpMethod, ExtrapMethod extrapMethod,
      double min, double max,
      Interp.EndCondition lowerEnd, Interp.EndCondition upperEnd)
    {
      if (interpMethod is string)
      {
        var input = interpMethod.ToString();
        InterpMethod im;
        if (Enum.TryParse<InterpMethod>(input, true, out im))
          return InterpFactory.FromMethod(im, extrapMethod, min, max);

        InterpScheme scheme;
        try
        {
          scheme = InterpScheme.FromString(
            input, extrapMethod, extrapMethod);
          scheme.LowerEndCondition = lowerEnd;
          scheme.UpperEndCondition = upperEnd;
        }
        catch
        {
          return null; // TODO: perhaps throw exception here
        }
        scheme.UpperExtrapScheme.MinValue =
          scheme.LowerExtrapScheme.MinValue = min;
        scheme.UpperExtrapScheme.MaxValue =
          scheme.LowerExtrapScheme.MaxValue = max;
        return scheme.ToInterp();
      }
      if (interpMethod is InterpMethod)
      {
        return InterpFactory.FromMethod(
          (InterpMethod)interpMethod, extrapMethod);
      }
      if (!(interpMethod is Interp))
      {
        throw new ToolkitException("Invalid interpMethod.");
      }
      return (Interp)interpMethod;
    }

    private ExtrapScheme upperExtrap_ = new ExtrapScheme();
    private ExtrapScheme lowerExtrap_ = new ExtrapScheme();
  }

  public static class InterpExtensions
  {
    public static Interp GetRealInterp(this Interp interp)
    {
      if (interp == null)
        return null;

      for (InterpAdapter a = interp as InterpAdapter;
        a != null;
        interp = a.InnerInterp, a = interp as InterpAdapter) ;
      return interp;
    }

    public static void SetTensionFactors(this Interp interp, double[] factors)
    {
      Tension t = GetRealInterp(interp) as Tension;
      if (t != null)
        t.SetTension(factors);
      return;
    }
  }

}  
