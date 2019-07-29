/*
 * SolverMethod
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Numerics
{
	/// <summary>
	///   Enumeration of known solvers in the C++ library.
	///   Handy interface for common uses.
	/// </summary>
  public enum SolverMethod
  {
		/// <summary>Generic (picks best)</summary>
    Generic,
		/// <summary>Brent method</summary>
		Brent,
		/// <summary>Newton method</summary>
		Newton,
		/// <summary>Bisection method</summary>
		Bisection
  }

} 
