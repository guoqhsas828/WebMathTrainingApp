using System.ComponentModel;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Risk
{
  ///<summary>
  ///   Enumeration of known interpolation methods.
  ///   Extends toolkit enumeration
  ///</summary>
  public enum RiskInterpMethod
  {
    /// <summary>Linear</summary>
    Linear,
    /// <summary>LogLinear</summary>
    [Browsable(false)]
    LogLinear,
    /// <summary>Flat interpolation</summary>
    [Browsable(false)]
    Flat,
    /// <summary>Weighted</summary>
    Weighted,
    /// <summary>LogWeighted</summary>
    [Browsable(false)]
    LogWeighted,
    /// <summary>Cubic</summary>
    Cubic,
    /// <summary>LogCubic</summary>
    [Browsable(false)]
    LogCubic,
    /// <summary>Cubic</summary>
    Quadratic,
    /// <summary>LogCubic</summary>
    [Browsable(false)]
    LogQuadratic,
    /// <summary>PCHIP</summary>
    PCHIP,
    /// <summary>Tension spline continuous C1</summary>
    TensionC1 = 0x100,
    /// <summary>Tension spline continuous C2</summary>
    TensionC2 = 0x101,
    /// <summary>Weighted Quadratic</summary>
    WeightedQuadratic = 0x102,
    /// <summary>Weighted Cubic</summary>
    WeightedCubic = 0x102,
    /// <summary>Weighted PCHIP</summary>
    WeightedPCHIP = 0x103,
    /// <summary>Weighted tension spline continuous C1</summary>
    WeightedTensionC1 = 0x104,
    /// <summary>Weighted tension spline continuous C2</summary>
    WeightedTensionC2 = 0x105
  }

  ///<summary>
  ///   Enumeration of known upper/lower extrapolation methods.
  ///   Extends toolkit enumeration
  ///</summary>
  public enum RiskExtrapMethod
  {
    /// <summary>Constant extrapolation on both upper/lower ends</summary>
    Const,

    /// <summary>Smooth extrapolation on both upper/lower ends</summary>
    Smooth,
    /// <summary>
    /// None extrapolation on both upper/lower ends
    /// </summary>
    None,
    /// <summary>
    /// Constant extrapolation on lower end and Smooth extrapolation on upper end
    /// </summary>
    ConstSmooth,
    /// <summary>
    /// Smooth extrapolation on lower end and Constant extrapolation on upper end
    /// </summary>
    SmoothConst
  }

  ///<summary>
  ///</summary>
  public class InterpMethodUtil
  {
    ///<summary>
    ///</summary>
    ///<param name="interpMethod"></param>
    ///<param name="upperExtrapMethod"></param>
    ///<param name="lowerExtrapMethod"></param>
    ///<returns></returns>
    public static InterpScheme From(RiskInterpMethod interpMethod,
                                    ExtrapMethod upperExtrapMethod,
                                    ExtrapMethod lowerExtrapMethod)
    {
      string interpMethodString = interpMethod.ToString();
      return InterpScheme.FromString(interpMethodString,
                                     upperExtrapMethod,
                                     lowerExtrapMethod);
    }

    ///<summary>
    ///</summary>
    ///<param name="interpMethod"></param>
    ///<param name="extrapMethod"></param>
    ///<returns></returns>
    public static InterpScheme From(RiskInterpMethod interpMethod,
                                    ExtrapMethod extrapMethod)
    {
      return From(interpMethod,
                  extrapMethod,
                  extrapMethod);
    }

    ///<summary>
    ///</summary>
    ///<param name="interpMethod"></param>
    ///<param name="riskExtrapMethod"></param>
    ///<returns></returns>
    public static InterpScheme From(RiskInterpMethod interpMethod,
      RiskExtrapMethod riskExtrapMethod)
    {
      switch (riskExtrapMethod)
      {
        case RiskExtrapMethod.SmoothConst:
          return From(interpMethod, ExtrapMethod.Const, ExtrapMethod.Smooth);
        case RiskExtrapMethod.ConstSmooth:
          return From(interpMethod, ExtrapMethod.Smooth, ExtrapMethod.Const);
        default:
          return From(interpMethod, (ExtrapMethod)((int)riskExtrapMethod));

      }
    }
  }
}
