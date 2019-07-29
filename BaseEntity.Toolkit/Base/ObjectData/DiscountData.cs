/*
 * DiscountData.cs
 *
 *  -2008. All rights reserved.
 *
 * A simple class hold discount curves data
 *
 * This is an internal class.  Used in basket test programs.  
 *
 */
using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Base
{
  /// <exclude />
  [Serializable]
	public class DiscountData
  {

    #region Methods

    /// <exclude />
		public DiscountData()
		{
			Name = null;
			AsOf = Dt.Today().ToStr("%D");
			Currency = Currency.USD;
			Category = null;
			Interp = InterpMethod.Weighted;
			Extrap = ExtrapMethod.Const;
			Factors = null;
			Bootst = null;
		}

		/// <exclude />
		public DiscountCurve
		GetDiscountCurve()
		{
      var curve = _curveBuilt;
		  if (curve != null) return curve;

      Dt asOfDate = Dt.FromStr(AsOf, "%D");

			if( Bootst == null )
			{
				curve = new DiscountCurve( asOfDate );
				curve.Interp = InterpFactory.FromMethod(Interp, Extrap);
				curve.Ccy = Currency;
				curve.Category = Category;
				curve.Name = Name;

				for( int i = 0; i < Factors.Length; i++ )
					if( Factors[i].Discount > 0.0 )
					  curve.Add( Dt.FromStr(Factors[i].Maturity, "%D"),
											 Factors[i].Discount );
			}
			else
			{
				var b = Bootst;

				var calibrator =
					new DiscountBootstrapCalibrator(asOfDate, asOfDate);
				calibrator.SwapInterp = InterpFactory.FromMethod(b.SwapInterp, b.SwapExtrap);

				curve = new DiscountCurve(calibrator);
				curve.Interp = InterpFactory.FromMethod(Interp, Extrap);
				curve.Ccy = Currency;
				curve.Category = Category;
				curve.Name = Name;

				// Add MM rates
        if (null != b.MmTenors && b.MmTenors.Length > 0)
        {
          for (int i = 0; i < b.MmTenors.Length; i++)
            if (b.MmRates[i] > 0.0)
            {
              curve.AddMoneyMarket(b.MmTenors[i],
                b.MmMaturities == null ? Dt.Add(asOfDate,b.MmTenors[i]) : Dt.FromStr(b.MmMaturities[i], "%D"),
                b.MmRates[i], b.MmDayCount);
            }
        }
				// Add swap rates
				if( null != b.SwapTenors && b.SwapTenors.Length > 0 )
				{
				  for( int i = 0; i < b.SwapTenors.Length; i++ )
						if( b.SwapRates[i] > 0.0 )
						  curve.AddSwap( b.SwapTenors[i],
                b.SwapMaturities == null ? Dt.Add(asOfDate,b.SwapTenors[i]) : Dt.FromStr(b.SwapMaturities[i], "%D"),
                b.SwapRates[i], b.SwapDayCount, b.SwapFrequency, BDConvention.None, Calendar.None );
				}

				curve.Fit();
			}
		  if (EnableBuildOnce) _curveBuilt = curve;

			return curve;
    }
    #endregion Methods

    #region Types

    //
		// class for discount factors
		//
		/// <exclude />
		public class Factor {
			/// <exclude />
			public string Maturity;
			/// <exclude />
			public double Discount;
		};

		/// <exclude />
		public class Bootstrap {
			/// <exclude />
			public DayCount MmDayCount;
			/// <exclude />
			public string [] MmTenors;
			/// <exclude />
			public string [] MmMaturities;
			/// <exclude />
			public double [] MmRates;
			/// <exclude />
			public DayCount SwapDayCount;
			/// <exclude />
			public Frequency SwapFrequency;
			/// <exclude />
			public InterpMethod SwapInterp;
			/// <exclude />
			public ExtrapMethod SwapExtrap;
			/// <exclude />
			public string [] SwapTenors;
			/// <exclude />
			public string [] SwapMaturities;
			/// <exclude />
			public double [] SwapRates;
		};

    #endregion Types

    #region Data

		/// <exclude />
		public string Name;
		/// <exclude />
		public string AsOf;
		/// <exclude />
		public Currency Currency;
		/// <exclude />
		public string Category;
		/// <exclude />
		public InterpMethod Interp;
		/// <exclude />
		public ExtrapMethod Extrap;
		/// <exclude />
		public Factor[] Factors;
		/// <exclude />
		public Bootstrap Bootst;
    /// <exclude />
    public bool EnableBuildOnce;

    private DiscountCurve _curveBuilt;
    #endregion
  }; // class DiscountData

}
