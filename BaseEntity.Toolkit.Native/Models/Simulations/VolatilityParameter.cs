using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// The types of the volatility processes,
  ///  static or stochastic.
  /// </summary>
  public enum VolatilityProcessKind
  {
    /// <summary>
    /// The deterministic term structure
    /// for non-stochastic volatilities.
    /// </summary>
    Static,

    /// <summary>
    /// The Heston volatility process
    /// </summary>
    Heston,

    /// <summary>
    /// The SABR volatility process
    /// </summary>
    Sabr
  };

  /// <summary>
  /// Interface representing volatility process parameters
  /// </summary>
  public interface IVolatilityProcessParameter
  {
    /// <summary>
    /// Gets the volatility process kind.
    /// </summary>
    /// <value>The kind.</value>
    VolatilityProcessKind Kind { get; }
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential)]
  public class HestonProcessParameter : IVolatilityProcessParameter
  {
    public double InitialSigma;
    public double Theta, Kappa, Nu, Rho;

    public VolatilityProcessKind Kind => VolatilityProcessKind.Heston;
  };

  [Serializable]
  [StructLayout(LayoutKind.Sequential)]
  public class SabrProcessParameter : IVolatilityProcessParameter
  {
    public double Alpha, Beta, Nu, Rho;

    public VolatilityProcessKind Kind => VolatilityProcessKind.Sabr;
  };

  [StructLayout(LayoutKind.Sequential)]
  public struct VolatilityParameter
  {
    public VolatilityProcessKind Kind;
    public IntPtr Ptr;

    public static Pinned<VolatilityParameter> Pin(
      IVolatilityProcessParameter obj)
    {
      if (obj.Kind == VolatilityProcessKind.Static)
      {
        var curves = (IReadOnlyList<NativeCurve>)obj;
        return Deterministic(curves[0]);
      }

      var gch = GCHandle.Alloc(obj, GCHandleType.Pinned);
      return new Pinned<VolatilityParameter>
      {
        Data = new VolatilityParameter
        {
          Kind = obj.Kind,
          Ptr = gch.AddrOfPinnedObject()
        },
        Gch = gch
      };
    }

    public static Pinned<VolatilityParameter> Deterministic(
      NativeCurve voalitilityCurve)
    {
      return new Pinned<VolatilityParameter>
      {
        Data = new VolatilityParameter
        {
          Kind = VolatilityProcessKind.Static,
          Ptr = NativeCurve.getCPtr(voalitilityCurve).Handle
        }
      };
    }

  }

  public struct Pinned<T> : IDisposable
  {
    public T Data;
    public GCHandle Gch;

    public void Dispose()
    {
      if (Gch.IsAllocated) Gch.Free();
    }
  }
}
