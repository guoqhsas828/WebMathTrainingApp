using System.Runtime.CompilerServices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Native;
using BaseEntity.Toolkit.Numerics;

[assembly: TypeForwardedTo(typeof(INativeObject))]
[assembly: TypeForwardedTo(typeof(INativeCurve))]

//
// Base
//
[assembly: TypeForwardedTo(typeof(CashflowFlag))]
[assembly: TypeForwardedTo(typeof(CopulaType))]
[assembly: TypeForwardedTo(typeof(DividendSchedule))]
[assembly: TypeForwardedTo(typeof(FuturesCAMethod))]
[assembly: TypeForwardedTo(typeof(RecoveryType))]
[assembly: TypeForwardedTo(typeof(OptionStyle))]
[assembly: TypeForwardedTo(typeof(OptionType))]
[assembly: TypeForwardedTo(typeof(OptionBarrierFlag))]
[assembly: TypeForwardedTo(typeof(OptionBarrierType))]
[assembly: TypeForwardedTo(typeof(OptionDigitalType))]
[assembly: TypeForwardedTo(typeof(OptionPayoffType))]
[assembly: TypeForwardedTo(typeof(VolatilityBootstrapMethod))]
[assembly: TypeForwardedTo(typeof(VolatilityType))]

//
// Extraplators and interpolators
//
[assembly: TypeForwardedTo(typeof(InterpMethod))]
[assembly: TypeForwardedTo(typeof(ExtrapMethod))]
[assembly: TypeForwardedTo(typeof(ExtrapScheme))]
[assembly: TypeForwardedTo(typeof(InterpScheme))]

[assembly: TypeForwardedTo(typeof(Extrap))]
[assembly: TypeForwardedTo(typeof(Const))]
[assembly: TypeForwardedTo(typeof(Smooth))]

[assembly: TypeForwardedTo(typeof(Interp))]
[assembly: TypeForwardedTo(typeof(Cubic))]
[assembly: TypeForwardedTo(typeof(Flat))]
[assembly: TypeForwardedTo(typeof(Linear))]
[assembly: TypeForwardedTo(typeof(LogAdapter))]
[assembly: TypeForwardedTo(typeof(PCHIP))]
[assembly: TypeForwardedTo(typeof(Quadratic))]
[assembly: TypeForwardedTo(typeof(SquareLinearVolatilityInterp))]
[assembly: TypeForwardedTo(typeof(Tension))]
[assembly: TypeForwardedTo(typeof(Weighted))]
[assembly: TypeForwardedTo(typeof(WeightedAdapter))]

//
// Curves
//
[assembly: TypeForwardedTo(typeof(DelegateCurveInterp))]
[assembly: TypeForwardedTo(typeof(NegSPTreatment))]

// Calendar Calculator
[assembly: TypeForwardedTo(typeof(CalendarCalc))]
[assembly: TypeForwardedTo(typeof(Cashflow))]

// Models.BGM
[assembly: TypeForwardedTo(typeof(BgmCorrelation))]
[assembly: TypeForwardedTo(typeof(BgmCorrelationType))]
