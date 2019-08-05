

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Tests.Helpers.Legacy
{
  internal static class FixtureBuilder
  {
    private static readonly Dictionary<Assembly, IDictionary<string, IDictionary<string, string>>> Data
      = new Dictionary<Assembly, IDictionary<string, IDictionary<string, string>>>();

    public static IDictionary<string, string> Get(object fixture, string name)
    {
      if (fixture == null) return null;

      var type = fixture.GetType();
      var data = LoadData(type.Assembly);
      var key = fixture.GetType().Name;
      if (name != null) key += '\n' + name;
      return data.TryGetValue(key, out var map) ? map : null;
    }

    public static IEnumerable<IDictionary<string, string>> GetAllFixtures(
      Assembly assembly)
    {
      return LoadData(assembly)?.Values;
    }

    public static void SetProperties(object fixture,
      IDictionary<string, string> fixtureProperties)
    {
      if (fixture == null || fixtureProperties == null)
        return;

      var type = fixture.GetType();

      // Set property values
      var properties = type.GetProperties(BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.FlattenHierarchy);
      foreach (var pi in properties)
      {
        if (!fixtureProperties.TryGetValue(pi.Name, out var str))
          continue;
        var value = Parsers.Parse(pi.PropertyType, str);
        pi.SetValue(fixture, value);
      }
    }

    public static ISet<string> GetIgnoredMethods(
      object fixture,
      IDictionary<string, string> fixtureProperties)
    {
      if (fixture == null || fixtureProperties == null)
        return null;

      var allMethods = GetAllTestMethods(fixture.GetType());
      var excludedMethods = new HashSet<string>();

      string names;
      if (fixtureProperties.TryGetValue("excludeMethods", out names))
      {
        if (names == "all")
        {
          AddRange(excludedMethods, allMethods.Keys);
        }
        else if (!string.IsNullOrEmpty(names))
        {
          AddRange(excludedMethods, names.Split(','));
        }
      }

      if (fixtureProperties.TryGetValue("excludeGroups", out names))
      {
        if (names == "all")
        {
          AddRange(excludedMethods, allMethods.Keys);
        }
        else if (!string.IsNullOrEmpty(names))
        {
          var list = names.Split('.');
          AddRange(excludedMethods, allMethods
            .Where(p=> list.Select(s => p.Value.Contains(s)).Any())
            .Select(p=>p.Key));
        }
      }

      if (fixtureProperties.TryGetValue("selectGroups", out names))
      {
        if (!string.IsNullOrEmpty(names))
        {
          var list = names.Split('.');
          RemoveRange(excludedMethods, allMethods
            .Where(p => list.Select(s => p.Value.Contains(s)).Any())
            .Select(p => p.Key));
        }
      }

      if (fixtureProperties.TryGetValue("selectMethods", out names))
      {
        if (!string.IsNullOrEmpty(names))
        {
          RemoveRange(excludedMethods, names.Split(','));
        }
      }

      return excludedMethods;
    }

    private static IDictionary<string, ISet<string>> GetAllTestMethods(
      Type type)
    {
      var results = new Dictionary<string, ISet<string>>();
      var methods = type.GetMethods(
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.FlattenHierarchy);
      foreach (var info in methods)
      {
        var attribues = info.GetCustomAttributes(true);
        if (!attribues.OfType<NUnit.Framework.TestAttribute>().Any())
          continue;
        var set = new HashSet<string>();
        foreach (var category in attribues.OfType<NUnit.Framework.CategoryAttribute>())
        {
          set.Add(category.Name);
        }
        results.Add(info.Name, set);
      }

      return results;
    }

    private static void AddRange<T>(ISet<T> set, IEnumerable<T> range)
    {
      foreach (var item in range)
      {
        set.Add(item);
      }
    }

    private static void RemoveRange<T>(ISet<T> set, IEnumerable<T> range)
    {
      foreach (var item in range)
      {
        if(set.Contains(item)) set.Remove(item);
      }
    }

    private static IDictionary<string, IDictionary<string, string>> LoadData(
      Assembly assembly)
    {
      if (Data.TryGetValue(assembly, out var data)) return data;

      data = new Dictionary<string, IDictionary<string, string>>();
      foreach (var xmlNode in LoadParameters(assembly))
      {
        var map = new Dictionary<string, string>();
        foreach (XmlAttribute attribute in xmlNode.Attributes)
        {
          var key = attribute.Name;
          if (map.ContainsKey(key))
          {
            throw new ToolkitException(
              $"Duplicate key '{key}' in {xmlNode.OuterXml}");
          }
          map.Add(attribute.Name, attribute.Value);
        }

        if (!map.TryGetValue("class", out var clasName))
        {
          throw new ToolkitException(
            "Missing class name on XML fixture:\n" + xmlNode.OuterXml);
        }

        if (map.TryGetValue("name", out var name))
        {
          name = clasName + '\n' + name;
        }
        else
        {
          name = clasName;
        }
        if (data.ContainsKey(name))
        {
          throw new ToolkitException(
            $"Duplicate key '{name}' in {xmlNode.OuterXml}");
        }
        data.Add(name, map);
      }

      return data;
    }

    private static IEnumerable<XmlNode> LoadParameters(Assembly assembly)
    {
      const string testParamFileext = ".test";
      foreach (string name in assembly.GetManifestResourceNames())
      {
        if (!name.EndsWith(testParamFileext)) continue;

        var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) continue;

        XmlTextReader reader = new XmlTextReader(stream);
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(reader);
        XmlNodeList fixtureNodes = xmlDoc.GetElementsByTagName("fixture");
        foreach (XmlNode node in fixtureNodes)
          yield return node;
      }
    } // LoadParameters(Type type)

  }
}