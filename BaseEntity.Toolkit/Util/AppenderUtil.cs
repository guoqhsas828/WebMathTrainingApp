using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  /// Util class to handle log aggregations for toolkit libraries
  /// </summary>
  public class AppenderUtil
  {
    /// <summary>
    /// Append a set of curves to a log aggregator where the key is the curves name
    /// </summary>
    /// <param name="curves"></param>
    /// <param name="logAggregator"></param>
    public static void AppendCurves(IEnumerable<Curve> curves, ILogAggregator logAggregator)
    {
      foreach (var curve in curves)
      {
        logAggregator.Append(curve.Name, curve);
      }
    }

    /// <summary>
    /// Append a set of spots to a log aggregator where the key is the spot name
    /// </summary>
    /// <param name="spots"></param>
    /// <param name="logAggregator"></param>
    public static void AppendSpots(IEnumerable<ISpot> spots, ILogAggregator logAggregator)
    {
      foreach (var spot in spots)
      {
        logAggregator.Append(spot.Name, spot);
      }
    }

    /// <summary>
    /// Append a set of dates to a log aggregator where the key is the date as a string (ToString())
    /// </summary>
    /// <param name="dts"></param>
    /// <param name="logAggregator"></param>
    public static void AppendDates(IEnumerable<Dt> dts, ILogAggregator logAggregator)
    {
      foreach (var dt in dts)
      {
        logAggregator.Append(dt.ToString(), dt);
      }
    }

    /// <summary>
    /// Append a set of tenors to a log aggregator where the key is the tenor as a string (ToString())
    /// </summary>
    /// <param name="tenors"></param>
    /// <param name="logAggregator"></param>
    public static void AppendTenors(IEnumerable<Tenor> tenors, ILogAggregator logAggregator)
    {
      foreach (var tenor in tenors)
      {
        logAggregator.Append(tenor.ToString(), tenor);
      }
    }

    /// <summary>
    /// Create a data set using a datatable
    /// </summary>
    /// <param name="dataTable"></param>
    /// <returns></returns>
    public static DataSet DataTableToDataSet(DataTable dataTable)
    {
      var dataSet = new DataSet();
      dataSet.Merge(dataTable);
      return dataSet;
    }
  }
}
