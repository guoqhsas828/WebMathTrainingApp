/*
 * CurveSolver.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Permissions;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Solver for the first below time
  /// </summary>
  /// 
  /// <remarks>
  /// <para>
  ///  Let <formula inline="true">F(t)</formula> be a curve.
  ///  For a given start time <formula inline="true">t_0</formula>
  ///  and a value level <formula inline="true">p</formula>,
  ///  we define a function <formula inline="true">T(p, t_0)</formula> by
  /// 
  /// <formula>
  ///  T(p, t_0) \equiv  \left\{ \begin{array}{ll}
  ///       \inf \{t \geq 0:\quad F(t + t_0) \geq p F(t_0) \} \quad\mbox{if solution exists};\\
  ///      \infty \quad\mbox{otherwise}.\end{array} \right.
  /// </formula>
  /// </para>
  /// 
  /// <para>
  ///   When <formula inline="true">F(t)</formula> is a credit curve,
  ///   <formula inline="true">T(p, t_0)</formula>
  ///   gives the default time corresponding to a survival probability
  ///   <formula inline="true">p</formula> conditional on 
  ///   the credit surviving on date <formula inline="true">t_0</formula>.
  /// </para>
  /// 
  /// <para>
  ///   In most cases, we are only interested in the default time within a interval
  ///   <formula inline="true">[t_0, t_1]</formula> and 
  ///   we do not care what happens after the time <formula inline="true">t_1</formula>.
  ///   We can define a modified version of the function
  ///   <formula>
  ///  T(p, t_0, t_1) \equiv  \left\{ \begin{array}{ll}
  ///         \inf \{0 \leq t \leq t_1 - t_0:\quad F(t + t_0) \geq p F(t_0) \} \quad\mbox{if solution exists};\\
  ///     t_1 - t_0 + 1 \quad\mbox{otherwise}.\end{array} \right.   
  ///   </formula>
  /// </para>
  /// 
  /// <para>
  ///   The CurveSolver class is designed to represent the function
  ///   <formula inline="true">T(p, t_0, t_1)</formula> for a given curve.
  /// </para>
  /// 
  /// </remarks>
  [Serializable]
  public class CurveSolver : BaseEntityObject, INativeSerializable, IDisposable
  {
    #region Swig
    private HandleRef swigCPtr;

    /// <exclude />
    protected bool swigCMemOwn;

    internal CurveSolver(IntPtr cPtr, bool cMemoryOwn)
    {
      swigCMemOwn = cMemoryOwn;
      swigCPtr = new HandleRef(this, cPtr);
    }

    /// <exclude />
    public static HandleRef getCPtr(CurveSolver obj)
    {
      return (obj == null) ? new HandleRef(null, IntPtr.Zero) : obj.swigCPtr;
    }

    /// <exclude />
    ~CurveSolver()
    {
      Dispose();
    }

    /// <exclude />
    public virtual void Dispose()
    {
      if (swigCPtr.Handle != IntPtr.Zero && swigCMemOwn)
      {
        swigCMemOwn = false;
        BaseEntityPINVOKE.delete_CurveSolverImpl(swigCPtr);
      }
      swigCPtr = new HandleRef(null, IntPtr.Zero);
      GC.SuppressFinalize(this);
    }
    #endregion // Swig

    #region Serialize
    ///<exclude/>
    [SecurityPermission(SecurityAction.Demand, SerializationFormatter=true)]
    public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (! swigCMemOwn )
        throw new ToolkitException("Object can not be serialized when swigCMemOwn is false.");

      info.AddValue("curve_", curve_);
      info.AddValue("begin_", begin_);
      info.AddValue("end_", end_);
      info.AddValue("accuracy_", accuracy_);
      info.AddValue("iterations_", iterations_);
    }

    ///<exclude/>
    protected CurveSolver(SerializationInfo info, StreamingContext context)
    {
      curve_ = (Curve)info.GetValue("curve_", typeof(Curve));
      begin_ = (Dt)info.GetValue("begin_", typeof(Dt));
      end_ = (Dt)info.GetValue("end_", typeof(Dt));
      accuracy_ = (double)info.GetValue("accuracy_", typeof(double));
      iterations_ = (int)info.GetValue("iterations_", typeof(int));
      if (curve_ != null)
        Create(curve_, begin_, end_, accuracy_, iterations_);
    }
    #endregion // Serialize

    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="curve">Curve used by the solver</param>
    /// <param name="begin">Period begin date (<formula inline="true">t_0</formula>)</param>
    /// <param name="end">Period end date (<formula inline="true">t_1</formula>)</param>
    public CurveSolver(Curve curve, Dt begin, Dt end)
    {
      curve_ = curve;
      begin_ = begin;
      end_ = end;
      accuracy_ = 1E-6;
      iterations_ = 2000;
      if (curve != null)
        Create(curve, begin, end, accuracy_, iterations_);
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="curve">Curve used by solver</param>
    /// <param name="begin">Period begin date (<formula inline="true">t_0</formula>)</param>
    /// <param name="end">Period end date (<formula inline="true">t_1</formula>)</param>
    /// <param name="accuracy">Accuracy level</param>
    /// <param name="iterations">Number of iterations</param>
    public CurveSolver(Curve curve, Dt begin, Dt end, double accuracy, int iterations)
    {
      curve_ = curve;
      begin_ = begin;
      end_ = end;
      accuracy_ = accuracy;
      iterations_ = iterations;
      if (curve != null)
        Create(curve, begin, end, accuracy, iterations);
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned solver</returns>
    public override object Clone()
    {
      CurveSolver obj = (CurveSolver)base.Clone();
      if (curve_ != null)
      {
        Curve curve = (Curve)curve_.Clone();
        obj.Create(curve, begin_, end_, accuracy_, iterations_);
      }
      return obj;
    }

    /// <summary>
    ///   Construct a curve solver
    /// </summary>
    /// <remarks>This function should only be called from constructor.</remarks>
    /// <param name="curve">Curve used to solve for date</param>
    /// <param name="begin">Period begin date</param>
    /// <param name="end">Period end date</param>
    /// <param name="accuracy">Accuracy level</param>
    /// <param name="iterations">Number of iterations</param>
    private void Create(Curve curve, Dt begin, Dt end, double accuracy, int iterations)
    {
      IntPtr cPtr = BaseEntityPINVOKE.CurveSolverImpl_Create(Curve.getCPtr(curve), false, begin, end, accuracy, iterations);
      swigCMemOwn = true;
      swigCPtr = new HandleRef(this, cPtr);
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Solve for the earliest time when the curve is below a value
    /// </summary>
    /// 
    /// <remarks>
    ///   This function calculates the value of the function
    ///   <formula inline="true">T(p, t_0, t_1)</formula>,
    ///   where <formula inline="true">p</formula> is the input parameter,
    ///   <formula inline="true">t_0</formula> and <formula inline="true">t_1</formula>
    ///   are the begin date and end date supplied to the constructors of the solver, respectively.
    /// </remarks>
    /// 
    /// <param name="p">Value to match</param>
    /// <returns>Days from period begin date</returns>
    public double Solve(double p)
    {
      if (curve_ == null) return Double.NaN;
      double ret = BaseEntityPINVOKE.CurveSolverImpl_Solve(swigCPtr, p);
      if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
      return ret;
    }
    #endregion // Methods

    #region Data
    private Curve curve_;
    private Dt begin_, end_;
    private double accuracy_;
    private int iterations_;
    #endregion // Data
  }
}
