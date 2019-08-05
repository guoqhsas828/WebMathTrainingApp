using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  public static class LoadData
  {
    #region LoadData

    public static CorrelationObject LoadCorrelationObject(string filename)
    {
      filename = GetTestFilePath(filename);
      CorrelationData cd = (CorrelationData) XmlLoadData(
        filename, typeof(CorrelationData));
      return cd.CreateCorrelationObject();
    }

    public static DiscountCurve LoadDiscountCurve(string filename)
    {
      DiscountCurve dc = LoadDiscountCurve(filename, Dt.Empty);
      return dc;
    }

    public static DiscountCurve LoadDiscountCurve(string filename, Dt asOfPassed)
    {
      // If asOfPassed is passed null, will assume that the input file contains the AsOf date.
      // Otherwise, will override with the date we pass here.
      DiscountCurve discountCurve = null;
      filename = GetTestFilePath(filename);
      DiscountData dd = (DiscountData) XmlLoadData(filename, typeof(DiscountData));
      if (!asOfPassed.IsEmpty())
        dd.AsOf = asOfPassed
          .ToStr("%D"); // In DiscountData object, the asof date is stored as a string. Set it here before creating the curve with this date.
      discountCurve = dd.GetDiscountCurve();
      if (discountCurve == null)
        throw new System.Exception(filename + ": Invalid discount data");
      return discountCurve;
    }

    public static SurvivalCurve[] LoadCreditCurves(string filename, DiscountCurve discountCurve)
    {
      SurvivalCurve[] survivalCurves = null;
      filename = GetTestFilePath(filename);
      CreditData cd = (CreditData) XmlLoadData(filename, typeof(CreditData));
      survivalCurves = cd.GetSurvivalCurves(discountCurve);
      if (survivalCurves == null)
        throw new System.Exception(filename + ": Invalid credit data");
      return survivalCurves;
    }

    public static SurvivalCurve[] LoadCreditCurves(
      string filename, DiscountCurve discountCurve, string[] names)
    {
      SurvivalCurve[] survivalCurves = null;
      filename = GetTestFilePath(filename);
      CreditData cd = (CreditData) XmlLoadData(filename, typeof(CreditData));
      survivalCurves = cd.GetSurvivalCurves(discountCurve, names);
      if (survivalCurves == null)
        throw new System.Exception(filename + ": Invalid credit data");
      return survivalCurves;
    }

    public static SyntheticCDO[][] LoadTranches(string filename)
    {
      SyntheticCDO[][] cdos = null;
      filename = GetTestFilePath(filename);
      BasketData.TrancheQuotes td = (BasketData.TrancheQuotes)
        XmlLoadData(filename, typeof(BasketData.TrancheQuotes));
      cdos = td.ToCDOs();
      if (cdos == null)
        throw new System.Exception(filename + ": Invalid tranche quotes data");
      return cdos;
    }

    public static BasketData.TrancheQuotes LoadTrancheQuotes(string filename)
    {
      filename = GetTestFilePath(filename);
      BasketData.TrancheQuotes td = (BasketData.TrancheQuotes)
        XmlLoadData(filename, typeof(BasketData.TrancheQuotes));
      return td;
    }

    public static Copula LoadCopula(string copulaData)
    {
      if (copulaData != null && copulaData.Length > 0)
      {
        // remove all spaces
        string[] elems = copulaData.Replace(" ", "").Split(new char[] {','});
        if (elems.Length < 3)
          throw new ArgumentException("Invalid copula data");
        CopulaType copulaType = (CopulaType) Enum.Parse(typeof(CopulaType), elems[0]);
        int dfCommon = Int32.Parse(elems[1]);
        int dfIdiosyncratic = Int32.Parse(elems[2]);
        return new Copula(copulaType, dfCommon, dfIdiosyncratic);
      }
      else
        return null;
    }

    private static SurvivalCurve FindCurve(
      SurvivalCurve[] curves, string name)
    {
      if (name == null) return null;
      foreach (SurvivalCurve sc in curves)
        if (String.Compare(sc.Name, name, true) == 0)
          return sc;
      throw new System.Exception(String.Format("Credit curve '{0}' not found", name));
    }

    #endregion // LoadData

    #region Find_File_Locations

    /// <summary>
    ///   Find the full path of a test file (the input/expects files)
    /// </summary>
    /// 
    /// <remarks>
    /// This function looks for the file in the directories
    /// arranged in following order:
    /// current working directory;
    /// root,
    /// root/toolkit,
    /// root/toolkit/test, 
    /// root/toolkit/test/pricers.
    /// </remarks>
    /// 
    /// <param name="filename">The file to find</param>
    /// <param name="throwException">
    ///   If true, throw FileNotFoundException when the file is not found;
    ///   if false, return null when the file is not found.
    /// </param>
    /// <returns>full path of the file</returns>
    public static string GetTestFilePath(
      string filename, bool throwException)
    {
      filename = NormalizeFileName(filename);
      var qnroot = BaseEntityContext.InstallDir;
      var fn = RecursiveSearchDirectories(filename,
        "", qnroot, "toolkit", "test", "pricers");
      if (fn != null)
        return fn;
      fn = RecursiveSearchDirectories(filename,
        qnroot, "risk", "test", "src");
      if (fn != null)
        return fn;
      fn = RecursiveSearchDirectories(filename,
        qnroot, "waterfall", "test", "data");
      if (fn == null && throwException)
        throw new System.IO.FileNotFoundException(
          "File \'" + filename + "\' not found");
      return fn;
    }

    internal static string NormalizeFileName(string filename)
    {
      var platform = IntPtr.Size == 8 ? "x64" : "x86";
      return filename.Replace("%platform%", platform)
        .Replace("/", Path.DirectorySeparatorChar.ToString())
        .Replace("\\", Path.DirectorySeparatorChar.ToString());
    }

    /// <summary>
    ///   Find the full path of a test file (the input/expects files)
    /// </summary>
    /// 
    /// <remarks>
    /// This function looks for the file in the directories
    /// arranged in following order:
    /// current working directory;
    /// root,
    /// root/toolkit,
    /// root/toolkit/test, 
    /// root/toolkit/test/pricers.
    /// </remarks>
    /// 
    /// <param name="filename">The file to find</param>
    /// <returns>full path of the file</returns>
    public static string GetTestFilePath(string filename)
    {
      return GetTestFilePath(filename, true);
    }

    /// <summary>
    ///   Find a list of test files
    /// </summary>
    /// 
    /// <remarks>
    /// This function looks for the files in the directory
    /// root/toolkit/test/pricers.
    /// </remarks>
    /// 
    /// <param name="filter">The pattern for the files to find</param>
    /// <returns>List of the files</returns>
    public static string[] GetTestFiles(string filter)
    {
      string root = Path.Combine(BaseEntityContext.InstallDir,
        "toolkit", "test", "pricers");
      DirectoryInfo dir = new DirectoryInfo(root);
      FileInfo[] files = dir.GetFiles(filter);
      string[] fnames = new string[files.Length];
      for (int i = 0; i < files.Length; ++i)
        fnames[i] = files[i].Name;
      return fnames;
    }

    private static string RecursiveSearchDirectories(
      string filename, params string[] directories)
    {
      string directory = "";
      foreach (string dir in directories)
      {
        if (dir != null && dir.Length > 0)
          directory += dir + Path.DirectorySeparatorChar;
        string fn = directory + filename;
        if (File.Exists(fn))
          return fn;
      }
      return null;
    }
    #endregion Find_File_Locations

    #region LoadSave_XML_Data
    /// <summary>
    /// Load a object from file in XML format
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="type">The type.</param>
    /// <returns>System.Object.</returns>
    /// <exception cref="System.NullReferenceException"></exception>
    public static object XmlLoadData(string filename, Type type)
    {
      if (filename == null || filename.Length <= 0)
        throw new System.NullReferenceException(
          String.Format("XML filename for {0} not specified", type));
      XmlSerializer serializer = new XmlSerializer(type);
      using (FileStream fs = new FileStream(filename,
        FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        object o = serializer.Deserialize(fs);
        fs.Close();
        return o;
      }
    }

    /// <summary>
    /// Load a object to file in XML format
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="type">The type.</param>
    /// <param name="data">The data.</param>
    /// <exception cref="System.NullReferenceException"></exception>
    public static void XmlSaveData(string filename, Type type, object data)
    {
      if (filename == null || filename.Length <= 0)
        throw new System.NullReferenceException(String.Format("XML filename for {0} not specified", type));
      XmlSerializer serializer = new XmlSerializer(type);
      using (TextWriter writer = new StreamWriter(filename))
      {
        serializer.Serialize(writer, data);
        writer.Close();
      }
    }
    #endregion // LoadSave_XML_Data
  }
}
