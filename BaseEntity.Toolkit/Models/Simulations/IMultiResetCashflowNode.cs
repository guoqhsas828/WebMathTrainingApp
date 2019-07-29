using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Models.Simulations
{
  #region IResetNode

  /// <summary>
  ///  Interface for a single reset point in time.
  /// </summary>
  /// <remarks>
  /// <para>A reset node is a point in time when the observations of market
  ///  data (interest rates, stock and commodity prices, credit spreads, etc)
  ///  were made and the state variables for payment calculations are updated.
  ///  Objects implementing this interface should hold a reference to the
  ///  market objects necessary to update the states, which will be evolved
  ///  during simulation. </para>
  /// 
  ///  <para>Objects implementing this interface should be marked as 
  ///  Serializable.</para>
  /// </remarks>
  public interface IResetNode
  {
    /// <summary>
    /// Observation date for the market data
    /// </summary>
    Dt Date { get; }

    /// <summary>
    ///  Update the state variables for payment calculations,
    ///  called on the observation date.
    /// </summary>
    void Update();
  }

  #endregion

  #region IMultiResetCashflowNode

  /// <summary>
  ///   Interface for the cash flow nodes with multiple resets prior to
  ///   payment date.
  /// </summary>
  public interface IMultiResetCashflowNode : ICashflowNode
  {
    /// <summary>
    ///  Gets a list of reset nodes ordered by reset dates.
    /// </summary>
    /// <remarks>
    ///  <para>This property can be null.</para>
    ///  <para>If not null, then the <c>Update()</c> method of each reset node
    ///   is called after the path is evolved to the date of the node, before
    ///   the <c>RealizedAmount()</c> method is called on the cashflow node.</para>
    ///  <para>This interface can be used to support the cash flow payments built
    ///   on multiple observations of market data, such as coupound rates,
    ///   range accruals and discretely monitored barrier options.</para>
    /// </remarks>
    IEnumerable<IResetNode> ResetNodes { get; }
  }

  #endregion
}
