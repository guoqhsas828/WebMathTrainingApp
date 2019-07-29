/*
 * SolverFactory
 *
 *  -2008. All rights reserved.
 *
 */

using System;

namespace BaseEntity.Toolkit.Numerics
{

  /// <summary>
	///   Solver-related utility methods.
	/// </summary>
  public class SolverFactory
  {
		/// <summary>
		///   Create an Solver object corresponding the given SolverMethod
		/// </summary>
		/// <returns>Solver from SolverMethod</returns>
    public static Solver
		FromMethod(SolverMethod type)
    {
			switch( type )
			{
			case SolverMethod.Generic:
				return new Generic();
			case SolverMethod.Brent:
				return new Brent();
			case SolverMethod.Newton:
				return new Newton();
			case SolverMethod.Bisection:
				return new Bisection();
			default:
        throw new ArgumentOutOfRangeException( "type" );
			}
		} // FromMethod()

  } // class SolverFactory

} 
