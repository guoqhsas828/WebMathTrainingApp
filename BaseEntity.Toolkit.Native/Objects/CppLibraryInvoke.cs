using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using CurveMarshaler = MagnoliaIG.ToolKits.RateCurves.CurveMarshaler;

namespace MagnoliaIG.Cpp
{
    unsafe public partial class CppLibraryInvoke
    {
        #region Helpers

        protected class SWIGExceptionHelper
        {

            public delegate void ExceptionDelegate(string message);
            public delegate void ExceptionArgumentDelegate(string message, string paramName);

            static ExceptionDelegate applicationDelegate = new ExceptionDelegate(SetPendingApplicationException);
            static ExceptionDelegate arithmeticDelegate = new ExceptionDelegate(SetPendingArithmeticException);
            static ExceptionDelegate divideByZeroDelegate = new ExceptionDelegate(SetPendingDivideByZeroException);
            static ExceptionDelegate indexOutOfRangeDelegate = new ExceptionDelegate(SetPendingIndexOutOfRangeException);
            static ExceptionDelegate invalidOperationDelegate = new ExceptionDelegate(SetPendingInvalidOperationException);
            static ExceptionDelegate ioDelegate = new ExceptionDelegate(SetPendingIOException);
            static ExceptionDelegate nullReferenceDelegate = new ExceptionDelegate(SetPendingNullReferenceException);
            static ExceptionDelegate outOfMemoryDelegate = new ExceptionDelegate(SetPendingOutOfMemoryException);
            static ExceptionDelegate overflowDelegate = new ExceptionDelegate(SetPendingOverflowException);
            static ExceptionDelegate systemDelegate = new ExceptionDelegate(SetPendingSystemException);

            static ExceptionArgumentDelegate argumentDelegate = new ExceptionArgumentDelegate(SetPendingArgumentException);
            static ExceptionArgumentDelegate argumentNullDelegate = new ExceptionArgumentDelegate(SetPendingArgumentNullException);
            static ExceptionArgumentDelegate argumentOutOfRangeDelegate = new ExceptionArgumentDelegate(SetPendingArgumentOutOfRangeException);

            [DllImport("MagnoliaCppWrapper", EntryPoint = "SWIGRegisterExceptionCallbacks_MagnoliaCppWrapper")]
            public static extern void SWIGRegisterExceptionCallbacks_MagnoliaCppWrapper(
                                        ExceptionDelegate applicationDelegate,
                                        ExceptionDelegate arithmeticDelegate,
                                        ExceptionDelegate divideByZeroDelegate,
                                        ExceptionDelegate indexOutOfRangeDelegate,
                                        ExceptionDelegate invalidOperationDelegate,
                                        ExceptionDelegate ioDelegate,
                                        ExceptionDelegate nullReferenceDelegate,
                                        ExceptionDelegate outOfMemoryDelegate,
                                        ExceptionDelegate overflowDelegate,
                                        ExceptionDelegate systemExceptionDelegate);

            [DllImport("MagnoliaCppWrapper", EntryPoint = "SWIGRegisterExceptionArgumentCallbacks_MagnoliaCppWrapper")]
            public static extern void SWIGRegisterExceptionCallbacksArgument_MagnoliaCppWrapper(
                                        ExceptionArgumentDelegate argumentDelegate,
                                        ExceptionArgumentDelegate argumentNullDelegate,
                                        ExceptionArgumentDelegate argumentOutOfRangeDelegate);

            static void SetPendingApplicationException(string message)
            {
                SWIGPendingException.Set(new System.ApplicationException(message));
            }
            static void SetPendingArithmeticException(string message)
            {
                SWIGPendingException.Set(new System.ArithmeticException(message));
            }
            static void SetPendingDivideByZeroException(string message)
            {
                SWIGPendingException.Set(new System.DivideByZeroException(message));
            }
            static void SetPendingIndexOutOfRangeException(string message)
            {
                SWIGPendingException.Set(new System.IndexOutOfRangeException(message));
            }
            static void SetPendingInvalidOperationException(string message)
            {
                SWIGPendingException.Set(new System.InvalidOperationException(message));
            }
            static void SetPendingIOException(string message)
            {
                SWIGPendingException.Set(new System.IO.IOException(message));
            }
            static void SetPendingNullReferenceException(string message)
            {
                SWIGPendingException.Set(new System.NullReferenceException(message));
            }
            static void SetPendingOutOfMemoryException(string message)
            {
                SWIGPendingException.Set(new System.OutOfMemoryException(message));
            }
            static void SetPendingOverflowException(string message)
            {
                SWIGPendingException.Set(new System.OverflowException(message));
            }
            static void SetPendingSystemException(string message)
            {
                SWIGPendingException.Set(new System.SystemException(message));
            }

            static void SetPendingArgumentException(string message, string paramName)
            {
                SWIGPendingException.Set(new System.ArgumentException(message, paramName));
            }
            static void SetPendingArgumentNullException(string message, string paramName)
            {
                SWIGPendingException.Set(new System.ArgumentNullException(paramName, message));
            }
            static void SetPendingArgumentOutOfRangeException(string message, string paramName)
            {
                SWIGPendingException.Set(new System.ArgumentOutOfRangeException(paramName, message));
            }

            static SWIGExceptionHelper()
            {
                SWIGRegisterExceptionCallbacks_MagnoliaCppWrapper(
                                          applicationDelegate,
                                          arithmeticDelegate,
                                          divideByZeroDelegate,
                                          indexOutOfRangeDelegate,
                                          invalidOperationDelegate,
                                          ioDelegate,
                                          nullReferenceDelegate,
                                          outOfMemoryDelegate,
                                          overflowDelegate,
                                          systemDelegate);

                SWIGRegisterExceptionCallbacksArgument_MagnoliaCppWrapper(
                                          argumentDelegate,
                                          argumentNullDelegate,
                                          argumentOutOfRangeDelegate);
            }
        }

        protected static SWIGExceptionHelper swigExceptionHelper = new SWIGExceptionHelper();

        public class SWIGPendingException
        {
            [ThreadStatic]
            private static Exception pendingException = null;
            private static int numExceptionsPending = 0;

            public static bool Pending
            {
                get
                {
                    bool pending = false;
                    if (numExceptionsPending > 0)
                        if (pendingException != null)
                            pending = true;
                    return pending;
                }
            }

            public static void Set(Exception e)
            {
                if (pendingException != null)
                    throw new ApplicationException("FATAL: An earlier pending exception from unmanaged code was missed and thus not thrown (" + pendingException.ToString() + ")", e);
                pendingException = e;
                lock (typeof(CppLibraryInvoke))
                {
                    numExceptionsPending++;
                }
            }

            public static Exception Retrieve()
            {
                Exception e = null;
                if (numExceptionsPending > 0)
                {
                    if (pendingException != null)
                    {
                        e = pendingException;
                        pendingException = null;
                        lock (typeof(CppLibraryInvoke))
                        {
                            numExceptionsPending--;
                        }
                    }
                }
                return e;
            }
        }


        protected class SWIGStringHelper
        {

            public delegate string SWIGStringDelegate(string message);
            static SWIGStringDelegate stringDelegate = new SWIGStringDelegate(CreateString);

            [DllImport("MagnoliaCppWrapper", EntryPoint = "SWIGRegisterStringCallbacks_MagnoliaCppWrapper")]
            public static extern void SWIGRegisterStringCallback_MagnoliaCppWrapper(SWIGStringDelegate stringDelegate);

            static string CreateString(string cString)
            {
                return cString;
            }

            static SWIGStringHelper()
            {
                SWIGRegisterStringCallback_MagnoliaCppWrapper(stringDelegate);
            }
        }

        static protected SWIGStringHelper swigStringHelper = new SWIGStringHelper();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_DoubleArray@@YAXPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void delete_DoubleArray(HandleRef curveCPtr);

        #endregion

        static CppLibraryInvoke()
        {
            //CheckValidation();
        }

        #region Calendar

        //[SuppressUnmanagedCodeSecurity]
        //[DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalendarCalc_Init@@YAXPEAX@Z")]
        //public static extern void CalendarCalc_Init(MagnoliaIG.ToolKits.Base.LoadCalendarCallback calendarDir);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalendarCalc_GetValidCalendars@@YAPEAXXZ")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]
        public static extern int[] CSharp_CalendarCalc_GetValidCalendars();

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalendarCalc_IsValidSettlement@@YA_NHHHH@Z")]
        public static extern bool CSharp_CalendarCalc_IsValidSettlement(int calId, int day, int month, int year);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalendarCalc_getName@@YAPEADH@Z")]
        public static extern string CSharp_CalendarCalc_getName(int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalendarCalc_getCalendar@@YA?AW4Calendar@Calendars@qn@@PEAD@Z")]
        public static extern Calendar CSharp_CalendarCalc_getCalendar(string jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalendarCalc_IsValidCalendar@@YA_NH@Z")]
        public static extern bool CSharp_CalendarCalc_IsValidCalendar(int calId);

        #endregion Calendar
        #region Models

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?GaussLaguerre@Quadrature@qn@@SAXN_NAEAV?$Array@N@2@1@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Quadrature_GaussLegendre(bool var1, bool var2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] x, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] w);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?StudentT@Quadrature@qn@@QEAAXH_NAEAV?$Array@N@2@1@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Quadrature_StudentT(int df, bool boundedIntegrand, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] x, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] w);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?PriceFromYield@BondModelDefaulted@qn@@SANNNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern double DefaultedBondPriceFromYield(double yield, double T, double recovery);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?BlackP@DigitalOption@qn@@SANW4OptionStyle@OptionStyles@2@W4OptionType@OptionTypes@2@W4OptionDigitalType@OptionDigitalTypes@2@NNNNNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern double DigitalOption_BlackP(int style, int type, int digitalType, double T, double numeraire, double F, double K, double v, double C);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?BlackP@DigitalOption@qn@@SANW4OptionStyle@OptionStyles@2@W4OptionType@OptionTypes@2@W4OptionDigitalType@OptionDigitalTypes@2@NNNNNNPEAN333@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern double DigitalOption_BlackP(int style, int type, int digitalType, double T, double numeraire, double F, double K, double v, double C, ref double delta, ref double gamma, ref double theta, ref double vega);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?NormalBlackP@DigitalOption@qn@@SANW4OptionStyle@OptionStyles@2@W4OptionType@OptionTypes@2@W4OptionDigitalType@OptionDigitalTypes@2@NNNNNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern double DigitalOption_NormalBlackP(int style, int type, int digitalType, double T, double numeraire, double F, double K, double v, double C);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?NormalBlackP@DigitalOption@qn@@SANW4OptionStyle@OptionStyles@2@W4OptionType@OptionTypes@2@W4OptionDigitalType@OptionDigitalTypes@2@NNNNNNPEAN333@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern double DigitalOption_NormalBlackP(int style, int type, int digitalType, double T, double numeraire, double F, double K, double v, double C, ref double delta, ref double gamma, ref double theta, ref double vega);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_LoanModel_ComputeDistributions@@YAXPEAX00H00@Z")]
        public static extern void LoanModel_ComputeDistributions(HandleRef jarg1, HandleRef jarg2, HandleRef jarg3, int jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, HandleRef jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BlackScholes_P@@YANHHNNNNNPEAXN@Z")]
        public static extern double BlackScholes_P_Cpp2(int jarg1, int jarg2, double jarg3, double jarg4, double jarg5, double jarg6, double jarg7, HandleRef jarg8, double jarg9);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BlackScholes_P3@@YANHHNNNNNPEAXNPEAN111111111111111@Z")]
        public static extern double BlackScholes_P_Cpp3(int jarg1, int jarg2, double jarg3, double jarg4, double jarg5, double jarg6, double jarg7, HandleRef jarg8, double jarg9, ref double delta, ref double gamma, ref double theta, ref double vega, ref double rho, ref double lambda, ref double gearing, ref double strikeGearing, ref double vanna, ref double charm, ref double speed, ref double zomma, ref double color, ref double vomma, ref double dualDelta, ref double dualGamma);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DigitalOption_IVol@@YANHHNNNNNN@Z")]
        public static extern double DigitalOption_IVol(int jarg1, int jarg2, double jarg3, double jarg4, double jarg5, double jarg6, double jarg7, double jarg8);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?ImpliedVolatility@BlackNormal@qn@@SANW4OptionType@OptionTypes@2@NNNNN@Z")]
        public static extern double BlackNormal_ImpliedVol(int jarg1,double jarg2, double jarg3, double jarg4, double jarg5, double jarg6);
                       
        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?ImpliedVolatility@Black@qn@@SANW4OptionType@OptionTypes@2@NNNN@Z")]
        public static extern double Black_ImpliedVol(int jarg1, double jarg2, double jarg3, double jarg4, double jarg5);

        #endregion Models

        #region NativeCurve

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Curve_0@@YAPEAXXZ", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr NativeCurve_new_Curve_0();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Curve_1@@YAPEAXPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr NativeCurve_new_Curve_1(HandleRef curve);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Curve_2@@YAPEAXUDtPINVOKE@qn@@PEAXHH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr NativeCurve_new_Curve_2(Dt jarg1, HandleRef jarg2, int jarg3, int jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Curve_3@@YAPEAXUDtPINVOKE@qn@@HN@Z")]
        public static extern IntPtr NativeCurve_new_Curve_3(Dt asOf, int jarg2, double forwardRate);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_SetSpread@@YAXPEAXN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void NativeCurve_set_Spread(HandleRef curveRef, double spread);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_GetDayCount@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int NativeCurve_get_DayCount(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_SetDayCount@@YAXPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void NativeCurve_set_DayCount(HandleRef curveRef, int dc);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_GetSpread@@YANPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double NativeCurve_get_Spread(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Curve@@YAXPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void delete_Curve(HandleRef curveCPtr);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_GetFrequency@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int NativeCurve_get_Frequency(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_GetCurrency@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int NativeCurve_get_Currency(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_SetCurrency@@YAXPEAXH@Z")]
        public static extern void NativeCurve_set_Currency(HandleRef curveRef, int ccy);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_SetFrequency@@YAXPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void NativeCurve_set_Frequency(HandleRef curveRef, int freq);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_GetAsOf@@YA?AUDtPINVOKE@qn@@PEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt NativeCurve_get_AsOf(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_SetAsOf@@YAXPEAXUDtPINVOKE@qn@@@Z")]
        public static extern void NativeCurve_set_AsOf(HandleRef curveRef, Dt asOf);
        
        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_Set_InternalState@@YAXPEAXUDtPINVOKE@qn@@NHHHPEAN2210H@Z")]
        public static extern void NativeCurve_Set_InternalState(HandleRef curveRef, Dt asOf, double spread, int dc, int freq, int flags, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] x, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] y, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] dt, Dt jumpDate, HandleRef interp, int ccy);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_Get_InternalState_dt@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef=typeof(ArrayOfDtMarshaler))]
        public static extern Dt[] NativeCurve_Get_InternalState_dt(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_Get_InternalState_x@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] NativeCurve_Get_InternalState_x(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_Get_InternalState_y@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] NativeCurve_Get_InternalState_y(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_Add@@YAXPEAXUDtPINVOKE@qn@@N@Z")]
        public static extern void NativeCurve_Add(HandleRef curveRef, Dt date, double val);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_AddRate@@YAXPEAXUDtPINVOKE@qn@@N@Z")]
        public static extern void NativeCurve_AddRate(HandleRef curveRef, Dt date, double rate);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_Clear@@YAXPEAX@Z")]
        public static extern void NativeCurve_Clear(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_SetDt@@YAXPEAXHUDtPINVOKE@qn@@@Z")]
        public static extern void NativeCurve_SetDt(HandleRef curveRef, int idx, Dt date);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_SetRate@@YAXPEAXHN@Z")]
        public static extern void NativeCurve_SetRate(HandleRef curveRef, int idx, double rate);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_SetVal@@YAXPEAXHN@Z")]
        public static extern void NativeCurve_SetVal(HandleRef curveRef, int idx, double val);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_clone@@YAPEAXPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr NativeCurve_clone(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_Set_Curve@@YAXPEAX0@Z")]
        public static extern void NativeCurve_Set_Curve(HandleRef jarg1, HandleRef jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_Get_JumpDate@@YA?AUDtPINVOKE@qn@@PEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt NativeCurve_get_JumpDate(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_Set_JumpDate@@YAXPEAXUDtPINVOKE@qn@@@Z")]
        public static extern void NativeCurve_set_JumpDate(HandleRef curveRef, Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_Get_Interp@@YAPEAXPEAX@Z")]
        public static extern IntPtr NativeCurve_Get_Interp(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_Interpolate_0@@YANPEAXUDtPINVOKE@qn@@@Z")]
        public static extern double NativeCurve_Interpolate_0(HandleRef curveRef, Dt date);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_Interpolate_1@@YANPEAXN@Z")]
        public static extern double NativeCurve_Interpolate_1(HandleRef curveRef, Double x);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_Shrink@@YAXPEAXH@Z")]
        public static extern void NativeCurve_Shrink(HandleRef jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_SetInterp@@YAXPEAX0@Z")]
        public static extern void Curve_SetInterp(HandleRef jarg1, HandleRef jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_cloneOverlayedCurve@@YAPEAXPEAX0@Z")]
        public static extern IntPtr Curve_cloneOverlayedCurve(HandleRef jarg1, HandleRef jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_getOverlayedCurve@@YAPEAXPEAX@Z")]
        public static extern IntPtr Curve_getOverlayedCurve(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve_GetFlag@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int NativeCurve_get_Flag(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeCurve_SetFlag@@YAXPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void NativeCurve_set_Flag(HandleRef curveRef, int flags);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Curve2D@@YAXPEAX@Z")]
        public static extern void delete_Curve2D(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Curve2D_0@@YAPEAXXZ")]
        public static extern IntPtr CSharp_new_Curve2D_0();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Curve2D_1@@YAPEAXUDtPINVOKE@qn@@@Z")]
        public static extern IntPtr CSharp_new_Curve2D_1(Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_GetAsOf@@YA?AUDtPINVOKE@qn@@PEAX@Z")]
        public static extern Dt CSharp_Curve2D_get_AsOf(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_SetAsOf@@YAXPEAXUDtPINVOKE@qn@@@Z")]
        public static extern void CSharp_Curve2D_set_AsOf(HandleRef curveRef, Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Set_InternalState@@YAXPEAXUDtPINVOKE@qn@@PEAHPEAN3@Z")]
        public static extern void CSharp_Curve2D_Set_InternalState(HandleRef curveRef, Dt asOf, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] dates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] levels, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] values);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Get_DatesArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]
        public static extern int[] NativeCurve2D_Get_InternalState_Dates(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Get_LevelsArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] NativeCurve2D_Get_InternalState_Levels(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Get_ValuesArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] NativeCurve2D_Get_InternalState_Values(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_GetAccuracy@@YANPEAX@Z")]
        public static extern double Curve2D_Get_Accuracy(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_SetAccuracy@@YAXPEAXN@Z")]
        public static extern void Curve2D_Set_Accuracy(HandleRef jarg1, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_clone@@YAPEAXPEAX@Z")]
        public static extern IntPtr Curve2D_clone(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Initialize@@YAXPEAXHHH@Z")]
        public static extern void Curve2D_Initialize(HandleRef jarg1, int jarg2, int jarg3, int jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Interpolate@@YANPEAXHUDtPINVOKE@qn@@N@Z")]
        public static extern double Curve2D_Interpolate(HandleRef jarg1, int jarg2, Dt jarg3, double jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_ResizeByDates@@YAXPEAXHH@Z")]
        public static extern void Curve2D_ResizeByDates(HandleRef jarg1, int jarg2, int jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_AddValue_1@@YAXPEAXHN@Z")]
        public static extern void Curve2D_AddValue(HandleRef jarg1, int jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_AddValue_3@@YAXPEAXHHHN@Z")]
        public static extern void Curve2D_AddValue(HandleRef jarg1, int jarg2, int jarg3, int jarg4, double jarg5);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_SetLevel_1@@YAXPEAXHN@Z")]
        public static extern void Curve2D_SetLevel(HandleRef jarg1, int jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_SetDate_1@@YAXPEAXHUDtPINVOKE@qn@@@Z")]
        public static extern void Curve2D_SetDate(HandleRef jarg1, int jarg2, Dt jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_SetValue_3@@YAXPEAXHHHN@Z")]
        public static extern void Curve2D_SetValue(HandleRef jarg1, int jarg2, int jarg3, int jarg4, double jarg5);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_NumDates@@YAHPEAX@Z")]
        public static extern int Curve2D_Get_NumDates(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_NumLevels@@YAHPEAX@Z")]
        public static extern int Curve2D_Get_NumLevels(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_NumValues@@YAHPEAX@Z")]
        public static extern int Curve2D_Get_NumValues(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_GetDateInt@@YAHPEAXH@Z")]
        public static extern int Curve2D_Get_DateInt(HandleRef jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_GetLevel@@YANPEAXH@Z")]
        public static extern double Curve2D_Get_Level(HandleRef jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_GetValue@@YANPEAXHHH@Z")]
        public static extern double Curve2D_Get_Value(HandleRef jarg1, int grpIndex, int dateIdx, int levelIdx);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Interpolate_2@@YANPEAXUDtPINVOKE@qn@@N@Z")]
        public static extern double Curve2D_Interpolate_2(HandleRef jarg1, Dt jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Interpolate_3@@YANPEAXUDtPINVOKE@qn@@NN@Z")]
        public static extern double Curve2D_Interpolate_3(HandleRef jarg1, Dt jarg2, double loLevel, double hiLevel);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Curve2D_Interpolate_4@@YANPEAXHUDtPINVOKE@qn@@NN@Z")]
        public static extern double Curve2D_Interpolate_4(HandleRef jarg1, int grpIdx, Dt jarg2, double loLevel, double hiLevel);

        #endregion

        #region Interp/Extrap

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Extrap@@YAXPEAX@Z")]
        public static extern void delete_Extrap(HandleRef curveCPtr);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Const@@YAXPEAX@Z")]
        public static extern void delete_Const(HandleRef curveCPtr);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Smooth@@YAXPEAX@Z")]
        public static extern void delete_Smooth(HandleRef curveCPtr);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Extrap_clone@@YAPEAXPEAX@Z")]
        public static extern IntPtr Extrap_clone(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Extrap_get_typeName@@YAPEADPEAX@Z")]
        public static extern string Extrap_getTypeName(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Extrap_get_FromMethod@@YAPEAXH@Z")]
        public static extern IntPtr new_Extrap_SWIG_FromMethod(int extrapMethod);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Extrap_initialize@@YAXPEAX00@Z")]
        public static extern void Extrap_initialize(HandleRef jarg1, HandleRef jarg2, HandleRef jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Extrap_lowerExtrap@@YANPEAX0N@Z")]
        public static extern double Extrap_lowerExtrap(HandleRef jarg1, HandleRef jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Extrap_upperExtrap@@YANPEAX0N@Z")]
        public static extern double Extrap_upperExtrap(HandleRef jarg1, HandleRef jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Extrap_getExtrapMethod@@YAHPEAX@Z")]
        public static extern int Extrap_get_ExtrapMethod(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ConstUpcast@@YAPEAVExtrap@qn@@PEAVConst@2@@Z")]
        public static extern IntPtr ConstUpcast(IntPtr jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SmoothUpcast@@YAPEAVExtrap@qn@@PEAVSmooth@2@@Z")]
        public static extern IntPtr SmoothUpcast(IntPtr jarg1);

        //[SuppressUnmanagedCodeSecurity]
        //[DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DelegateInterp_new@@YAPEAXPEAX0@Z")]
        //public static extern IntPtr DelegateCurveInterp_New(ToolKits.Numerics.DelegateCurveInterp.InitializeFn jarg1, ToolKits.Numerics.DelegateCurveInterp.EvaluateFn jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_InterpFn@@YAXPEAX@Z")]
        public static extern void delete_InterpFn(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_InterpFn_findIndexBefore@@YAHPEAXN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int InterpFn_findIndexBefore(HandleRef jarg1, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_InterpFn_getSize@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int InterpFn_getSize(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_InterpFn_getX@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double InterpFn_getX(HandleRef jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_InterpFn_getY@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double InterpFn_getY(HandleRef jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Interp@@YAXPEAX@Z")]
        public static extern void delete_Interp(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_clone@@YAPEAXPEAX@Z")]
        public static extern IntPtr Interp_clone(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_get_typeName@@YAPEADPEAX@Z")]
        public static extern string Interp_getTypeName(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_getLowerExtrap@@YAPEAXPEAX@Z")]
        public static extern IntPtr Interp_getLowerExtrap(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_getUpperExtrap@@YAPEAXPEAX@Z")]
        public static extern IntPtr Interp_getUpperExtrap(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_Set_InternalState@@YAXPEAX00@Z")]
        public static extern void Interp_Set_InternalState(HandleRef jarg1, HandleRef jarg2, HandleRef jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Tension_SetInternalData@@YAXPEAXHPEAN1@Z")]
        public static extern void Tension_Set_InternalData(HandleRef jarg1,  int jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_PCHIP_set_InternalData@@YAXPEAX000@Z")]
        public static extern void PCHIP_Set_InternalData(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_PCHIP_get_InternalData@@YAXPEAX000@Z")]
        public static extern void PCHIP_Get_InternalData(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cubic_set_InternalData@@YAXPEAX0@Z")]
        public static extern void Cubic_Set_InternalData(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cubic_get_InternalData@@YAXPEAX0@Z")]
        public static extern void Cubic_Get_InternalData(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Weighted_0@@YAPEAXXZ")]
        public static extern IntPtr Interp_Weighted_0();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Tension@@YAXPEAX@Z")]
        public static extern void delete_Tension(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Tension_get_Flags@@YAHPEAX@Z")]
        public static extern int Tension_get_Internal_Flags(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Tension_Get_InternalData_S@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Tension_get_Internal_S(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Tension_Get_InternalData_T@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Tension_get_Internal_T(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_evaluate_1@@YANPEAX0N@Z")]
        public static extern double Interp_evaluate_1(HandleRef jarg1, HandleRef jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_evaluate_0@@YANPEAX0NH@Z")]
        public static extern double Interp_evaluate_0(HandleRef jarg1, HandleRef jarg2, double jarg3, int jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_reset@@YAXPEAX@Z")]
        public static extern void Interp_reset(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_initializeWith@@YAXPEAX0@Z")]
        public static extern void Interp_initializeWith(HandleRef jarg1, HandleRef jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_getInterpMethod@@YAHPEAX@Z")]
        public static extern int Interp_get_InterpMethod(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interp_get_FromMethod@@YAPEAXHH@Z")]
        public static extern IntPtr new_Interp_SWIG_FromMethod(int interpMethod, int extrapMethod);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Interpolator@@YAXPEAX@Z")]
        public static extern void delete_Interpolator(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Interpolator@@YAPEAXPEAX00@Z")]
        public static extern IntPtr new_Interpolator(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interpolator_evaluate@@YANPEAXN@Z")]
        public static extern double CSharp_Interpolator_evaluate(HandleRef jarg1, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interpolator_getSize@@YAHPEAX@Z")]
        public static extern int CSharp_Interpolator_getSize(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interpolator_getInterp@@YAPEAXPEAX@Z")]
        public static extern IntPtr CSharp_Interpolator_getInterp(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Interpolator_getPoint@@YAXPEAXHPEAN1@Z")]
        public static extern void CSharp_Interpolator_getPoint(HandleRef jarg1, int jarg2, ref double x, ref double y);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Smooth_get_InternalData@@YAXPEAXPEAN1111@Z")]
        public static extern void CSharp_Smooth_get_InternalData(HandleRef jarg1, ref double h, ref double ls, ref double rs, ref double min, ref double max);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Smooth_set_InternalData@@YAXPEAXNNNNN@Z")]
        public static extern void CSharp_Smooth_set_InternalData(HandleRef jarg1, double h, double ls, double rs, double min, double max);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Flat_get_InternalData@@YANPEAX@Z")]
        public static extern double CSharp_Flat_get_InternalData(HandleRef jarg1 );

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Flat_set_InternalData@@YAXPEAXN@Z")]
        public static extern void CSharp_Flat_set_InternalData(HandleRef jarg1, double rounding);

        #endregion

        #region Optimizers

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_DelegateOptimizerFn@@YAXPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void delete_DelegateOptimizerFn(HandleRef fnCPtr);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_DelegateOptimizerFn0@@YAPEAXHPEAX0@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr new_DelegateOptimizerFn_0(int jarg1, Double_Vector_Fn jarg2, Void_Vector_Vector_Fn jarg3);
        
        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DelegateOptimizerUpcast@@YAPEAVOptimizerFn@qn@@PEAVDelegateOptimizerFn@2@@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr DelegateOptimizerFnUpcast(IntPtr objectRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_DelegateOptimizerFn1@@YAPEAXHHPEAXI@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr new_DelegateOptimizerFn_1(int jarg0, int jarg1,  Void_Vector_Vector_Vector_Fn jarg2, bool jarg3);


        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Optimizer@@YAXPEAX@Z")]
        public static extern void delete_Optimizer(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_clone@@YAPEAXPEAX@Z")]
        public static extern IntPtr Optimizer_clone(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_minize@@YAPEAXPEAX0@Z")]
        public static extern double* Optimizer_minimize(HandleRef jarg1, HandleRef jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_getCurrentSolution@@YAPEAXPEAX@Z")]
        public static extern double* Optimizer_getCurrentSolution(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_restart_Optimizer@@YAXPEAX@Z")]
        public static extern void Optimizer_restart(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setMaxIterations@@YAXPEAXH@Z")]
        public static extern void Optimizer_setMaxIterations(HandleRef jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setMaxEvaluations@@YAXPEAXH@Z")]
        public static extern void Optimizer_setMaxEvaluations(HandleRef jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setToleranceF@@YAXPEAXN@Z")]
        public static extern void Optimizer_setToleranceF(HandleRef jarg1, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setToleranceX@@YAXPEAXN@Z")]
        public static extern void Optimizer_setToleranceX(HandleRef jarg1, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setToleranceGrad@@YAXPEAXN@Z")]
        public static extern void Optimizer_setToleranceGrad(HandleRef jarg1, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setLowerBounds_0@@YAXPEAX0@Z")]
        public static extern void Optimizer_setLowerBounds_0(HandleRef jarg1, double[] jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setUpperBounds_0@@YAXPEAX0@Z")]
        public static extern void Optimizer_setUpperBounds_0(HandleRef jarg1, double[] jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_setInitialPoint_0@@YAXPEAX0@Z")]
        public static extern void Optimizer_setInitialPoint_0(HandleRef jarg1, double[] jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Optimizer_getDimension@@YAHPEAX@Z")]
        public static extern int Optimizer_getDimension(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NLS_Swig_0@@YAPEAXH@Z")]
        public static extern IntPtr new_NLS_Swig_0(int jarg0);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NLS_clone@@YAPEAXPEAX@Z")]
        public static extern IntPtr NLS_clone(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_NLS@@YAXPEAX@Z")]
        public static extern void delete_NLS(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NLS_Upcast@@YAPEAVNLS@Optimizers@math@qn@@PEAVOptimizer@4@@Z")]
        public static extern IntPtr NLS_UpCast(IntPtr jarg0);

        #endregion

        #region BGM

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_BgmCalibrationInputs@@YAXPEAX@Z")]
        public static extern void delete_BgmCalibrationInputs(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrationInputs_Create@@YAPEAXUDtPINVOKE@qn@@PEAX11HHH1@Z")]
        public static extern IntPtr BgmCalibrationInputs_Create(Dt jarg1, HandleRef jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg4, int jarg5, int jarg6, int jarg7, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg8);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrationInputs_getRateCount@@YAHPEAX@Z")]
        public static extern int BgmCalibrationInputs_GetRateCount(HandleRef jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrationInputs_getEffectiveTenorCount@@YAHPEAX@Z")]
        public static extern int BgmCalibrationInputs_GetEffectiveTenorCount(HandleRef jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrationInputs_getDiscountFactors@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] BgmCalibrationInputs_GetDiscountFactors(HandleRef jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrationInputs_getTenors@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] BgmCalibrationInputs_GetTenors(HandleRef jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrationInputs_getFractions@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] BgmCalibrationInputs_GetFractions(HandleRef jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrationInputs_getVolatilities@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]
        public static extern double[,] BgmCalibrationInputs_GetVolatilities(HandleRef jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_BgmCorrelation@@YAXPEAX@Z")]
        public static extern void delete_BgmCorrelation(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCorrelation_CreateCorrelation@@YAPEAXHHPEAX@Z")]
        public static extern IntPtr BgmCorrelation_createCorrelation(int jarg1, int jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg3);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCorrelation_resize@@YAXPEAXH@Z")]
        public static extern void BgmCorrelation_resize(HandleRef jarg1, int size);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCorrelation_factorAt@@YGNPAXHH@Z")]
        public static extern double BgmCorrelation_factorAt(HandleRef jarg1, int m, int n);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCorrelation_at@@YANPEAXHH@Z")]
        public static extern double BgmCorrelation_at(HandleRef jarg1, int m, int n);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCorrelation_dim@@YAHPEAX@Z")]
        public static extern int BgmCorrelation_dim(HandleRef jarg1);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCorrelation_rank@@YAHPEAX@Z")]
        public static extern int BgmCorrelation_rank(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCorrelation_reduceRank@@YAPEAXPEAXH@Z")]
        public static extern IntPtr BgmCorrelation_reduceRank(HandleRef jarg1, int jarg2);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrations_CascadeCalibrateGeneric@@YAXIPEAX0@Z")]
        public static extern void BgmCalibrations_CascadeCalibrateGeneric(bool isNormal, HandleRef jarg2, HandleRef jarg3);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrations_CascadeCalibrate@@YAXIPEAX0000@Z")]
        public static extern void BgmCalibrations_CascadeCalibrate(bool isNormal, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] fractions, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] dfs, HandleRef jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] expiries, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] vols);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrations_PieceWiseConstantFitGeneric@@YAXIHNPEAX000@Z")]
        public static extern void BgmCalibrations_PieceWiseConstantFitGeneric(bool isNormal, int modelChoice, double tolerance, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] shapeControls, HandleRef correlations, HandleRef data, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] results);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrations_PieceWiseConstantFit@@YAXHNPEAX00000@Z")]
        public static extern void BgmCalibrations_PieceWiseConstantFit(int modelChoice, double tolerance, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] shapeControls, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] discounts, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] expiries, HandleRef correlations, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] swpnVolatilities, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] results);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrations_SwaptionVolatilities@@YAXIPEAX00000@Z")]
        public static extern void BgmCalibrations_Swaption_Volatilities(bool isNormal, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, HandleRef jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg7);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmCalibrations_SwaptionVolatility@@YANIUDtPINVOKE@qn@@0PEAX111@Z")]
        public static extern double BgmCalibrations_SwaptionVolatility(bool isNormal, Dt jarg2, Dt jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg6, HandleRef jarg7);

        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_RateSystem@@YAXPEAX@Z")]
        public static extern void delete_BgmRateSystem(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_RateSystem@@YAPEAXXZ")]
        public static extern IntPtr new_BgmRateSystem();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_RateSystem_clone@@YAPEAXPEAX@Z")]
        public static extern IntPtr BgmRateSystem_clone(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_RateSystem_initialize@@YAXPEAXHH@Z")]
        public static extern void BgmRateSystem_initialize(HandleRef jarg1, int jarg2, int jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_RateSystem_setup@@YAXPEAXHH@Z")]
        public static extern void BgmRateSystem_setup(HandleRef jarg1, int jarg2, int jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmBinomialTree_calculateRateSystem@@YAXNNPEAX00000@Z")]
        public static extern void BgmBinomialTree_calculateRateSystem(double jarg1, double jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg6,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg7, HandleRef jarg8);

        //[SuppressUnmanagedCodeSecurity]
        //[DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_BgmBinomialTree_calibrateCoTerminalSwaptions@@YAXUDtPINVOKE@qn@@0PEAXNH1@Z")]
        //public static extern void BgmBinomialTree_calibrateCoTerminalSwaptions(Dt jarg1, Dt jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(MagnoliaIG.Cpp.ArrayOfStructMarshaler<MagnoliaIG.ToolKits.Models.SwaptionInfo>))]MagnoliaIG.ToolKits.Models.SwaptionInfo[] jarg3, double jarg4, int jarg5, HandleRef jarg6);

        #endregion

        #region Simulation

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_MultiStreamRng_get_0@@YAPEAXHHPEAXH@Z")]
        public static extern IntPtr MultiStreamRng_get_0(int type, int factorCount, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] partition, int quadRule);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_MultiStreamRng_DrawUniform@@YAXPEAXH0@Z")]
        public static extern void MultiStreamRng_DrawUniform(HandleRef jarg1, int idx, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] workspace);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_MultiStreamRng_clone@@YAPEAXPEAX@Z")]
        public static extern IntPtr MultiStreamRng_clone(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_CreateSimulator@@YAPEAXHPEAX0H@Z")]
        public static extern IntPtr Simulator_CreateSimulator( int jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, int jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_CreateProjectiveSimulator@@YAPEAXPEAX0H@Z")]
        public static extern IntPtr Simulator_CreateProjectiveSimulator([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, int jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?Simulator_PerturbFactors@@YAPEAXPEAX0000@Z")]
        public static extern IntPtr Simulator_PerturbFactors(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg5);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?Simulator_PerturbVolatilities@@YAPEAXPEAX0000@Z")]
        public static extern IntPtr Simulator_PerturbVolatilitis(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg5);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?Simulator_PerturbTermStructures@@YAPEAXPEAX000@Z")]
        public static extern IntPtr Simulator_PerturbTermStructures(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_SimulatedPath@@YAXPEAX@Z")]
        public static extern void delete_SimulatedPath(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveCreditCurve@@YAXPEAXHH0@Z")]
        public static extern void SimulatedPath_EvolveCreditCurve(HandleRef jarg1, int jarg2, int jarg3, HandleRef jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveCreditCurve_2@@YAXPEAXHNH0@Z")]
        public static extern void SimulatedPath_EvolveCreditCurve_2(HandleRef jarg1, int curveId, double dt, int dateIdx, HandleRef jarg4);
        
        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_getIndex@@YAHPEAX@Z")]
        public static extern int SimulatedPath_getIndex(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_getDim@@YAHPEAX@Z")]
        public static extern int SimulatedPath_getDim(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_getWeight@@YANPEAX@Z")]
        public static extern double SimulatedPath_getWeight(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveSpotPrice@@YANPEAXHH@Z")]
        public static extern double SimulatedPath_EvolveSpotPrice(HandleRef jarg1, int jarg2, int jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveSpotPrice_2@@YANPEAXHNH@Z")]
        public static extern double SimulatedPath_EvolveSpotPrice_2(HandleRef jarg1, int curveId, double dt, int dateIdx);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveDiscountCurve@@YAXPEAXHH0PEAN1@Z")]
        public static extern void SimulatedPath_EvolveDiscountCurve(HandleRef jarg1, int jarg2, int jarg3, HandleRef jarg4, ref double jarg5, ref double jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveDiscountCurve_2@@YAXPEAXHNH0PEAN1@Z")]
        public static extern void SimulatedPath_EvolveDiscountCurve_2(HandleRef jarg1, int curveId, double dt, int dateIdx, HandleRef jarg4, ref double jarg5, ref double jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveForwardCurve@@YAXPEAXHH0@Z")]
        public static extern void SimulatedPath_EvolveForwardCurve(HandleRef jarg1, int jarg2, int jarg3, HandleRef jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveForwardCurve_2@@YAXPEAXHNH0@Z")]
        public static extern void SimulatedPath_EvolveForwardCurve_2(HandleRef jarg1, int curveId, double dt, int dateIdx, HandleRef jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_EvolveRnDensity@@YAXPEAXHH@Z")]
        public static extern double SimulatedPath_EvolveRnDensity(HandleRef jarg1, int jarg2, int jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Simulator@@YAXPEAX@Z")]
        public static extern void delete_Simulator(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_GetPath_0@@YAPEAXPEAXH0@Z")]
        public static extern IntPtr Simulator_GetPath(HandleRef jarg1, int jarg2, HandleRef jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_GetPath_1@@YAPEAXPEAX0@Z")]
        public static extern IntPtr Simulator_GetPath(HandleRef jarg1, HandleRef path);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_DefaultKernel@@YAXPEAXH0@Z")]
        public static extern void Simulator_DefaultKernel(HandleRef jarg1, int jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3 );

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_SurvivalKernel@@YAXPEAXH0@Z")]
        public static extern void Simulator_SurvivalKernel(HandleRef jarg1, int index, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_getDim@@YAHPEAX@Z")]
        public static extern int Simulator_GetDim(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_AddDomesticDiscountProcess@@YAHPEAX000II@Z")]
        public static extern int Simulator_AddDomesticDiscountProcess(HandleRef jarg1, HandleRef jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, bool jarg5, bool jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_AddDiscountProcess@@YAHPEAX000NI00II@Z")]
        public static extern int Simulator_AddDiscountProcess(HandleRef jarg1, HandleRef jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, double jarg5, bool jarg6, HandleRef jarg7, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg8, bool jarg9, bool jarg10);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_AddSurvivalProcess@@YAHPEAX000I@Z")]
        public static extern int Simulator_AddSurvivalProcess(HandleRef jarg1, HandleRef jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, bool jarg5);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_AddForwardProcess@@YAHPEAX000II@Z")]
        public static extern int Simulator_AddForwardProcess(HandleRef jarg1, HandleRef jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, bool jarg5, bool jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_AddSpotProcess@@YAHPEAXNH0000I@Z")]
        public static extern int Simulator_AddSpotProcess(HandleRef jarg1, double jarg2, int jarg3, HandleRef vol, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] dividends, bool jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Simulator_AddRnDensityProcess@@YAXPEAX00N@Z")]
        public static extern int Simulator_AddRnDensityProcess(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg3, double jarg4);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_ErrorForGivenFactorDimension@@YANPEAXH@Z")]
        public static extern double CalibrationUtils_ErrorForGivenFactorDimension([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg1, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_ChooseFactorDimension@@YAHPEAXN@Z")]
        public static extern int CalibrationUtils_ChooseFactorDimension([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg1, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_InterpolateFactorLoadings@@YAXPEAX000@Z")]
        public static extern void CalibrationUtils_InterpolateFactorLoadings([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4);


        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateSpotVol@@YAXHPEAX000N000I0@Z")]
        public static extern void CalibrationUtils_CalibrateSpotVol(int jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, HandleRef jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg5, double jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg7, HandleRef jarg8, HandleRef jarg9, bool jarg10, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg11);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateFxVol@@YAXHPEAX000000N000II0@Z")]
        public static extern void CalibrationUtils_CalibrateFxVol(int jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, HandleRef jarg3, HandleRef jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg7, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg8, double jarg9, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg10, HandleRef jarg11, HandleRef jarg12, bool jarg13, bool jarg14, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg15);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateSemiAnalyticFxVol@@YAXPEAX000000N000II00@Z")]
        public static extern void CalibrationUtils_CalibrateSemiAnalyticFxVol([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg1, HandleRef jarg2, HandleRef jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg7, double jarg8, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg9, HandleRef jarg10, HandleRef jarg11, bool jarg12, bool jarg13, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg14, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg15);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SimulatedPath_getWeinerIncrements@@YAXPEAXH0@Z")]
        public static extern double SimulatedPath_GetWeinerIncrements(HandleRef jarg1, int dateIndex, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateSemiAnalyticSpotVol@@YAXPEAX000N000I00@Z")]
        public static extern void CalibrationUtils_CalibrateSemiAnalyticSpotVol([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg1, HandleRef jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg4, double jarg5, HandleRef jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg7, HandleRef jarg8, bool jarg9, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg10, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg11);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_PerturbFactorLoadings@@YAXPEAXH00I@Z")]
        public static extern void CalibrationUtils_PerturbFactorLoadings([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg1, int jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4, bool jarg5);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateCreditVol@@YAXPEAXN000000H0@Z")]
        public static extern void CalibrationUtils_CalibrateCreditVol(HandleRef jarg1, double jarg2, HandleRef jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg7, HandleRef jarg8, int jarg9, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg10);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateLiborFactors@@YAXPEAX00000000I@Z")]
        public static extern void CalibrationUtils_CalibrateLiborFactors(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg7, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg8, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg9, bool jarg10);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateFromSwaptionVolatility0@@YAXPEAX000000II@Z")]
        public static extern void CalibrationUtils_CalibrateFromSwaptionVolatility(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg7, bool jarg8, bool jarg9);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_CalibrateFromSwaptionVolatility1@@YAXPEAX000000000II@Z")]
        public static extern void CalibrationUtils_CalibrateFromSwaptionVolatility(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg7, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg8, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg9, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg10, bool jarg11, bool jarg12);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_MapCapletVolatilities@@YAXPEAX00000I@Z")]
        public static extern void CalibrationUtils_MapCapletVolatilities(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] jarg6, bool jarg8);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_FactorizeCorrelationMatrix@@YAXPEAX00H0PEAN@Z")]
        public static extern void CalibrationUtils_FactorizeCorrelationMatrix([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg3, int jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg5, ref double jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CalibrationUtils_GetEigenValues@@YAXPEAX0@Z")]
        public static extern void CalibrationUtils_GetEigenValues([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_RateOptionParamCollection_count@@YAHPEAX@Z")]
        public static extern int RateOptionParamCollection_Count(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_RateOptionParamCollection_add@@YAXPEAXHUDtPINVOKE@qn@@NNN000@Z")]
        public static extern void RateOptionParamCollection_Add(HandleRef jarg1, int jarg2, Dt jarg3, double jarg4, double jarg5, double jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg7, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg8, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg9);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_RateOptionParamCollection@@YAXPEAX@Z")]
        public static extern void RateOptionParamCollection_delete(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_RateOptionParamCollection_0@@YAPEAXH@Z")]
        public static extern IntPtr RateOptionParamCollection_new(int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_RateVolatilityCurveBuilder_BootstrapEDFCapletCurve@@YAXPEAX000000NNNH00@Z")]
        public static extern void RateVolatilityCurveBuilder_BootstrapEDFCapletCurve([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg2, HandleRef jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg6, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg7, double jarg8, double jarg9, double jarg10, int jarg11, HandleRef jarg12, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg13);

        #endregion Simulation

        #region Cashflow Schedule

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Schedule_0@@YAPEAXUDtPINVOKE@qn@@0000HHHHH@Z")]
        public static extern IntPtr new_Schedule_SWIG_0(Dt jarg1, Dt effective, Dt firstCpnDate, Dt lastCpnDate, Dt maturity, int jarg2, int jarg3, int jarg4, int jarg5, int jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Schedule_1@@YAPEAXUDtPINVOKE@qn@@HHHHH0H@Z")]
        public static extern IntPtr new_Schedule_SWIG_1(Dt jarg1, int jarg2, int jarg3, int jarg4, int jarg5, int jarg6, Dt anchor, int jarg8);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_GetNextCouponIndex@@YAHPEAXUDtPINVOKE@qn@@@Z")]
        public static extern int Schedule_GetNextCouponIndex(HandleRef jarg1, Dt jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_GetNextPaymentIndex@@YAHPEAXUDtPINVOKE@qn@@@Z")]
        public static extern int Schedule_GetNextPaymentIndex(HandleRef jarg1, Dt jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_GetPrevCouponIndex@@YAHPEAXUDtPINVOKE@qn@@@Z")]
        public static extern int Schedule_GetPrevCouponIndex(HandleRef jarg1, Dt jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_Size@@YAHPEAX@Z")]
        public static extern int Schedule_Size(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?Schedule_getPeriodArray@@YAPEAXPEAX@Z")]
        public static extern IntPtr Schedule_GetPeriodArray(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?delete_Schedule@@YAXPEAX@Z")]
        public static extern void delete_Schedule(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_AccrualDays_0@@YAHPEAXUDtPINVOKE@qn@@H@Z")]
        public static extern int Schedule_AccrualDays_0(HandleRef jarg1, Dt settle, int dc);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_AccrualDays_1@@YAHPEAXUDtPINVOKE@qn@@1H@Z")]
        public static extern int Schedule_AccrualDays_1(HandleRef jarg1, Dt settle, Dt nextCpnDate, int dc);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_Fraction_0@@YANPEAXHH@Z")]
        public static extern double Schedule_Fraction_0(HandleRef jarg1, int idx, int dc);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_Fraction_1@@YANPEAXUDtPINVOKE@qn@@1H@Z")]
        public static extern double Schedule_Fraction_1(HandleRef jarg1, Dt start, Dt end, int dc);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Schedule_Fraction_2@@YANPEAXHHIIII@Z")]
        public static extern double Schedule_Fraction_2(HandleRef jarg1, int idx, int dc, bool jarg3, bool jarg4, bool jarg5, bool jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CounterpartyRisk_TransformSurvivalCurves@@YAXUDtPINVOKE@qn@@0PEAX1N11HH@Z")]
        public static extern void CounterpartyRisk_EvolveCreditCurve(Dt start, Dt end, HandleRef jarg1, HandleRef jarg2, double jarg3, HandleRef jarg4, HandleRef jarg5, int jarg6, int jarg7);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CounterpartyRisk_SurvivalProbability@@YANUDtPINVOKE@qn@@0PEAX1NHH@Z")]
        public static extern double CounterpartyRisk_SurvivalProbability(Dt start, Dt end, HandleRef jarg1, HandleRef jarg2, double jarg3, int jarg6, int jarg7);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetAsOf@@YA?AUDtPINVOKE@qn@@PEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt Cashflow_get_AsOf(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_AsOf@@YAXPEAXUDtPINVOKE@qn@@@Z")]
        public static extern void Cashflow_set_AsOf(HandleRef curveRef, Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetEffective@@YA?AUDtPINVOKE@qn@@PEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt Cashflow_get_Effective(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_Effective@@YAXPEAXUDtPINVOKE@qn@@@Z")]
        public static extern void Cashflow_set_Effective(HandleRef curveRef, Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetDayCount@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern DayCount Cashflow_get_DayCount(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_DayCount@@YAXPEAXH@Z")]
        public static extern void Cashflow_set_DayCount(HandleRef curveRef, int dc);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetCcy@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Currency Cashflow_get_Ccy(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_Ccy@@YAXPEAXH@Z")]
        public static extern void Cashflow_set_Ccy(HandleRef curveRef, int ccy);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetDefaultCcy@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Currency Cashflow_get_DefaultCcy(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_DefaultCcy@@YAXPEAXH@Z")]
        public static extern void Cashflow_set_DefaultCcy(HandleRef curveRef, int ccy);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetSize@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int Cashflow_get_Size(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_IsAccruedPaidOnDefault@@YA_NPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern bool Cashflow_get_IsAccruedPaidOnDefault(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_IsAccruedPaidOnDefault@@YAXPEAXI@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_IsAccruedPaidOnDefault(HandleRef curveRef, bool jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_IsAccruedIncludingDefaultDate@@YA_NPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern bool Cashflow_get_IsAccruedIncludingDefaultDate(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_IsAccruedIncludingDefaultDate@@YAXPEAXI@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_IsAccruedIncludingDefaultDate(HandleRef curveRef, bool jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_AccruedFractionOnDefault@@YANPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_AccruedFractionOnDefault(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_AccruedFractionOnDefault@@YAXPEAXN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_AccruedFractionOnDefault(HandleRef curveRef, double jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_DefaultTiming@@YANPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_DefaultTiming(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_DefaultTiming@@YAXPEAXN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_DefaultTiming(HandleRef curveRef, double jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetFreq@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Frequency Cashflow_get_Frequency(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_Freq@@YAXPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_Frequency(HandleRef curveRef, int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetRecoveryType@@YAHPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern RecoveryType Cashflow_get_RecoveryType(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_RecoveryType@@YAXPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_RecoveryType(HandleRef curveRef, int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_RecoveryDispersion@@YANPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_RecoveryDispersion(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_RecoveryDispersion@@YAXPEAXN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_RecoveryDispersion(HandleRef curveRef, double jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Cashflow_0@@YAPEAXXZ")]
        public static extern IntPtr new_Cashflow_0();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Cashflow_1@@YAPEAXUDtPINVOKE@qn@@@Z")]
        public static extern IntPtr new_Cashflow_1(Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_Cashflow_2@@YAPEAXUDtPINVOKE@qn@@0@Z")]
        public static extern IntPtr new_Cashflow_2(Dt asOf, Dt effective);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_clone_Cashflow@@YAPEAXPEAX@Z")]
        public static extern IntPtr clone_Cashflow(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_Cashflow@@YAXPEAX@Z")]
        public static extern void delete_Cashflow(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Clear@@YAXPEAX@Z")]
        public static extern void Cashflow_Clear(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_OriginalPrincipal@@YANPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_OriginalPrincipal(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_OriginalPrincipal@@YAXPEAXN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_OriginalPrincipal(HandleRef curveRef, double jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_DefaultAmount@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_DefaultAmount(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_Amount@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_Amount(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_Coupon@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_Coupon(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_Accrued@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_Accrued(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetDt@@YA?AUDtPINVOKE@qn@@PEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt Cashflow_get_Dt(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetStartDt@@YA?AUDtPINVOKE@qn@@PEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt Cashflow_get_StartDt(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetResetDt@@YA?AUDtPINVOKE@qn@@PEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt Cashflow_get_ResetDt(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_GetEndDt@@YA?AUDtPINVOKE@qn@@PEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern Dt Cashflow_get_EndDt(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_PrincipalAt@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_PrincipalAt(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_PeriodFraction@@YANPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_PeriodFraction(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_ClearAmount@@YAXPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_ClearAmount(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_ProjectedAt@@YAIPEAXH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern bool Cashflow_get_ProjectedAt(HandleRef curveRef, int jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_0@@YAXPEAXHNNN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_Set_0(HandleRef curveRef, int jarg2, double amt, double accrued, double defaultAmt);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_1@@YAXPEAXHNNNN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_Set_1(HandleRef curveRef, int jarg2, double amt, double accrued, double coupon, double defaultAmt);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_InternalData@@YAXPEAX000000000000@Z")]
        public static extern void Cashflow_Set_InternalState(HandleRef curveRef, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] startDts, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] endDates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] dates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] fracs, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] amts, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] accrs, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] cpns, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] dftAmts, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] resets, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] principals, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] sprs, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] flags);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_2@@YAXPEAXHNNNNN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_Set_2(HandleRef curveRef, int jarg2, double principal, double amt, double accrued, double coupon, double defaultAmt);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Add_0@@YAXPEAXUDtPINVOKE@qn@@11NNNNN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_Add_0(HandleRef curveRef, Dt jarg2, Dt jarg3, Dt jarg4, double frac, double amt, double accrued, double coupon, double defaultAmt);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Add_1@@YAXPEAXUDtPINVOKE@qn@@11NNNNNN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_Add_1(HandleRef curveRef, Dt jarg2, Dt jarg3, Dt jarg4, double frac, double principal, double amt, double accrued, double coupon, double defaultAmt);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Add_2@@YAXPEAXUDtPINVOKE@qn@@111NNNNNNNI@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_Add_2(HandleRef curveRef, Dt resetDt, Dt jarg2, Dt jarg3, Dt jarg4, double frac, double principal, double amt, double accrued, double coupon, double defaultAmt, double spread, bool isProjected);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_StartDates@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]
        public static extern Dt[] Cashflow_Get_InternalState_StartDates(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_EndDates@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]
        public static extern Dt[] Cashflow_Get_InternalState_EndDates(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_Dates@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]
        public static extern Dt[] Cashflow_Get_InternalState_Dates(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_ResetDates@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]
        public static extern Dt[] Cashflow_Get_InternalState_ResetDates(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_Accrued@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Cashflow_Get_InternalState_Accrued(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_Amounts@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Cashflow_Get_InternalState_Amounts(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_Coupons@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Cashflow_Get_InternalState_Coupons(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_DftAmounts@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Cashflow_Get_InternalState_DftAmounts(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_PeriodFractions@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Cashflow_Get_InternalState_PeriodFractions(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_Principals@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Cashflow_Get_InternalState_Principals(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_Spreads@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] Cashflow_Get_InternalState_Spreads(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_InternalData_ProjectedFlags@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]
        public static extern int[] Cashflow_Get_InternalState_ProjectedFlags(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Get_MaturityPaymentIfDefaulted@@YANPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double Cashflow_get_MaturityPaymentIfDefaulted(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Cashflow_Set_MaturityPaymentIfDefaulted@@YAXPEAXN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern void Cashflow_set_MaturityPaymentIfDefaulted(HandleRef curveRef, double jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CashflowModel_Price@@YANPEAXUDtPINVOKE@qn@@1000NHHHH@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern double CashflowModel_Price(HandleRef cfRef, Dt asOf, Dt settle, HandleRef discountCurve, HandleRef survival, HandleRef counterPtySurvival, double correlation, int flags, int step, int stepUnit, int maturityIdx);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CashflowFactory_FillFloat@@YAXPEAX0UDtPINVOKE@qn@@1111HN100HHHH000N00NH1HH@Z")]
        public static extern double CashflowFactory_FillFloat(HandleRef jarg1, HandleRef jarg2, Dt jarg3, Dt jarg4, Dt jarg5, Dt jarg6, Dt jarg7, int jarg8, double jarg9, Dt jarg10, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg11, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg12, int jarg13, int jarg14, int jarg15, int cal, HandleRef jarg17, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg18, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg19, double jarg20, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg21, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg22, double jarg23, int jarg24, Dt jarg25, int jarg26, int jarg27);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CashflowFactory_FillFixed@@YAXPEAX0UDtPINVOKE@qn@@1111HN100HHHHN00NH1HH@Z")]
        public static extern double CashflowFactory_FillFixed(HandleRef jarg1, HandleRef jarg2, Dt jarg3, Dt jarg4, Dt jarg5, Dt jarg6, Dt jarg7, int jarg8, double jarg9, Dt jarg10, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg11, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg12, int jarg13, int jarg14, int jarg15, int cal, double jarg20, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg21, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg22, double jarg23, int jarg24, Dt jarg25, int jarg26, int jarg27);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_CashflowFactory_Init@@YAXIIIII@Z")]
        public static extern double CashflowFactory_Init(bool accrueOnCycle, bool rollLastPaymentDate, bool cdsIncludeMaturityAccrual, bool cdsIncludeMaturityProtection, bool dontRollInSchedule);

        #endregion Cashflow Schedule

        #region SemiAnalyticModel

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_0@@YAXHHHHPEAX00HN000@Z")]      
        public static extern void SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_0(int nth, int copulaType,int dfCommon,int dfIdiosyncratic,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] copulaParams,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates,int quadraturePoints,double accuracy,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] lossGivenDefaults, HandleRef nthLossCurve);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_1@@YAXHPEAX0H0000I00@Z")]
        public static extern void SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_1(int nth, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, int quadraturePoints, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] lossGivenDefaults,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] cptyCorrData, HandleRef cptyCurve,bool survivalOnly, HandleRef nthLossCurve, HandleRef nthSurvival);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_2@@YAXHUDtPINVOKE@qn@@PEAX111111111@Z")]
        public static extern void SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_2(int nth, Dt asOf, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] dates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] lossGivenDefaults, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] quadPoints, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] quadWts, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] nthLossCurve, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] surProb,Double_Vector_Fn jarg12);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_3@@YAXHUDtPINVOKE@qn@@PEAX11111111@Z")]
        public static extern void SemiAnalyticNtdModel_LossGivenNthDefault__SWIG_3(int nth, Dt asOf, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] dates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] lossGivenDefaults, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] quadPoints, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] quadWts, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] nthLossCurve, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Array2DOfDoubleMarshaler))]double[,] surProb);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?cumulative@Beta@Distributions@qn@@SANNNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Distributions_Beta_cumulative(double x, double jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?density@Beta@Distributions@qn@@SANNNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Distributions_Beta_density(double x, double jarg2, double jarg3);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?cumulative@StudentT@Distributions@qn@@SANNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Distributions_StudentT_cumulative(double x, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?density@StudentT@Distributions@qn@@SANNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Distributions_StudentT_density(double x, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?cumulative@Exponential@Distributions@qn@@SANNN@Z", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Distributions_Exponential_cumulative(double x, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?density@Exponential@Distributions@qn@@SANNN@Z", CallingConvention = CallingConvention.StdCall)]
        public static extern int Distributions_Exponential_density(double x, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_Distributions_Exponential_density@@YANNN@Z")]
        public static extern int Distributions_Exponential_density1(double x, double jarg2);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_HomogeneousBasketModel_ComputeDistributions__SWIG_0@@YAXIUDtPINVOKE@qn@@0HHHHHPEAX1HH111@Z")]
        public static extern void HomogeneousBasketModel_ComputeDistributions_SWIG_0(bool wantProb, Dt asOf, Dt maturity,int stepSize, TimeUnit stepUnit,MagnoliaIG.ToolKits.Base.CopulaType copulaType,int dfCommon, int dfIdio, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, int intgFirst, int intgSecond, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] levels, HandleRef lossDist);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_HomogeneousBasketModel_ComputeDistributions__SWIG_1@@YAXIUDtPINVOKE@qn@@0HHHHHPEAX1HH1111@Z")]
        public static extern void HomogeneousBasketModel_ComputeDistributions_SWIG_1(bool wantProb, Dt asOf, Dt maturity, int stepSize, TimeUnit stepUnit, MagnoliaIG.ToolKits.Base.CopulaType copulaType, int dfCommon, int dfIdio, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, int intgFirst, int intgSecond, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] altCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] levels, HandleRef lossDist);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_HomogeneousBasketModel_ComputeDistributions__SWIG_2@@YAXIUDtPINVOKE@qn@@0HHHHHPEAX111HH111@Z")]
        public static extern void HomogeneousBasketModel_ComputeDistributions_SWIG_2(bool wantProb, Dt asOf, Dt maturity, int stepSize, TimeUnit stepUnit, MagnoliaIG.ToolKits.Base.CopulaType copulaType, int dfCommon, int dfIdio, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] altCorr, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] altDates, int intgFirst, int intgSecond, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] levels, HandleRef lossDist);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_HeterogeneousBasketModel_ComputeDistributions__SWIG_0@@YAXIUDtPINVOKE@qn@@0HHHHHPEAX1HH11111N11@Z")]
        public static extern void HeterGenerousBasketModel_ComputeDistributions_SWIG_0(bool wantProb, Dt asOf, Dt maturity, int stepSize, TimeUnit stepUnit, MagnoliaIG.ToolKits.Base.CopulaType copulaType, int dfCommon, int dfIdio, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, int intgFirst, int intgSecond, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] principals,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] recoveries, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] recoveryDist,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] levels,double gridSize, HandleRef lossDist, HandleRef amortDist);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_HeterogeneousBasketModel_ComputeDistributions__SWIG_1@@YAXIUDtPINVOKE@qn@@0HHHHHPEAX1HH111111N11@Z")]
        public static extern void HeterGenerousBasketModel_ComputeDistributions_SWIG_1(bool wantProb, Dt asOf, Dt maturity, int stepSize, TimeUnit stepUnit, MagnoliaIG.ToolKits.Base.CopulaType copulaType, int dfCommon, int dfIdio, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corrData, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, int intgFirst, int intgSecond, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] bCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] principals, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] recoveries, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] recoveryDist, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] levels, double gridSize, HandleRef lossDist, HandleRef amortDist);
        //?CSharp_HeterogeneousBasketModel_ComputeDistributions__SWIG_2@@YAXIUDtPINVOKE@qn@@0HHHHHPEAX111HH11111N11@Z

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_LargePoolBasketModel_Cumulative@@YANIHHHPEAXNHHNNN@Z")]
        public static extern double LargePoolBasketModel_Cumulative(bool wantProb, MagnoliaIG.ToolKits.Base.CopulaType copulaType, int dfCommon, int dfIdio, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] copulaParams, double factors, int intgFirst, int intgSecond, double dftProb,double lowerBound, double higherBound);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_LargePoolBasketModel_ComputeDistributions__SWIG_0@@YANIUDtPINVOKE@qn@@0HHHPEAX11HH111NN@Z")]
        public static extern double LargePoolBasketModel_ComputeDistributions_SWIG_0(bool wantProb, Dt begin, Dt end, MagnoliaIG.ToolKits.Base.CopulaType copulaType, int dfCommon, int dfIdio,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] copulaParams, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corr, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, int intgFirst, int intgSecond, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] principals, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] recoveries, double attachment, double detachment);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_LargePoolBasketModel_ComputeDistributions__SWIG_1@@YAXIHHHHHPEAX00HH0000@Z")]
        public static extern double LargePoolBasketModel_ComputeDistributions_SWIG_1(bool wantProb, int startIdx, int endIdx, MagnoliaIG.ToolKits.Base.CopulaType copulaType, int dfCommon, int dfIdio, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] copulaParams, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] corr, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] corrDates, int intgFirst, int intgSecond, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CurveMarshaler))]BaseEntity.Toolkit.Curves.Native.Curve[] survivalCurves, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] principals, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] recoveries, HandleRef distribution);

        #endregion SemiAnalyticModel

        #region ForwardVolatilityCube

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ForwardVolatilityCube_GetAsOf@@YA?AUDtPINVOKE@qn@@PEAX@Z")]
        public static extern Dt NativeForwardVolatilityCube_get_AsOf(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ForwardVolatilityCube_SetAsOf@@YAXPEAXUDtPINVOKE@qn@@@Z")]
        public static extern void NativeForwardVolatilityCube_set_AsOf(HandleRef curveRef, Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_ForwardVolatilityCube_0@@YAPEAXXZ")]
        public static extern IntPtr new_NativeForwardVolatilityCube__SWIG_0();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_new_ForwardVolatilityCube_1@@YAPEAXUDtPINVOKE@qn@@@Z")]
        public static extern IntPtr new_NativeForwardVolatilityCube__SWIG_1(Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ForwardVolatilityCube_GetIsFlatCube@@YA_NPEAX@Z")]
        public static extern bool NativeForwardVolatilityCube_get_IsFlatCube(HandleRef curveRef);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ForwardVolatilityCube_SetIsFlatCube@@YAXPEAXI@Z")]
        public static extern void NativeForwardVolatilityCube_set_IsFlatCube(HandleRef curveRef, bool isFlat);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ForwardVolatilityCube_AddValue@@YAXPEAXHHHN@Z")]
        public static extern void NativeForwardVolatilityCube_AddValue(HandleRef curveRef, int dateIdx, int expiryIdx, int strikeIdx, double val);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ForwardVolatilityCube_Set_InternalState@@YAXPEAX00000UDtPINVOKE@qn@@@Z")]
        public static extern void NativeForwardVolatilityCube_Set_InternalState(HandleRef curveRef, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] runningTimes, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] expiries, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] strikes,[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] vals, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] firstCapIdx, Dt asOf);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_ForwardVolatilityCube_SetFirstCapIdx@@YAXPEAX0@Z")]
        public static extern void NativeForwardVolatilityCube_SetFirstCapIdx(HandleRef curveRef, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] firstCapIdx);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeForwardVolatilityCube_Get_InternalState_Strikes@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] NativeForwardVolatilityCube_Get_InternalState_Strikes(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeForwardVolatilityCube_Get_InternalState_Values@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] NativeForwardVolatilityCube_Get_InternalState_Values(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeForwardVolatilityCube_Get_InternalState_RunningTimes@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]
        public static extern int[] NativeForwardVolatilityCube_Get_InternalState_RunningTimes(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeForwardVolatilityCube_Get_InternalState_Expiries@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]
        public static extern int[] NativeForwardVolatilityCube_Get_InternalState_Expiries(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeForwardVolatilityCube_Get_InternalState_FirstCapIdx@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]
        public static extern int[] NativeForwardVolatilityCube_Get_InternalState_FirstCapIdx(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_ForwardVolatilityCube@@YAXPEAX@Z")]
        public static extern void delete_NativeForwardVolatilityCube(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeForwardVolatilityCube_CalcCapletVolatility@@YANPEAXUDtPINVOKE@qn@@N@Z")]
        public static extern double NativeForwardVolatilityCube_CalcCapletVolatility(HandleRef jarg1, Dt expiry, double strike);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_NativeForwardVolatilityCube_Interpolate@@YANPEAXUDtPINVOKE@qn@@1N@Z")]
        public static extern double NativeForwardVolatilityCube_Interpolate(HandleRef jarg1, Dt date, Dt expiry, double strike);

        #endregion FwdVolCube

        #region DividendSchedule

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_Swig_0@@YAPEAXXZ")]
        public static extern IntPtr DividendSchedule_new_Swig_0();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_getSize@@YAHPEAX@Z")]
        public static extern int DividendSchedule_getSize(HandleRef jarg0);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_getExDivDate@@YA?AUDtPINVOKE@qn@@PEAXH@Z")]
        public static extern Dt DividendSchedule_getExDivDate(HandleRef jarg0, int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_getDt@@YA?AUDtPINVOKE@qn@@PEAXH@Z")]
        public static extern Dt DividendSchedule_getDt(HandleRef jarg0, int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_getTime@@YANPEAXH@Z")]
        public static extern double DividendSchedule_getTime(HandleRef jarg0, int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_getAmount@@YANPEAXH@Z")]
        public static extern double DividendSchedule_getAmount(HandleRef jarg0, int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_getType@@YAHPEAXH@Z")]
        public static extern int DividendSchedule_getType(HandleRef jarg0, int jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_Add_Swig_0@@YAXPEAXUDtPINVOKE@qn@@1NNH@Z")]
        public static extern void DividendSchedule_Add_Swig_0(HandleRef jarg1, Dt exDiv, Dt date, double time, double amt, int type);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_GetInternalData_ExDivDateArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]
        public static extern Dt[] DividendSchedule_GetInternalData_ExDivArray(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_GetInternalData_DateArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]
        public static extern Dt[] DividendSchedule_GetInternalData_DateArray(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_GetInternalData_TimeArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] DividendSchedule_GetInternalData_TimeArray(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_GetInternalData_AmountArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]
        public static extern double[] DividendSchedule_GetInternalData_AmountArray(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_GetInternalData_TypeArray@@YAPEAXPEAX@Z")]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef=typeof(ArrayOfIntMarshaler))]
        public static extern int[] DividendSchedule_GetInternalData_TypeArray(HandleRef jarg1);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_DividendSchedule_SetInternalData@@YAXPEAX00000@Z")]
        public static extern void DividendSchedule_SetInternalData(HandleRef jarg1, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg2, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDtMarshaler))]Dt[] jarg3, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg4, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfDoubleMarshaler))]double[] jarg5, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(ArrayOfIntMarshaler))]int[] jarg6);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("MagnoliaCppWrapper", EntryPoint = "?CSharp_delete_DividendSchedule@@YAXPEAX@Z")]
        public static extern void delete_DividendSchedule(HandleRef jarg1);

        #endregion DividendSchedule

        //public static double TestCppSum()
        //{
        //    int noElements = 1000;
        //    int[] myArray = new int[noElements];
        //    for (int i = 0; i < noElements; i++)
        //        myArray[i] = i * 10;

        //    unsafe
        //    {
        //        fixed (int* pArray = &myArray[0])
        //        {
        //            CppWrapperClass controlCpp = new CppWrapperClass(pArray, noElements);

        //            double ret = controlCpp.getSum();
        //            return controlCpp.sum;
        //        }
        //    }
        //}
    }
}
