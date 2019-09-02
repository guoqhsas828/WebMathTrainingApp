using System;

namespace MagnoliaIG.ToolKits.Base
{
    public enum QuotingConvention
    {
        None,
        FullPrice,
        FlatPrice,
        PartialCoupon,
        Yield,
        YieldSpread,
        ZSpread,
        CreditSpread,
        Fee,
        Volatility,
        RSpread,
        CreditConventionalUpfront,
        FxRate,
        ForwardFullPrice,
        ForwardFlatPrice,
        UseModelPrice,
        ASW_Par,
        ASW_Mkt,
        DiscountRate,
        DiscountMargin,
        CreditConventionalSpread,
    }

    public enum LoanNextCouponTreatment
    {
        None,
        CurrentFixing,
        InterestPeriods,
        StubRate
    }

    public enum IndexationMethod
    {
        None,
        /// <summary>
        /// The current international methodology, use interpolated RPI/CPI figures
        /// </summary>
        CanadianMethod,
        /// <summary>
        /// UK-Gilt inflation bond issued before year 2005, use static RPI index from 1st of the month in 8 monhts lag
        /// </summary>
        UKGilt_OldStyle,
        /// <summary>
        /// Australian Capital indexed Bond method, K factors generated from pairs of lagged rates
        /// </summary>
        AustralianCIB
    }

    public enum AssetSwapQuoteType
    {
        None,
        /// <summary>
        /// Par spread over libor
        /// </summary>
        PAR,
        /// <summary>
        /// Price
        /// </summary>
        MARKET
    }

    public enum AtmKind
    {
        Spot,
        Forward,
        DeltaNeutral
    }

    public enum FxVolatilityQuoteFlags
    {
        /// <summary>
        /// No special flag
        /// </summary>
        None = 0,
        ForwardAtm = 1,
        SpotAtm = 2,
        ForwardDelta = 4,
        PremiumIncludedDelta = 8,
        OneVolatilityButterfly = 16,
        Ccy2Strangle = 32,
        Ccy2RiskReversal = 64,
        
    }

    public enum ButterflyQuoteKind
    {
        CallPutAverage,
        Ccy1Strangle,
        Ccy2Strangle,
    }

    public enum DeltaPremiumKind
    {
        Excluded,
        Included,
    }

    public enum DeltaKind
    {
        Spot,
        Forward,
    }

    public enum RiskReversalKind
    {
        Ccy1CallPut,
        Ccy1PutCall,
        Ccy2CallPut,
        Ccy2PutCall,
    }

    [Flags]
    public enum DeltaStyle
    {
        None = 0,
        Forward = 1,
        PremiumIncluded = 2
    }
}
