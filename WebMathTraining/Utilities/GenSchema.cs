//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using BaseEntity.Configuration;
//using BaseEntity.Metadata;
//using BaseEntity.Database;
//using BaseEntity.Database.Engine;

//namespace WebMathTraining.Utilities
//{
//  public class GenSchema
//  {
//    public static int GenerateSchema(string[] args)
//    {
//      Configurator.InitPhaseOne();

//      var parser = new CmdLineParser();
//      parser.AddStringOption("-o", "outFile", null, "Output schema to specified file.");
//      parser.AddBooleanOption("-u", "update", false, "Generate schema update DDL instead of schema create DDL.");
//      parser.AddBooleanOption("-e", "exec", false, "Execute generated DDL against database.");
//      parser.AddStringOption("-p", "pluginAssemblies", null, "Comma separated list of plugin assemblies.");
//      parser.AddStringOption("--qConnectString", "ConnectString", "Database connection string");
//      if (!parser.ParseArgs(args))
//      {
//        return 1;
//      }

//      var exec = (bool)parser.GetValue("exec");
//      var outFile = (string)parser.GetValue("outFile");
//      var update = (bool)parser.GetValue("update");

//      var pluginAssembliesArg = (string)parser.GetValue("pluginAssemblies");
//      string[] pluginAssemblyNames = pluginAssembliesArg == null ? null : pluginAssembliesArg.Split(',');

//      Configurator.InitPhaseTwo(null, "GenSchema", new PluginLoader(update, pluginAssemblyNames));

//      var gen = new SchemaGenerator();

//      if (update)
//      {
//        if (!gen.Update(outFile, exec, pluginAssemblyNames))
//        {
//          return 2;
//        }
//      }
//      else
//      {
//        if (!gen.Create(outFile, exec, pluginAssemblyNames))
//        {
//          return 2;
//        }
//        if (exec)
//          return InitDatabase();
//      }

//      return 0;
//    }

//    public static int InitDatabase()
//    {
//      var addedUser =  DatabaseUtil.AddUser();
//      var assembly = Assembly.GetExecutingAssembly();

//      var plugin = new PluginAssembly()
//      {
//        Name = assembly.GetName().Name,
//        Description = assembly.GetName().Name,
//        FileName = assembly.ManifestModule.Name,
//        Enabled = true,
//        LastUpdated = DateTime.Now,
//        UpdatedBy = EntityContextFactory.User,
//        PluginType = PluginType.EntityModel,
//        ObjectVersion = 1
//      };

//      DatabaseUtil.SaveObject(plugin);
//      return addedUser;
//    }

//    private class PluginLoader : IPluginLoader
//    {
//      private readonly List<string> _assemblyNames;

//      public PluginLoader(bool update, IEnumerable<string> assemblyNames = null)
//      {
//        _assemblyNames = new List<string>(new[] {  "BaseEntity.Metadata.dll", "BaseEntity.Database.dll" , "WebMathTraining.dll"}); 

//        if (update)
//        {
//          _assemblyNames.AddRange(SchemaGenerator.GetPluginAssemblyNames());
//        }

//        if (assemblyNames != null)
//        {
//          _assemblyNames.AddRange(assemblyNames);
//        }

//        for (int i = 0; i < _assemblyNames.Count; ++i)
//        {
//          var assemblyName = _assemblyNames[i];
//          if (!assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
//            _assemblyNames[i] = assemblyName + ".dll";
//        }
//      }

//      /// <summary>
//      /// Load any enabled assemblies from the PluginAssembly table
//      /// </summary>
//      /// <returns></returns>
//      public IEnumerable<PluginItem> Load()
//      {
//        return _assemblyNames.Select(
//          assemblyName => new PluginItem(assemblyName, PluginType.EntityModel));
//      }
//    }
//  }
//}
