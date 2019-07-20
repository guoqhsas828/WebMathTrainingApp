// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace BaseEntity.Configuration
{
  /// <summary>
  ///
  /// </summary>
  public class AssemblyResolver
  {
    /// <summary>
    /// </summary>
    public class AssemblyConflictEventArgs
    {
      /// <summary>
      ///  Constructor
      /// </summary>
      /// <param name="loaded"></param>
      /// <param name="loading"></param>
      public AssemblyConflictEventArgs(Assembly loaded, Assembly loading)
      {
        AssemblyLoaded = loaded;
        AssemblyLoading = loading;
      }

      /// <summary>
      /// 
      /// </summary>
      public Assembly AssemblyLoaded { get; set; }

      /// <summary>
      /// 
      /// </summary>
      public Assembly AssemblyLoading { get; set; }
    }

    private static readonly Hashtable AssemblyFileLockFreeDirectoryTable = new Hashtable();
    private static readonly Hashtable InMemoryAssemblyTable = new Hashtable();

    /// <summary>
    ///  Adds the assembly to the HashTable
    /// </summary>
    /// <param name="assemblyName"></param>
    /// <param name="assembly"></param>
    public static void AddInMemoryAssembly(AssemblyName assemblyName, byte[] assembly)
    {
      InMemoryAssemblyTable[assemblyName] = assembly;
    }

    /// <summary>
    ///   
    /// </summary>
    /// <param name="directory"></param>
    public static void AddAssemblyFileLockFreeDirectory(string directory)
    {
      //first to formalize the directory a little bit
      var di = new DirectoryInfo(directory);
      var formalizedDirectory = di.FullName;
      if (AssemblyFileLockFreeDirectoryTable.ContainsKey(formalizedDirectory) == false)
      {
        AssemblyFileLockFreeDirectoryTable[formalizedDirectory] = true;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="args"></param>
    public delegate void AssemblyConflictEventHandler(AssemblyConflictEventArgs args);

    /// <summary>
    /// 
    /// </summary>
    public static event AssemblyConflictEventHandler OnAssemblyConflictEvent;

    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(AssemblyResolver));

    private AssemblyResolver()
    {}

    private static readonly ArrayList PathList = new ArrayList();

    /// <summary>
    /// Event handler: when the current domain can NOT found some assembly.
    /// This class will do a file search in all directories added using AddSearchPath() calls.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    /// <returns>The assembly found by this class. NULL means can not find.</returns>
    private static Assembly ResolveEventHandler(object sender, ResolveEventArgs args)
    {
      Logger.DebugFormat("in AssemblyResolver.ResolveEventHandler ({0})", args.Name);

      bool bCheckVersionInformation;
      string name;
      var index = args.Name.IndexOf(',');
      if (index == -1)
      {
        name = args.Name.ToLower(); //sometimes the .Net framework only pass in the short name, without version information
        bCheckVersionInformation = false;
      }
      else
      {
        name = args.Name.Substring(0, index);
        bCheckVersionInformation = true;
      }

      //NOTE: if is rare that .resources file is not embedded within the assemblies
      //don't probe for all resources.
      if (name.EndsWith("resources")) return null;

      Assembly assembly = null; //return value to be here
      if (name != "")
      {
        //As the first step, we will search for assembly that already been loaded (CLR will not recognize
        //that the needed assembly is already been loaded if args.Name is not a strong name)
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var loadedAssembly in assemblies)
        {
          if (loadedAssembly.GetName().Name == args.Name || loadedAssembly.GetName().FullName == args.Name)
          {
            Logger.DebugFormat("Assembly {0} already loaded", loadedAssembly.GetName().Name);
            assembly = loadedAssembly;
            break;
          }
        }

        //then search for in-memory assembly table
        if (assembly == null)
        {
          byte[] inMemoryAssemblyContent = null;

          foreach (AssemblyName assemblyName in InMemoryAssemblyTable.Keys)
          {
            //(1) first check if full name match
            if (String.CompareOrdinal(assemblyName.FullName, args.Name) == 0)
            {
              inMemoryAssemblyContent = InMemoryAssemblyTable[assemblyName] as byte[];
              break;
            }
            //(2) then check if partial match
            if (String.Compare(assemblyName.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
            {
              inMemoryAssemblyContent = InMemoryAssemblyTable[assemblyName] as byte[];
              break;
            }
          }
          if (inMemoryAssemblyContent != null)
            assembly = Assembly.Load(inMemoryAssemblyContent);
        }

        //If can not find, search this DLL for all search path
        if (assembly == null)
        {
          var enumerator = PathList.GetEnumerator();
          while ((enumerator.MoveNext()))
          {
            var path = (string)(enumerator.Current);
            var fullPath = Path.Combine(path, name);
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Exists == true)
            {
              //NOTE: Don't call LoadFrom() which will load this assembly into this domain,
              //we only need to get the version information here.
              var assemblyName = AssemblyName.GetAssemblyName(fullPath);
              var assemblyQualifiedName = assemblyName.ToString();
              if (bCheckVersionInformation == false || String.Compare(assemblyQualifiedName, args.Name, StringComparison.OrdinalIgnoreCase) == 0) //case-insensitive compare
              {
                Logger.DebugFormat("Assembly probing successful: {0} is loaded", fullPath);

                //check if the directory should be file-lock-free
                var di = new DirectoryInfo(path);
                var formalizedDirectoryName = di.FullName;
                if (AssemblyFileLockFreeDirectoryTable.ContainsKey(formalizedDirectoryName))
                {
                  //will load as in-memeroy bytes, in order to avoid having file locking
                  using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                  {
                    var buf = new byte[fs.Length];
                    fs.Read(buf, 0, buf.Length);
                    assembly = Assembly.Load(buf);
                  }
                }
                else
                  assembly = Assembly.LoadFrom(fullPath);
              }
              else
              {
                //TODO: Add the application configuration, such that the mismatched version can be used, especially for WebMathTraining.XllCommon.dll
                //NOTE: if version mismatched, we also return it at this point
                if (Logger.IsWarnEnabled) Logger.WarnFormat("Assembly probing version mismatch: {0} is NOT loaded", fullPath);
                if (Logger.IsWarnEnabled) Logger.WarnFormat("Needed:{0}", args.Name);
                if (Logger.IsWarnEnabled) Logger.WarnFormat("Found: {0}", assemblyQualifiedName);
                //return assembly;
              }
            }
          }
        }

        //When the .net framework is deserializing the XML text to object, it is weird that
        //they can not find any type that is non-primary type also is not in the calling assemblies.
        //Unfortunately, System.Data is one of these 'missing' types.
        if (assembly == null && args.Name == "System.Data")
        {
          Logger.Debug("Missing System.Data assembly is detected.");
          assembly = typeof(System.Data.DataTable).Assembly;
        }

        //We will suppress WebMathTraining.Toolkit and NHibernate serialization assembly [which will be auto-generated if it is not found]
        if (assembly == null &&
            !args.Name.StartsWith("WebMathTraining.Toolkit.XmlSerializers") &&
            !args.Name.StartsWith("NHibernate.XmlSerializers"))
          Logger.DebugFormat("Assembly probing failed: {0} can not be found", args.Name);

        return assembly;
      }
      return null;
    }

    /// <summary>
    /// Indicate the given path will be searched when the AppDomain can NOT find a assembly.
    /// </summary>
    /// <param name="path"></param>
    public static void AddSearchPath(string path)
    {
      foreach (string str in PathList)
      {
        if (String.Compare(str, path, StringComparison.OrdinalIgnoreCase) == 0)
          return;
      }
      PathList.Add(path);
    }

    /// <summary>
    /// Event handler: when the AppDomain loads one assembly. We need to check if
    /// two versions of same assemblies are loaded into the same AppDomain.
    /// </summary>
    /// <param name="sender"> standard event parameters</param>
    /// <param name="args">  </param>
    private static void OnLoadAssembly(object sender, AssemblyLoadEventArgs args)
    {
      Logger.DebugFormat("in AssemblyResolver.OnLoadAssembly ({0})", args.LoadedAssembly.FullName);

      var loadedAssembly = args.LoadedAssembly;
      var assemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var assembly in assemblies)
      {
        if (assembly.GetName().Name == loadedAssembly.GetName().Name && assembly != loadedAssembly)
        {
          //versioning issue founded: with the same name but different version
          //
          if (assembly.GetName().Version != loadedAssembly.GetName().Version)
          {
            Logger.ErrorFormat("Two different versions of assembly '{0}' are loaded: '{1}' from '{2}' - '{3}' from '{4}' ",
              assembly.GetName().Name, assembly.GetName().Version, assembly.CodeBase, loadedAssembly.GetName().Version, loadedAssembly.CodeBase);

            if (OnAssemblyConflictEvent != null)
            {
              var eventargs = new AssemblyConflictEventArgs(loadedAssembly, assembly);
              OnAssemblyConflictEvent(eventargs);
            }
          }
          else
          {
            Logger.ErrorFormat("Assembly '{0}' version '{1}' are loaded from different location: '{2}' - '{3}' ",
              assembly.GetName().Name, assembly.GetName().Version, assembly.CodeBase, loadedAssembly.CodeBase);

            if (OnAssemblyConflictEvent != null)
            {
              var eventargs = new AssemblyConflictEventArgs(loadedAssembly, assembly);
              OnAssemblyConflictEvent(eventargs);
            }
          }
        }
      }
    }

    /// <summary>
    /// Get all loaded assemblies which is referencing the given assembly
    /// </summary>
    /// <param name="assembly">the assembly which is referenced</param>
    /// <returns></returns>
    public static AssemblyName[] GetReferencingAssemblies(Assembly assembly)
    {
      var list = new ArrayList();
      var assemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var ass in assemblies)
      {
        var references = ass.GetReferencedAssemblies();
        foreach (var refer in references)
        {
          if (refer.FullName == assembly.GetName().FullName)
          {
            list.Add(ass.GetName());
            break;
          }
        }
      }

      return (AssemblyName[])list.ToArray(typeof(AssemblyName));
    }

    /// <summary>
    /// Attach the event listener to the current AppDomain, such that we can load the required
    /// assembly which is not at start-up directory and private directory known to .Net
    /// </summary>
    public static void AttachToCurrentDomain()
    {
      var currentDomain = AppDomain.CurrentDomain;
      currentDomain.AssemblyResolve += ResolveEventHandler;
      currentDomain.AssemblyLoad += OnLoadAssembly;
      currentDomain.TypeResolve += OnDomainTypeResolve;
    }

    /// <summary>
    /// Remove all search paths
    /// </summary>
    public static void RemoveAllSearchPath()
    {
      PathList.Clear();
    }

    /// <summary>
    ///   Load the assembly if the assembly has not been loaded into current domain.
    /// </summary>
    ///
    /// <remarks>
    ///   Assembly::LoadFrom will load the assembly even the same version is already loaded
    ///   such that there are two copies of static data members.
    /// </remarks>
    public static Assembly VersionChecking_LoadFrom(string fullPathName)
    {
      //check if the path is HTTP URL
      try
      {
        Path.GetPathRoot(fullPathName);
      }
      catch (ArgumentException e)
      {
        Logger.InfoFormat("Failed to recognize path: {0}: {1}", fullPathName, e.Message);
        return Assembly.LoadFrom(fullPathName);
      }

      //here we can assume that the file is a at local disk or network drive
      AssemblyName assemblyName;
      try
      {
        assemblyName = AssemblyName.GetAssemblyName(fullPathName);
      }
      catch (Exception)
      {
        var asm = Assembly.Load(Path.GetFileName(fullPathName));
        if (asm == null) throw;
        assemblyName = asm.GetName();
      }

      //check if the current domain already load the same version
      var assemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var ass in assemblies)
      {
        var assName = ass.GetName();
        if (String.Compare(assName.FullName, assemblyName.FullName, StringComparison.OrdinalIgnoreCase) == 0)
        {
          Logger.DebugFormat("The same version already been loaded:\r\n loaded from: {0} \r\n loading from: {1}", ass.Location, fullPathName);
          return ass;
        }
      }

      //assembly not found, load it now
      Logger.DebugFormat("First time loading of assembly: {0}", fullPathName);
      return Assembly.LoadFrom(fullPathName);
    }

    /// <summary>
    ///  Load the assembly if the assembly has not been loaded into current domain.
    /// </summary>
    /// 
    /// <remarks>
    ///   Assembly::LoadFrom will load the assembly even the same version is already loaded
    ///   such that there are two copies of static data members.
    /// </remarks>
    public static Assembly VersionChecking_LoadFrom(AssemblyName assemblyName, byte[] assemblyContent)
    {
      //check if the current domain already load the same version
      var assemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var ass in assemblies)
      {
        var assName = ass.GetName();
        if (String.Compare(assName.FullName, assemblyName.FullName, StringComparison.OrdinalIgnoreCase) == 0)
        {
          Logger.DebugFormat("The same version already been loaded:\r\n loaded from: {0} \r\n loading from in-memory", ass.Location);
          return ass;
        }
      }
      //assembly not found, load it now
      Logger.DebugFormat("First time loading of in-memory assembly: {0}", assemblyName);
      return Assembly.Load(assemblyContent);
    }

    private static Assembly OnDomainTypeResolve(object sender, ResolveEventArgs args)
    {
      Logger.DebugFormat("Type '{0}' can not be found.", args.Name);
      return null;
    }
  }
}