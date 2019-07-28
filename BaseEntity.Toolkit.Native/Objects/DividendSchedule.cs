using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Runtime.Serialization;
using MagnoliaIG.Cpp;
using BaseEntity.Toolkit.Base;

namespace MagnoliaIG.ToolKits.Base
{
    [Serializable]
    public sealed class DividendSchedule : IEnumerable<Tuple<Dt, Dt, DividendSchedule.DividendType, double>>, INativeSerializable
    {
        public enum DividendType
        {
            Fixed,
            Proportional //Dividend yield can be represented by this type too
        }

        private DividendSchedule()
            : this(CppLibraryInvoke.DividendSchedule_new_Swig_0(), true)
        {
        }

        internal DividendSchedule(IntPtr cPtr, bool cMemOwn)
        {
            mCPtr = new HandleRef(this, cPtr);
            mCMemOwn = cMemOwn;
        }

        public static HandleRef getCPtr(DividendSchedule obj)
        {
            return obj == null ? new HandleRef(null, IntPtr.Zero) : obj.mCPtr;
        }

        ~DividendSchedule()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (mCPtr.Handle != IntPtr.Zero && mCMemOwn)
            {
                mCMemOwn = false;
                CppLibraryInvoke.delete_DividendSchedule(mCPtr);
                if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            }
            mCPtr = new HandleRef(null, IntPtr.Zero);
            GC.SuppressFinalize(this);
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("asOf_", AsOf);
            info.AddValue("exDivs_", GetInternalData_ExDivArray());
            info.AddValue("dates_", GetInternalData_DateArray());
            info.AddValue("times_", GetInternalData_TimeArray());
            info.AddValue("amts_", GetInternalData_AmountArray());
            info.AddValue("types_", GetInternalData_TypeArray());
        }

        public DividendSchedule(Dt asOf) : this()
        {
            AsOf = asOf;
        }

        protected DividendSchedule(SerializationInfo info, StreamingContext context)
        {
            IntPtr cPtr = CppLibraryInvoke.DividendSchedule_new_Swig_0();
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            mCPtr = new HandleRef(this, cPtr);
            mCMemOwn = true;
            AsOf = (Dt)info.GetValue("asOf_", typeof(Dt));
            Dt[] exDivs = (Dt[])info.GetValue("exDivs_", typeof(Dt[]));
            Dt[] dates = (Dt[])info.GetValue("dates_", typeof(Dt[]));
            double[] times = (double[])info.GetValue("times_", typeof(double[]));
            double[] amts = (double[])info.GetValue("amts_", typeof(double[]));
            int[] types = (int[])info.GetValue("types_", typeof(int[]));
            CppLibraryInvoke.DividendSchedule_SetInternalData(mCPtr, exDivs, dates, times, amts, types);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
        }

        public DividendSchedule(Dt asOf, IEnumerable<Tuple<Dt, Dt, DividendType, double>> dividends)
            : this(asOf)
        {
            if (dividends == null)
                return;

            foreach (var dvdItem in dividends.OrderBy(div => div.Item2))
            {
                Add(dvdItem.Item1, dvdItem.Item2,dvdItem.Item3, dvdItem.Item4);  
            }
        }

        public void Add(Dt exDivDate, Dt date, DividendType type, double amt)
        {
            CppLibraryInvoke.DividendSchedule_Add_Swig_0(mCPtr, exDivDate, date, Dt.RelativeTime(AsOf, date), amt, (int)type);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
        }

        public void Add(Tuple<Dt, Dt , DividendType , double> toolkitDividend)
        {
            Add(toolkitDividend.Item1, toolkitDividend.Item2, toolkitDividend.Item3, toolkitDividend.Item4);
        }

        public DividendType[] Types
        {
            get { return GetInternalData_TypeArray().Select(t => (DividendType)t).ToArray(); }
        }

        public Dt[] Dates
        {
            get { return GetInternalData_DateArray(); }
        }

        public double GetTime(int i)
        {
            double ret = CppLibraryInvoke.DividendSchedule_getTime(mCPtr, i);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        public Dt GetExDivDate(int i)
        {
            Dt ret = CppLibraryInvoke.DividendSchedule_getExDivDate(mCPtr, i);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        public double GetAmount(int i)
        {
            var ret = CppLibraryInvoke.DividendSchedule_getAmount(mCPtr, i);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        public Dt GetDt(int i)
        {
            Dt ret = CppLibraryInvoke.DividendSchedule_getDt(mCPtr, i);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        public DividendType GetType(int i)
        {
            DividendType ret = (DividendType)CppLibraryInvoke.DividendSchedule_getType(mCPtr, i);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        public int Size()
        {
            int ret = CppLibraryInvoke.DividendSchedule_getSize(mCPtr);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        Dt[] GetInternalData_ExDivArray()
        {
            Dt[] ret = CppLibraryInvoke.DividendSchedule_GetInternalData_ExDivArray(mCPtr);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        Dt[] GetInternalData_DateArray()
        {
            Dt[] ret = CppLibraryInvoke.DividendSchedule_GetInternalData_DateArray(mCPtr);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        Double[] GetInternalData_TimeArray()
        {
            var ret = CppLibraryInvoke.DividendSchedule_GetInternalData_TimeArray(mCPtr);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        Double[] GetInternalData_AmountArray()
        {
            var ret = CppLibraryInvoke.DividendSchedule_GetInternalData_AmountArray(mCPtr);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        int[] GetInternalData_TypeArray()
        {
            var ret = CppLibraryInvoke.DividendSchedule_GetInternalData_TypeArray(mCPtr);
            if (CppLibraryInvoke.SWIGPendingException.Pending) throw CppLibraryInvoke.SWIGPendingException.Retrieve();
            return ret;
        }

        public IEnumerator<Tuple<Dt, Dt, DividendType, double>> GetEnumerator()
        {
            return new DividendEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Dt AsOf { get; set; }

        private class DividendEnumerator : IEnumerator<Tuple<Dt, Dt, DividendType, double>>
        {
            internal DividendEnumerator(DividendSchedule dvs)
            {
                _dividendSchedule = dvs;
            }

            public Tuple<Dt, Dt, DividendType, double> Current
            {
                get
                {
                    return new Tuple<Dt, Dt, DividendType, double>(_dividendSchedule.GetExDivDate(_position), _dividendSchedule.GetDt(_position), _dividendSchedule.Types[_position], _dividendSchedule.GetAmount(_position));
                }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                _position++;
                return _position < _dividendSchedule.Size();
            }

            public void Reset()
            {
                _position = -1;
            }

            void IDisposable.Dispose()
            { }
 
            private DividendSchedule _dividendSchedule;
            private int _position = -1;
        }

        private HandleRef mCPtr;
        private bool mCMemOwn;

    }
}
