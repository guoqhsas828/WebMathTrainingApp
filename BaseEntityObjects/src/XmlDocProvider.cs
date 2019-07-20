// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using log4net;

namespace BaseEntity.Shared
{
  /// <summary>
  /// A component that can provide Xml documentation for members of assemblies
  /// </summary>
  public class XmlDocProvider : IXmlDocProvider
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(XmlDocProvider));
    private readonly Dictionary<Assembly, XmlDocument> _docMap = new Dictionary<Assembly, XmlDocument>();

    /// <summary>
    /// Gets the summary documentation for a member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public XmlText GetSummary(MemberInfo member)
    {
      var assembly = member.Module.Assembly;
      XmlDocument doc;
      if (!_docMap.TryGetValue(assembly, out doc))
      {
        doc = new XmlDocument();
        _docMap[assembly] = doc;
        var location = assembly.Location;
        var directory = Path.GetDirectoryName(location);
        if (directory != null)
        {
          var xmlDocFile = Path.Combine(directory, Path.GetFileNameWithoutExtension(location) + ".xml");
          if (File.Exists(xmlDocFile))
          {
            doc.Load(xmlDocFile);
          }
        }
      }
      string memberName = null;
      switch (member.MemberType)
      {
        case MemberTypes.TypeInfo:
          memberName = string.Format("T:{0}", ((Type)member).FullName);
          break;
        case MemberTypes.NestedType:
          memberName = string.Format("T:{0}", ((Type)member).FullName.Replace("+", "."));
          break;
        case MemberTypes.Property:
          if (member.DeclaringType != null)
          {
            memberName = string.Format("P:{0}.{1}", member.DeclaringType.FullName, member.Name);
          }
          break;
        case MemberTypes.Field:
          if (member.DeclaringType != null)
          {
            memberName = string.Format("F:{0}.{1}", member.DeclaringType.FullName, member.Name);
          }
          break;
        default:
          Log.Debug("");
          break;
      }
      if (memberName != null)
      {
        var xpath = string.Format("/doc/members/member[@name='{0}']/summary/text()", memberName);
        var summaryText = doc.SelectSingleNode(xpath) as XmlText;
        if (summaryText != null)
        {
          return doc.CreateTextNode(summaryText.Value.Trim());
        }
      }
      Log.WarnFormat("No documentation found for member: {0}", memberName);
      return doc.CreateTextNode("No documentation is available.");
    }
  }
}