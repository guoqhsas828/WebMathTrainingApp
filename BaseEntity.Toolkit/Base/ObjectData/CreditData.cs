/*
 * CreditData.cs
 *
 *  -2008. All rights reserved.
 *
 * A simple class hold credit curves data
 *
 * This is an internal class.  Used in basket test programs.  
 *
 */
using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Base
{
  /// <exclude />
  [Serializable]
	public class CreditData {
		/// <exclude />
		public CreditData()
		{
		  BasketSize = 100;
			AsOf = Dt.Today().ToStr("%D");
			Currency = Currency.USD;
			DayCount = DayCount.Actual360;
			Frequency = Frequency.Quarterly;
			Calendar = Calendar.NYB;
			Roll = BDConvention.Modified;
			Interp = InterpMethod.Weighted;
			Extrap = ExtrapMethod.Const;
			NegSPTreat = NegSPTreatment.Zero;
			ScalingFactor = 0;
			ScalingFactors = null;
			ScalingTenorNames = null;
			ScalingWeights = null;
			TenorNames = null;
			Tenors = null;
			Credits = null;
		}

		// validate the credit data
		/// <exclude />
		public void
		Validate()
		{
		  if( Credits == null )
				throw new NullReferenceException("No credit specified");
			if( Credits.Length != BasketSize && Credits.Length != 1 )
				throw new ArgumentException("Number of credits not match basket size");
		}

		// create recovery curves
		/// <exclude />
		public RecoveryCurve[]
		GetRecoveryCurves()
		{
		  Dt asOf = Dt.FromStr(AsOf, "%D");
		  RecoveryCurve[] rc = new RecoveryCurve[BasketSize];
			for (int i = 0; i < BasketSize; ++i) {
			  double r = ( Credits.Length == 1 ?
										 Credits[0].RecoveryRate :
										 Credits[i].RecoveryRate );
			  rc[i] = new RecoveryCurve(asOf, r);
				rc[i].RecoveryDispersion = ( Credits.Length == 1 ?
																		 Credits[0].RecoveryDispersion :
																		 Credits[i].RecoveryDispersion );
			}
			return rc;
		}

		// create a subset of recovery curves
		/// <exclude />
		public RecoveryCurve[]
		GetRecoveryCurves( double [] picks )
		{
		  if( picks.Length > BasketSize )
				throw new ArgumentException("Number of picks exceeds basket size");

			int nPicks = 0;
			for( int i = 0; i < picks.Length; ++i )
				if( picks[i] > 0.0 ) ++nPicks;

		  Dt asOf = Dt.FromStr(AsOf, "%D");
		  RecoveryCurve[] rc = new RecoveryCurve[nPicks];
			for (int i = 0, idx = 0; i < picks.Length; ++i) {
			  if( picks[i] > 0.0 )
				{
					double r = ( Credits.Length == 1 ?
											 Credits[0].RecoveryRate :
											 Credits[i].RecoveryRate );
					rc[idx] = new RecoveryCurve(asOf, r);
					rc[idx].RecoveryDispersion = ( Credits.Length == 1 ?
																				 Credits[0].RecoveryDispersion :
																				 Credits[i].RecoveryDispersion );
					++idx;
				}
			}
			return rc;
		}

    // create a subset of survival curves
    /// <exclude />
    public SurvivalCurve[]
    GetSurvivalCurves(DiscountCurve discountCurve)
		{
      return GetSurvivalCurves(discountCurve, new CreditPicks(this, (bool[])null));
		}

    // create a subset of survival curves
    /// <exclude />
    public SurvivalCurve[]
    GetSurvivalCurves(DiscountCurve discountCurve, string[] namesToPick)
    {
      return GetSurvivalCurves(discountCurve, new CreditPicks(this, namesToPick));
    }

    // create a subset of survival curves
    /// <exclude />
    public SurvivalCurve[]
    GetSurvivalCurves(DiscountCurve discountCurve, double[] picks)
    {
      return GetSurvivalCurves(discountCurve, new CreditPicks(this, picks));
    }

    // create a subset of survival curves
    /// <exclude />
    public SurvivalCurve[]
    GetSurvivalCurves(DiscountCurve discountCurve, double[] picks, string[] names)
    {
      return GetSurvivalCurves(discountCurve, new CreditPicks(this, picks, names));
    }

    // create survival curves
    /// <exclude />
    private SurvivalCurve[]
    GetSurvivalCurves(DiscountCurve discountCurve, CreditPicks picks)
    {
      Dt asOf = Dt.FromStr(AsOf, "%D");

      // find the maximum number of tenros
      int nTenors = 0;
      for (int i = 0; i < BasketSize; ++i)
      {
        Credit credit = (Credits.Length == 1 ?
                          Credits[0] : Credits[i]);
        if (credit.Quotes.Length > nTenors)
          nTenors = credit.Quotes.Length;
      }

      // storage spaces      
      double[] fees = new double[] { 0.0 };
      double[] recoveries = new double[] { 0.0 };
      List<Dt> tenorDates = new List<Dt>();
      List<double> premiums = new List<double>();
      List<string> tenorNames = new List<string>();

      // check scaling and set up scaling factors for each name
      CheckScaling();

      List<SurvivalCurve> scList = new List<SurvivalCurve>();
      foreach (Credit credit in picks)
      {
        tenorDates = new List<Dt>();
        premiums = new List<double>();
        tenorNames = new List<string>();
        Dt defaultDate = credit.DefaultDate == null ? Dt.Empty : Dt.FromStr(credit.DefaultDate, "%D");
        Dt recoveryDate = credit.RecoveryDate == null ? Dt.Empty : Dt.FromStr(credit.RecoveryDate, "%D");
        Credit.Quote[] quotes = credit.Quotes;
        for (int j = 0; j < quotes.Length; j++)
        {
          Credit.Quote q = quotes[j];
          Dt maturity = Dt.Empty;
          if (q.Spread > 0.0)
          {
            
            if (q.Maturity == null || q.Maturity.Length <= 0)
            {
              if (Tenors == null || Tenors.Length <= j)
                throw new ArgumentException("Invalid tenor data");
              maturity = Dt.FromStr(Tenors[j], "%D");
            }
            else
              maturity = Dt.FromStr(q.Maturity, "%D");
            tenorDates.Add(maturity);
            tenorNames.Add(this.TenorNames[j]);
            premiums.Add(q.Spread);
          }
        }
        for (int j = quotes.Length; j < nTenors; ++j)
          premiums.Add(0.0);
        recoveries[0] = credit.RecoveryRate;

        SurvivalCurve curve;
        try
        {
          curve = SurvivalCurve.FitCDSQuotes(asOf, this.Currency, credit.Category, this.DayCount,
            this.Frequency, this.Roll, this.Calendar, this.Interp, this.Extrap, this.NegSPTreat,
            discountCurve, tenorNames.ToArray(), tenorDates.ToArray(), fees, premiums.ToArray(),
            recoveries, credit.RecoveryDispersion, credit.ForceFit,
            recoveryDate.IsEmpty() ? new[] {defaultDate} : new[] {defaultDate, recoveryDate});
        }
        catch
        {
          scList.Add(null); continue;
          //curve = SurvivalCurve.FitCDSQuotes(asOf, this.Currency, credit.Category, this.DayCount,
          //  this.Frequency, this.Roll, this.Calendar, this.Interp, this.Extrap, this.NegSPTreat,
          //  discountCurve, tenorNames, tenorDates, fees, premiums, recoveries, credit.RecoveryDispersion,
          //  true/*credit.ForceFit*/, defaultDate);
        }

        if (credit.scalingFactors_ != null)
          curve = SurvivalCurve.Scale(curve, ScalingTenorNames, credit.scalingFactors_, true);

        curve.Name = credit.Name;
        scList.Add(curve);
      }

      return scList.ToArray();
    }

    #region Credit Iterator
    class CreditPicks
    {
      public CreditPicks(CreditData cd, double[] picks)
        : this(cd, GetPicks(picks))
      { }

      public CreditPicks(CreditData cd, string[] names)
        : this(cd, GetPicks(names, cd.Credits))
      { }

      public CreditPicks(CreditData cd, double[] picks, string[] names)
        : this(cd, GetPicks(picks, names, cd.Credits))
      { }

      public CreditPicks(CreditData cd, bool[] picks)
      {
        if (picks != null && picks.Length == 0)
          picks = null;
        if (picks != null && picks.Length != cd.BasketSize)
        {
          throw new ArgumentException(String.Format(
            "Picks length ({0}) not match basket size ({1})",
            picks.Length, cd.BasketSize));
        }
        basketSize_ = cd.BasketSize;
        credits_ = cd.Credits;
        picks_ = picks;
      }

      public IEnumerator<Credit> GetEnumerator()
      {
        for (int i = 0; i < basketSize_; ++i)
          if (picks_ == null || picks_[i])
            yield return (credits_.Length == 1 ? credits_[0] : credits_[i]);
      }
      private int basketSize_;
      private Credit[] credits_;
      private bool[] picks_;

      static bool[] GetPicks(double[] prins)
      {
        if (prins == null) return null;
        bool[] picks = new bool[prins.Length];
        for (int i = 0; i < prins.Length; ++i)
          picks[i] = (prins[i] > 0.0);
        return picks;
      }

      static bool[] GetPicks(string[] names, Credit[] credits)
      {
        if (names == null) return null;
        bool[] picks;

        // all name share the same data
        if (credits.Length == 1)
        {
          picks = new bool[names.Length];
          for (int i = 0; i < picks.Length; ++i)
            picks[i] = true;
          return picks;
        }

        // subset of names
        Dictionary<string, bool> ht = new Dictionary<string, bool>();
        for (int i = 0; i < names.Length; ++i)
          ht.Add(names[i], false);
        picks = new bool[credits.Length];
        for (int i = 0; i < credits.Length; ++i)
        {
          string name = credits[i].Name;
          if (ht.ContainsKey(name) && !ht[name])
            picks[i] = ht[name] = true;
        }
        return picks;
      }

      // find picks
      static bool[] GetPicks(double[] picks, string[] names, Credit[] credits)
      {
        if (names.Length != picks.Length)
          throw new System.ArgumentException("picks and names have different lengths");
        // build a hash table
        Dictionary<string, int> h = new Dictionary<string, int>();
        for (int i = 0; i < credits.Length; ++i)
          h.Add(credits[i].Name, i);
        bool[] newPicks = new bool[credits.Length];
        for (int i = 0; i < picks.Length; ++i)
          if (picks[i] > 0.0)
            newPicks[ h[names[i]] ] = (picks[i] > 0.0);
        return newPicks;
      }

    }
    #endregion // Credit Iterator

    // Scale credit curves
    private void CheckScaling()
    {
      double[] factors = ScalingFactors;
      double[] scalingWeights = ScalingWeights;
      if (scalingWeights == null || scalingWeights.Length < 1 || factors == null)
      {
        for (int i = 0; i < Credits.Length; ++i)
          Credits[i].scalingFactors_ = factors;
        return;
      }

      for (int i = 0; i < Credits.Length; ++i)
        Credits[i].scalingFactors_ = GetScalingFactors(factors, scalingWeights[i]);
      return;
    }

    private double[] GetScalingFactors(double[] factors, double scalingWeight)
    {
      if (factors == null || factors.Length < 1)
        throw new ArgumentException("Must specify at lease one scaling factor");
      if (scalingWeight < 0 || scalingWeight > 1)
        throw new ArgumentOutOfRangeException("scalingWeight", "Invalid scaling weight. Must be between 0 and 1");

      // Apply scaling weight
      double[] scalingFactors = factors;
      if (scalingWeight != 1.0)
      {
        scalingFactors = new double[factors.Length];
        for (int i = 0; i < scalingFactors.Length; i++)
          scalingFactors[i] = factors[i] * scalingWeight;
      }
      return scalingFactors;
    }

		//
		// class for CDS quote
		//
		/// <exclude />
		[Serializable]
		public class Credit {
			/// <exclude />
			public string Name;
			/// <exclude />
			public string Category;
			/// <exclude />
			public double RecoveryRate;
			/// <exclude />
			public double RecoveryDispersion;
      /// <exclude />
      public Quote[] Quotes;
      /// <exclude />
      public Cancelability[] Cancelabilities;
      /// <exclude />
      public string DefaultDate, RecoveryDate;
      /// <exclude />
      public bool ForceFit;

			/// <exclude />
			[Serializable]
			public class Quote {
        /// <exclude />
        public string Tenor;
        /// <exclude />
        public string Maturity;
        /// <exclude />
				public double Spread;
			};

      /// <exclude />
      [Serializable]
      public class Cancelability
      {
        /// <exclude />
        public string Tenor;
        /// <exclude />
        public string Date;
        /// <exclude />
        public double Probability;
        /// <exclude />
        public double Correlation;
      };

      /// <exclude />
      internal double[] scalingFactors_;
    }; // class Credit


		// data
		/// <exclude />
		public int BasketSize = 0;
		/// <exclude />
		public string AsOf;
		/// <exclude />
		public Currency Currency;
		/// <exclude />
		public DayCount DayCount;
		/// <exclude />
		public Frequency Frequency;
		/// <exclude />
		public Calendar Calendar;
		/// <exclude />
		public BDConvention Roll;
		/// <exclude />
		public InterpMethod Interp;
		/// <exclude />
		public ExtrapMethod Extrap;
		/// <exclude />
		public NegSPTreatment NegSPTreat;
		/// <exclude />
		public double ScalingFactor;
		/// <exclude />
		public double [] ScalingFactors;
		/// <exclude />
		public string [] ScalingTenorNames;
		/// <exclude />
		public double[] ScalingWeights;
		/// <exclude />
		public string[] Tenors;
		/// <exclude />
		public string[] TenorNames;
		/// <exclude />
		public Credit[] Credits;
	}; // class CreditData

}
