using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  [Serializable]
  partial class CurveArray : INativeSerializable, IComparable
  {
    private double detach_;

    
    /// <summary>
    /// Detachment point of the equity tranche whose expected loss was differentiated 
    /// </summary>
    public double Detach
    {
      get { return detach_; }
      set { detach_ = value; }
    }

    #region IComparable Members
    /// <summary>
    /// CompareTo implementation
    /// </summary>
    /// <param name="obj">CurveArray object</param>
    /// <returns></returns>
    public int CompareTo(Object obj)
    {
      var array = (CurveArray) obj;
      return (Detach < array.Detach) ? -1 : (Detach > array.Detach) ? 1 : 0;
    }

    #endregion


    #region ISerializable Members

    /// <summary>
    /// Get object info
    /// </summary>
    /// <param name="info">SerializationInfo object</param>
    /// <param name="context">Streaming context</param>
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (!swigCMemOwn)
        throw new ToolkitException("Object can not be serialized when swigCMemOwn is false.");
      var detach = detach_;
      var asOf = GetAsOf();
      var values = Get_publicState_values();
      var dates = Get_publicState_dates();
      info.AddValue("detach_", detach);
      info.AddValue("asOf_", asOf);
      info.AddValue("values_", values);
      info.AddValue("dates_", dates);
    }

    ///<exclude/>
    protected CurveArray(SerializationInfo info, StreamingContext context)
      : this((Dt)info.GetValue("asOf_", typeof(Dt)))
    {
      
      detach_ = (double) info.GetValue("detach_", typeof (double));
      var values = (double[]) info.GetValue("values_", typeof (double[]));
      var dates = (int[]) info.GetValue("dates_", typeof (int[]));
      Set_publicState(dates, values); 
    }

    #endregion
  }
}
