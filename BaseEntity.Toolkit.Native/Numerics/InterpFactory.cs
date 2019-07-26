/*
 * InterpFactory
 *
 * Copyright (c)   2002-2008. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Utility methods for InterpMethods
  /// </summary>
  public class InterpFactory
  {
    /// <summary>
    ///   Create an Interp object corresponding the given InterpMethod and ExtrapMethod
    /// </summary>
    ///
    /// <returns>Interp object from InterpMethod and ExtrapMethod</returns>
    ///
    public static Interp
    FromMethod(InterpMethod type, ExtrapMethod etype, params double[] pars)
    {
      Interp interp = null;

      using (Extrap extrap = ExtrapFactory.FromMethod(etype))
      {
        switch (type)
        {
          case InterpMethod.Flat:
            interp = new Flat(Double.Epsilon);
            break;
          case InterpMethod.Linear:
          case InterpMethod.LogLinear:
            interp = (extrap != null) ? new Linear(extrap, extrap) : new Linear();
            break;
          case InterpMethod.Weighted:
          case InterpMethod.LogWeighted:
            interp = (extrap != null) ? new Weighted(extrap, extrap) : new Weighted();
            break;
          case InterpMethod.Cubic:
          case InterpMethod.LogCubic:
            interp = (extrap != null) ? new Cubic(extrap, extrap) : new Cubic();
            break;
          case InterpMethod.Quadratic:
          case InterpMethod.LogQuadratic:
            interp = (extrap != null) ? new Quadratic(extrap, extrap) : new Quadratic();
            break;
          case InterpMethod.PCHIP:
            interp = (extrap != null) ? new PCHIP(extrap, extrap) : new PCHIP();
            break;
          case InterpMethod.Tension:
            interp = (extrap != null) ? new Tension(extrap, extrap) : new Tension();
            break;
          default:
            throw new ArgumentOutOfRangeException("type",
              String.Format("Invalid interpolation type: {0}", type));
        }
        switch (type)
        {
          case InterpMethod.LogLinear:
          case InterpMethod.LogWeighted:
          case InterpMethod.LogCubic:
          case InterpMethod.LogQuadratic:
            interp = new LogAdapter(extrap, extrap, interp, true);
            break;
          default:
            break;
        }
      }
      return interp;
    }

    /// <summary>
    ///   Create an Interp object corresponding the given InterpMethod and ExtrapMethod
    /// </summary>
    /// <param name="type">Interpolation type</param>
    /// <param name="etype">Extrapolation type</param>
    /// <param name="min">Minimum return value (for Smooth extrap method only)</param>
    /// <param name="max">Maximum return value (for Smooth extrap method only)</param>
    ///
    /// <returns>Interp object from InterpMethod and ExtrapMethod</returns>
    ///
    public static Interp
    FromMethod(InterpMethod type, ExtrapMethod etype, double min, double max)
    {
      // Sanity checks
      if (!(max > Double.MinValue))
        throw new ApplicationException("Extrap.Max must be greater than Double.MinValue ");
      if (!(min < Double.MaxValue))
        throw new ApplicationException("Extrap.Min must be less than Double.MaxValue ");

      Interp interp = null;
      using (Extrap extrap = ExtrapFactory.FromMethod(etype, min, max))
      {
        switch (type)
        {
          case InterpMethod.Flat:
            interp = new Flat(Double.Epsilon);
            break;
          case InterpMethod.Linear:
          case InterpMethod.LogLinear:
            interp = (extrap != null) ? new Linear(extrap, extrap) : new Linear();
            break;
          case InterpMethod.Weighted:
          case InterpMethod.LogWeighted:
            interp = (extrap != null) ? new Weighted(extrap, extrap) : new Weighted();
            break;
          case InterpMethod.Cubic:
          case InterpMethod.LogCubic:
            interp = (extrap != null) ? new Cubic(extrap, extrap) : new Cubic();
            break;
          case InterpMethod.Quadratic:
          case InterpMethod.LogQuadratic:
            interp = (extrap != null) ? new Quadratic(extrap, extrap) : new Quadratic();
            break;
          case InterpMethod.PCHIP:
            interp = (extrap != null) ? new PCHIP(extrap, extrap) : new PCHIP();
            break;
          case InterpMethod.Tension:
            interp = (extrap != null) ? new Tension(extrap, extrap) : new Tension();
            break;
          case InterpMethod.Squared:
            interp = (extrap != null)
              ? new SquareLinearVolatilityInterp(extrap, extrap)
              : new SquareLinearVolatilityInterp();
          break;
          default:
            throw new ArgumentOutOfRangeException("type", String.Format("Invalid interpolation type: {0}", type));
        }

        switch (type)
        {
          case InterpMethod.LogLinear:
          case InterpMethod.LogWeighted:
          case InterpMethod.LogCubic:
          case InterpMethod.LogQuadratic:
            interp = new LogAdapter(extrap, extrap, interp, true);
            break;
          default:
            break;
        }
      }
      return interp;
    }


    /// <summary>
    ///   Return the InterpMethod given an Interp object
    /// </summary>
    ///
    /// <returns>InterpMethod given Interp object</returns>
    ///
    public static InterpMethod
    ToInterpMethod( Interp interp )
    {
      if( interp is Flat )
        return InterpMethod.Flat;
      else if( interp is Linear )
        return InterpMethod.Linear;
      else if( interp is Weighted )
        return InterpMethod.Weighted;
      else if( interp is Cubic )
        return InterpMethod.Cubic;
      else if( interp is Quadratic )
        return InterpMethod.Quadratic;
      else if (interp is PCHIP)
        return InterpMethod.PCHIP;
      else if (interp is Tension)
        return InterpMethod.Tension;
      else if (interp is LogAdapter)
        return GetLogInterpMethod(interp);
      else if (interp is InterpAdapter)
        return InterpMethod.Custom;
      else if (interp is DelegateCurveInterp)
        return InterpMethod.Delegate;
      else 
        throw new ArgumentException( "Invalid interpolation obj" );
    }

    private static InterpMethod GetLogInterpMethod(Interp outerInterp)
    {
      var loginterp = outerInterp as LogAdapter;
      var interp = loginterp == null ? null : loginterp.InnerInterp;
      if (interp == null)
      {
        throw new ArgumentException("Invalid interpolation obj");
      }
      if (interp is Linear)
        return InterpMethod.LogLinear;
      else if (interp is Weighted)
        return InterpMethod.LogWeighted;
      else if (interp is Cubic)
        return InterpMethod.LogCubic;
      else if (interp is Quadratic)
        return InterpMethod.LogQuadratic;
      else
        return InterpMethod.Custom;
    }


    /// <summary>
    ///   Return the ExtrapMethod given an Interp object
    /// </summary>
    ///
    /// <returns>ExtrapMethod given Interp object</returns>
    ///
    public static ExtrapMethod
    ToExtrapMethod( Interp interp )
    {
      Extrap extrap = interp.getLowerExtrap();
      if( extrap is Const )
        return ExtrapMethod.Const;
      else if( extrap is Smooth )
        return ExtrapMethod.Smooth;
      else if( extrap == null )
        return ExtrapMethod.None;
      else
        throw new ArgumentException( "Invalid interpolation obj" );
    }

    /// <summary>
    ///   Get the upper bound of extrapolation
    /// </summary>
    /// <param name="interp">Interp object</param>
    /// <returns>The upper bound of extrapolation</returns>
    public static double GetUpperBound(Interp interp)
    {
      Extrap e = interp.getUpperExtrap();
      if (e is Smooth)
        return ((Smooth)e).GetpublicData_Max();
      return Double.MaxValue;
    }

    /// <summary>
    ///   Get the lower bound of extrapolation
    /// </summary>
    /// <param name="interp">Interp object</param>
    /// <returns>The lower bound of extrapolation</returns>
    public static double GetLowerBound(Interp interp)
    {
      Extrap e = interp.getLowerExtrap();
      if (e is Smooth)
        return ((Smooth)e).GetpublicData_Min();
      return Double.MinValue;
    }
  } // class InterpFactory
}  
