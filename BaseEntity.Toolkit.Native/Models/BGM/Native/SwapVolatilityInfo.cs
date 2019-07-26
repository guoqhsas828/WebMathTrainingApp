using System;
using System.Runtime.InteropServices;

namespace BaseEntity.Toolkit.Models.BGM.Native
{
  [StructLayout(LayoutKind.Sequential)]
  public struct SwapVolatilityInfo
  {
    public int First;  // First is the date index when the swap starts.
    public int Last;   // (Last + 1) is the date index of the swap maturity.
    public double Volatility;  // the swap volatility

    public override string ToString()
    {
      return String.Format("[{0},{1}] {2}", First, Last, Volatility);
    }
  }
}
