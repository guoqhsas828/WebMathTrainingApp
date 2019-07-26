/*
 * ExtrapFactory
 *
  */
using System;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
	///   Utility methods for ExtrapMethod
	/// </summary>
	///
  public class ExtrapFactory
  {
		/// <summary>
		///   Create an Extrap object corresponding the given ExtrapMethod
		/// </summary>
		/// <returns>Extrap from ExtrapMethod</returns>
		///
		public static Extrap
		FromMethod(ExtrapMethod type)
		{
			switch (type)
			{
				case ExtrapMethod.None:
					return null;
				case ExtrapMethod.Const:
					return new Const();
				case ExtrapMethod.Smooth:
					return new Smooth();
				default:
					throw new ArgumentOutOfRangeException("type", String.Format("Invalid ExtrapMethod: {0}", type));
			}
		}

    /// <summary>
		///   Create an Extrap object corresponding the given ExtrapMethod
		/// </summary>
		/// <param name="type">Extrapolation type</param>
		/// <param name="min">Minimum return value (for Smooth method only)</param>
		/// <param name="max">Maximum return value (for Smooth method only)</param>
		/// <returns>Extrap from ExtrapMethod</returns>
		///
		public static Extrap
		FromMethod(ExtrapMethod type, double min, double max)
		{
			switch (type)
			{
				case ExtrapMethod.None:
					return null;
				case ExtrapMethod.Const:
					return new Const();
				case ExtrapMethod.Smooth:
					return new Smooth(min, max);
				default:
					throw new ArgumentOutOfRangeException("type", String.Format("Invalid ExtrapMethod: {0}", type));
			}
		}

  } // class ExtrapFactory

}  
