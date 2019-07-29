using System;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Convenience data transfer class for specifying an Fx curve from spot + fwds
  /// </summary>
  [Serializable]
  public class FxData
  {
    /// <summary>
    /// Get FxCurve
    /// </summary>
    /// <returns>FxCurve</returns>
    public FxCurve GetFxCurve()
    {
      Dt[] dates = new Dt[TenorNames.Length];
      for (int i = 0; i < TenorNames.Length; i++)
      {
        dates[i] = Dt.Add(AsOf, TenorNames[i]);
      }
      return new FxCurve(new FxRate(AsOf, 2, FromCcy, ToCcy, SpotFx, FromCcyCalendar, ToCcyCalendar),
                         dates, FwdFx, TenorNames, Name);
    }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set;}
    /// <summary>
    /// As of date
    /// </summary>
    public Dt AsOf { get; set;}
    /// <summary>
    /// From currency
    /// </summary>
    public Currency FromCcy { get; set; }
    /// <summary>
    /// To currency
    /// </summary>
    public Currency ToCcy { get; set; }
    /// <summary>
    /// From calendar
    /// </summary>
    public Calendar FromCcyCalendar { get; set; }
    /// <summary>
    /// To calendar
    /// </summary>
    public Calendar ToCcyCalendar { get; set; }
    /// <summary>
    /// Spot fx level at as of
    /// </summary>
    public double SpotFx { get; set; }
    /// <summary>
    /// Forward fx points
    /// </summary>
    public double[] FwdFx { get; set; }
    /// <summary>
    /// Forward tenor names
    /// </summary>
    public string[] TenorNames { get; set; }
  }
}