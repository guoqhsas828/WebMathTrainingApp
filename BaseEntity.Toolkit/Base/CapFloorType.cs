/*
 * CapFloorType.cs
 *
 *   2005-2010. All rights reserved.
 * 
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Cap or a Floor
  /// </summary>
  public enum CapFloorType
  {
    /// <summary>
    /// Option that pays when the rate exceed the strike
    /// </summary>
    Cap, 

    /// <summary>
    /// Option that pays when the rate is below the strike
    /// </summary>
    Floor
  }
}
