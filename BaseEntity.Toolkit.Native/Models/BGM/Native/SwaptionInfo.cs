using System;
using System.Runtime.InteropServices;

namespace BaseEntity.Toolkit.Models.BGM.Native
{
  [StructLayout(LayoutKind.Sequential)]
  [Serializable]
  public struct SwaptionInfo
  {
    public BaseEntity.Toolkit.Base.Dt Date;
    public double Rate;
    public double Level;
    public double Coupon;
    public double Volatility;
    public double Value;
    public BaseEntity.Toolkit.Base.OptionType OptionType;
    public double Accuracy;
    public int Steps;
  }
}
