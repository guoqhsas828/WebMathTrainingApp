// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System.Collections.Generic;
using System.Data;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Wrapper for a DataTable to extend the data table additional attributes
  /// </summary>
  public class DataTableWrapper
  {
    /// <summary>
    /// Data Table
    /// </summary>
    public DataTable Table { get; set; }

    /// <summary>
    /// Visible column names in the Table
    /// </summary>
    public List<string> VisibleColumns { get; set; }
  }
}
