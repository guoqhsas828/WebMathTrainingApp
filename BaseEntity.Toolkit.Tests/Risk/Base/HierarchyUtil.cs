using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Core.Logging;
using BaseEntity.Database;
using BaseEntity.Metadata;
using log4net;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Hierarchy Util class which provides a number of extension methods for HierarchyElement and HierarchySchema
  /// </summary>
  public static class HierarchyUtil
  {
    private static readonly ILog Logger = QLogManager.GetLogger(typeof(HierarchyUtil));

    private const string Delimiter = "\\";

    private static readonly ConcurrentDictionary<string, HierarchySchema> HierarchySchemaCache
      = new ConcurrentDictionary<string, HierarchySchema>();

    private static readonly ConcurrentDictionary<HierarchySchema, ConcurrentBag<HierarchyElement>> HierarchyElementCache
      = new ConcurrentDictionary<HierarchySchema, ConcurrentBag<HierarchyElement>>();

    /// <summary>
    /// Checks if a query element is an decedent of a 
    /// </summary>
    /// <param name="possibleDescendant">the query element</param>
    /// <param name="possibleAncestor">the possible ancestor element</param>
    /// <returns>of the query element is a decedent of the possible ancestor element</returns>
    public static bool IsDescendant(this HierarchyElement possibleDescendant, HierarchyElement possibleAncestor)
    {
      if (possibleDescendant == null)
      {
        return false;
      }
      return possibleDescendant.Equals(possibleAncestor) || IsDescendant(possibleDescendant.Parent, possibleAncestor);
    }

    /// <summary>
    /// Load a Hierarchy Schema with a specific name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static HierarchySchema GetHierarchySchema(string name)
    {
      var hierarchySchema = HierarchySchemaCache.GetOrAdd(name, _ => null);
      if (hierarchySchema != null)
      {
        return hierarchySchema;
      }
      try
      {
        hierarchySchema = Session.Linq<HierarchySchema>().SingleOrDefault(s => s.Name == name);
        if (hierarchySchema == null)
        {
          return null;
        }
        hierarchySchema.ResolveAll();
        HierarchySchemaCache.TryAdd(name, hierarchySchema);
        return hierarchySchema;
      }
      catch (Exception e)
      {
        Logger.WarnFormat("Session.Find method failure due to {0}", e.Message);
      }
      return hierarchySchema;
    }

    /// <summary>
    /// Load a Hierarchy Element with a specific name, level, schema, and parent.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="level"></param>
    /// <param name="hierarchySchema"></param>
    /// <param name="parent"></param>
    /// <returns></returns>
    public static HierarchyElement GetHierarchyElement(string name, int level, HierarchySchema hierarchySchema, HierarchyElement parent)
    {
      var hierarchyElements = GetHierarchyElements(hierarchySchema);
      if (parent == null)
      {
        return hierarchyElements?.FirstOrDefault(h => h.Name == name && (int)h.Level == level);
      }
      else
      {
        return hierarchyElements?.FirstOrDefault(h => h.Name == name && (int)h.Level == level && h.Parent == parent);
      }
    }

    /// <summary>
    /// Load all the Hierarchy Elements for a specific schema, valid from the supplied AsOf date.
    /// </summary>
    /// <param name="hierarchySchema"></param>
    /// <returns></returns>
    public static IEnumerable<HierarchyElement> GetHierarchyElements(HierarchySchema hierarchySchema)
    {
      var hierarchyElements = HierarchyElementCache.GetOrAdd(hierarchySchema, _ => new ConcurrentBag<HierarchyElement>())?.ToList();
      if (hierarchyElements?.Count != 0)
      {
        return hierarchyElements;
      }
      var context = new NHibernateEntityContext(ReadWriteMode.ReadOnly);
      using (new EntityContextBinder(context, true))
      {
        try
        {
          var list = Session.Linq<HierarchyElement>().Where(e => e.HierarchySchema == hierarchySchema).ToList();
          if (list.Count == 0)
          {
            return null;
          }
          foreach (HierarchyElement element in list)
          {
            if (element == null)
            {
              continue;
            }
            element.ResolveAll();
            HierarchyElementCache[hierarchySchema].Add(element);
          }
          return HierarchyElementCache[hierarchySchema].ToList();
        }
        catch (Exception e)
        {
          Logger.WarnFormat("Session.Find method failure due to {0}", e.Message);
          return null;
        }
      }
    }

    /// <summary>
    /// Return a set of column labels formulated for each Hierarchy Schema
    /// </summary>
    public static IDictionary<string, HashSet<string>> HierarchySchemas
    {
      get
      {
        var hierarchySchemas = new Dictionary<string, HashSet<string>>();
        var context = new NHibernateEntityContext(ReadWriteMode.ReadOnly);
        using (new EntityContextBinder(context, true))
        {
          try
          {
            var list = Session.Find("FROM HierarchySchema");
            if (list.Count == 0)
            {
              return null;
            }
            foreach (HierarchySchema hierarchySchema in list)
            {
              hierarchySchema.ResolveAll();
              hierarchySchemas.Add(hierarchySchema.Name, new HashSet<string>(GetHierarchySchemaLevelNames(hierarchySchema)));
            }
          }
          catch (Exception e)
          {
            Logger.WarnFormat("Session.Find method failure due to {0}", e.Message);
          }
        }
        return hierarchySchemas;
      }
    }

    /// <summary>
    /// Gets list of Hierarchy Schema level names
    /// </summary>
    /// <param name="hierarchySchema"></param>
    /// <returns></returns>
    public static List<string> GetHierarchySchemaLevelNames(HierarchySchema hierarchySchema)
    {
      var names = new List<string> { hierarchySchema.Level1 };

      if (hierarchySchema.Level2 != null)
      {
        names.Add(hierarchySchema.Level2);
      }
      if (hierarchySchema.Level3 != null)
      {
        names.Add(hierarchySchema.Level3);
      }
      if (hierarchySchema.Level4 != null)
      {
        names.Add(hierarchySchema.Level4);
      }
      if (hierarchySchema.Level5 != null)
      {
        names.Add(hierarchySchema.Level5);
      }
      if (hierarchySchema.Level6 != null)
      {
        names.Add(hierarchySchema.Level6);
      }
      return names;
    }


    /// <summary>
    /// Returns the Hierarchy Element which exists within a Hierarchy tree at a specific Level.
    /// </summary>
    /// <param name="hierarchyElement"></param>
    /// <param name="level"></param>
    /// <returns></returns>
    public static HierarchyElement HierarchyElementAtLevel(this HierarchyElement hierarchyElement, int level)
    {
      if ((int)hierarchyElement.Level < level || level < 1)
      {
        Logger.WarnFormat("HierarchyElementAtLevel requires a level parameter: {0}, which is less than the hierarchical level {1}.", level, hierarchyElement.Level);
        return null;
      }
      var next = hierarchyElement;
      for (var i = (int)hierarchyElement.Level; i >= level; --i)
      {
        if (next == null)
        {
          Logger.WarnFormat("The HierarchyElement {0} hierarchical structure appears to have an invalid structure.", hierarchyElement.Name);
          continue;
        }
        if ((int)next.Level == level)
        {
          return next;
        }
        next = next.Parent;
      }
      return null;
    }

    /// <summary>
    /// Returns the name of a Hierarchy Element which exists within a Hierarchy tree at Level Name.
    /// </summary>
    /// <param name="hierarchyElement"></param>
    /// <param name="levelName"></param>
    /// <returns></returns>
    public static string HierarchyNameAtLevel(this HierarchyElement hierarchyElement, string levelName)
    {
      var schema = hierarchyElement.HierarchySchema;
      if (!schema.IsLevel(levelName))
      {
        Logger.WarnFormat("HierarchyNameAtLevel requires a level name in this case {0} for which HierarchyElement {1} or one of its ancestors belongs.", levelName, hierarchyElement.Level);
        return null;
      }
      var level = schema.Level(levelName);
      return hierarchyElement.HierarchyElementAtLevel(level)?.Name;
    }

    /// <summary>
    /// Check is a query name exists within a level within a Hierarchy
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="levelName"></param>
    /// <returns></returns>
    public static bool IsLevel(this HierarchySchema schema, string levelName)
    {
      return schema.Level1 == levelName ||
             schema.Level2 == levelName ||
             schema.Level3 == levelName ||
             schema.Level4 == levelName ||
             schema.Level5 == levelName ||
             schema.Level6 == levelName;
    }

    /// <summary>
    /// Checks the number of levels valid for a schema
    /// </summary>
    /// <param name="schema"></param>
    /// <returns></returns>
    public static int NumberOfLevels(this HierarchySchema schema)
    {
      if (schema.Level6 != null)
      {
        return 6;
      }
      if (schema.Level5 != null)
      {
        return 5;
      }
      if (schema.Level4 != null)
      {
        return 4;
      }
      if (schema.Level3 != null)
      {
        return 3;
      }
      if (schema.Level2 != null)
      {
        return 2;
      }
      return 1;
    }

    /// <summary>
    /// Returns the level that a level name exists within a HierarchySchema
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="levelName"></param>
    /// <returns></returns>
    public static int Level(this HierarchySchema schema, string levelName)
    {
      if (schema.Level1 == levelName)
      {
        return 1;
      }
      if (schema.Level2 == levelName)
      {
        return 2;
      }
      if (schema.Level3 == levelName)
      {
        return 3;
      }
      if (schema.Level4 == levelName)
      {
        return 4;
      }
      if (schema.Level5 == levelName)
      {
        return 5;
      }
      if (schema.Level6 == levelName)
      {
        return 6;
      }
      throw new RiskException("Hierarchies in Risk currently only support five tiers.");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parent"></param>
    /// <returns></returns>
    public static List<HierarchyElement> GetAllChildren(HierarchyElement parent)
    {
      if (parent == null)
        return new List<HierarchyElement>();
      var children = Session.Linq<HierarchyElement>().Where(el => el.Parent == parent).ToList();
      var childrensChildren = new List<HierarchyElement>();
      foreach (var child in children)
      {
        childrensChildren.AddRange(GetAllChildren(child));
      }
      children.AddRange(childrensChildren);
      return children;
    }


    #region xPath methods

    /// <summary>
    /// Generates an xPath representation of the Hierarchy Element
    /// </summary>
    /// <returns></returns>
    public static string XPath(this HierarchyElement hierarchyElement, bool includeSchemName)
    {
      var stack = new Stack<string>();
      stack.Push(hierarchyElement.Name);
      var next = hierarchyElement.Parent;
      while (next != null)
      {
        stack.Push(next.Name);
        next = next.Parent;
      }
      if (includeSchemName)
        stack.Push(hierarchyElement.HierarchySchema.Name);

      var stringBuilder = new StringBuilder();
      stringBuilder.Append(stack.Pop());
      while (stack.Any())
      {
        stringBuilder.Append(Delimiter);
        stringBuilder.Append(stack.Pop());
      }
      return stringBuilder.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="xPath"></param>
    /// <param name="elements"></param>
    /// <returns></returns>
    public static bool TryParseXPath(string xPath, out IList<HierarchyElement> elements)
    {
      elements = new List<HierarchyElement>();
      if (string.IsNullOrEmpty(xPath))
      {
        return false;
      }
      var components = xPath.Split(new[] { Delimiter }, StringSplitOptions.RemoveEmptyEntries);
      if (!components.Any())
      {
        return false;
      }

      string schemaName = components[0];
      // Doing a query again to handle changes to Hierarchy Schema
      HierarchySchema schema = GetHierarchySchema(schemaName);

      HierarchyElement parent = null;
      var level = 0;
      foreach (var component in components)
      {
        if (level != 0)
        {
          var element = GetHierarchyElement(component, level, schema, parent);
          if (element == null)
          {
            return false;
          }
          elements.Add(element);
          parent = element;
        }
        level++;
      }
      return true;
    }

    #endregion
  }
}
