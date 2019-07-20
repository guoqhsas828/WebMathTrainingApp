// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Summary description for PropertyFormatters.
  /// </summary>
  public class PropertyFormatters
  {
    private static PropertyFormatters _me;

    /// <summary>
    /// Always get an instance through this factory method.
    /// We are a singleton.
    /// </summary>
    /// <returns></returns>
    public static PropertyFormatters GetPropertyFormatters()
    {
      if (_me == null)
      {
        _me = new PropertyFormatters();
      }

      return _me;
    }

    private PropertyFormatters()
    {}

    #region Format

    /// <summary>
    /// Convert the underlying data to a string
    /// </summary>
    /// <param name="propertyName">Name of property</param>
    /// <param name="o">Object this property belongs to</param>
    /// <returns>user readable representation of data</returns>
    public string Format(string propertyName, object o)
    {
      var cm = ClassCache.Find(o.GetType());

      if (cm == null)
      {
        return "";
      }

      var pm = cm.GetProperty(propertyName);

      return pm == null ? "" : pm.Format(o);
    }

    /// <summary>
    /// Formats the specified property meta.
    /// </summary>
    /// <param name="propertyMeta">The property meta.</param>
    /// <param name="o">The o.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns></returns>
    public string Format(PropertyMeta propertyMeta, object o, string propertyName = "Name")
    {
      return propertyMeta.Format(o, propertyName);
    }

    #endregion

    #region UnFormat

    /// <summary>
    /// Return a dataobject based on a user entered string
    /// </summary>
    /// <param name="pm">Property meta description</param>
    /// <param name="o">object property belongs to</param>
    /// <param name="sInput">string entered by user</param>
    /// <returns></returns>
    public object UnFormat(PropertyMeta pm, object o, string sInput)
    {
      // DEFAULT IS RETURN SAME STRING WITH NO CHANGES
      return sInput;
    }

    #endregion
  }
}