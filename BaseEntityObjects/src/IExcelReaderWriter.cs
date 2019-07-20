// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Interface for generating excel files
  /// </summary>
  /// <remarks>
  ///   Curently this interface has one implementation using EPPPlusAPI in Excel.Util assembly
  /// </remarks>
  public interface IExcelReaderWriter
  {
    /// <summary>
    /// Generates excel file in a temp folder with the given data tables and opens
    /// </summary>
    /// <param name="dataTableWithVisibleColumnsList">List of tuple which has  a DataTable and its visible columns list</param>
    /// <example>
    /// <code>
    ///    IExcelReaderWriter excelReaderWriter = new Excel.Util.EPPlusExcelReaderWriter(); 
    ///    excelReaderWriter.GenerateExcelAndOpen( new List&lt;DataTableWrapper&gt;(new DataTableWrapper(dataTable, new List{"ColumnA","ColumnB"})) )
    /// </code>
    /// </example>
    void GenerateExcelAndOpen(List<DataTableWrapper> dataTableWithVisibleColumnsList);

    /// <summary>
    /// Generate excel file with the datatables in the given path
    /// </summary>
    /// <param name="dataTableWithVisibleColumnsList">List of tuple which has  a DataTable and its visible columns list </param>
    /// <param name="filePath"></param>
    /// <param name="openExcelFile"></param>
    /// <example>
    /// <code>
    ///   IExcelReaderWriter excelReaderWriter = new Excel.Util.EPPlusExcelReaderWriter(); 
    ///   excelReaderWriter.GenerateExcel(  new List&lt;DataTableWrapper&gt;(new DataTableWrapper(dataTable, new List{"ColumnA","ColumnB"})), @"C:\RiskReport.xlsx", true ) 
    /// </code>
    /// </example>
    void GenerateExcel(List<DataTableWrapper> dataTableWithVisibleColumnsList, string filePath, bool openExcelFile);
  }
}