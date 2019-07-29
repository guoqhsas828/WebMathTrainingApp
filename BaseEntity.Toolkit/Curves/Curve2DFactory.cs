/*
 * Curve2DFactory.cs
 *
 * Helper factory classes for 2-dimentional curve objects
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Curves
{
  //
	// <summary>
	//   Helper factory methods for Courve2D objects
	// </summary>
	//
	// <remarks>
	//   This class provides constructor and merge routines for Curve2D objects.
	// </remarks>
  //
	/// <exclude />
	public abstract class Curve2DFactory : BaseEntityObject
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(Curve2DFactory));

		#region Constructors

		/// <exclude/>
		protected Curve2DFactory() {}

		#endregion // Constructors

		#region Methods
		/// <exclude />
		public static void
		Initialize( Dt asOf,
								Dt[] dates,
								int start,
								int length,
								double[] levels,
								int nGroups,
								Curve2D distributions )
		{
		  if( start >= dates.Length )
				throw new ArgumentException(String.Format("start ({0}) must be less than array length {1}", start, dates.Length));
			if( length <= 0 )
				throw new ArgumentException(String.Format("length ({0}) must be positive", length));

		  int nDates = Math.Min(start + length, dates.Length) - start;
			int nLevels = levels.Length;
      distributions.Initialize(nDates, nLevels, nGroups);
      distributions.SetAsOf(asOf);

      for( int i = 0; i < nDates; ++i ) {
        distributions.SetDate( i, dates[i+start] );
      }
      
      for( int i = 0; i < nLevels; ++i )
        distributions.SetLevel( i, levels[i] );

			return;
		}

		/// <exclude />
		public static void
		Merge( Curve2D [] curves, Curve2D result )
		{
		  int totalDates = 0;
			for( int i = 0; i < curves.Length; ++i )
				totalDates += curves[i].NumDates();

			result.Initialize( totalDates, curves[0].NumLevels(), curves[0].NumGroups() );
      result.SetAsOf( curves[0].GetAsOf() );

			int nLevels = curves[0].NumLevels();
      for( int i = 0; i < nLevels; ++i ) {
        result.SetLevel( i, curves[0].GetLevel(i) );
			}

			for( int i = 0, idx = 0; i < curves.Length; ++i )
			{
				Curve2D srcCurve = curves[i];
				int nDates = srcCurve.NumDates();
				for( int d = 0; d < nDates; ++idx, ++d )
				{
					result.SetDate( idx, srcCurve.GetDate(d) );
					result.CopyValues( idx, srcCurve, d );
				}
			}

			return ;
		}

		#endregion // Methods
	}


}
