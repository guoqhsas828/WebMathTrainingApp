// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataTableExtensions.cs" company="WebMathTraining, Inc">
//   (c) 2011 WebMathTraining, Inc
// </copyright>
// <summary>
//   Extension methods for System.Data.DataTable
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace BaseEntity.Database.Extension
{
  using System.Data;
  using System.Linq;

  /// <summary>
  /// Extension methods for System.Data.DataTable
  /// </summary>
  public static class DataTableExtensions
  {
    #region Public Methods

    /// <summary>
    /// Validates that the specified columns are in the table.
    /// </summary>
    /// <param name="dataTable">
    /// The data table.
    /// </param>
    /// <param name="mandatoryColumns">
    /// The mandatory columns.
    /// </param>
    public static void ValidateMandatoryColumns(this DataTable dataTable, params string[] mandatoryColumns)
    {
      var missing = mandatoryColumns.Except(dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName)).ToList();
      if (missing.Any())
      {
        throw new DataException(
          string.Format("Table is missing the following mandatory columns: {0}", string.Join(",", missing.ToArray())));
      }
    }

    /// <summary>
    /// Validates the uniqueness of the data in a column of the table.
    /// </summary>
    /// <param name="dataTable">
    /// The data table.
    /// </param>
    /// <param name="columnName">
    /// Name of the column.
    /// </param>
    public static void ValidateUniqueColumn(this DataTable dataTable, string columnName)
    {
      if (dataTable.Rows.Cast<DataRow>().Select(r => r[columnName]).Distinct().Count() != dataTable.Rows.Count)
      {
        throw new DataException(string.Format("Table contains duplicate values for column: {0}", columnName));
      }
    }

    #endregion
  }
}