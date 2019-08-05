using System;
using System.IO;
using System.Xml;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  internal static class XmlUtil
  {
    internal static string GetElementAsText(XmlNode node, string elemName,
      string defaultValue)
    {
      XmlNode n = node.SelectSingleNode(elemName);
      return n == null ? defaultValue : n.InnerText.Trim();
    }

    internal static string GetElementAsTenor(XmlNode node, string elemName,
      string defaultValue)
    {
      XmlNode n = node.SelectSingleNode(elemName);
      return n == null ? defaultValue : n.InnerText.Trim().ToUpper();
    }

    internal static int GetElementAsInt(XmlNode node, string elemName,
      int defaultValue)
    {
      XmlNode n = node.SelectSingleNode(elemName);
      return n == null ? defaultValue : Int32.Parse(n.InnerText.Trim());
    }

    internal static int GetElementAsDate(XmlNode node, string elemName,
      int defaultValue)
    {
      XmlNode n = node.SelectSingleNode(elemName);
      if (n == null) return defaultValue;
      string[] parts = n.InnerText.Trim().Split('-');
      return Int32.Parse(parts[0]) * 10000 + Int32.Parse(parts[1]) * 100 + Int32.Parse(parts[2]);
    }

    internal static double GetElementAsDouble(XmlNode node, string elemName,
      double defaultValue)
    {
      XmlNode n = node.SelectSingleNode(elemName);
      return n == null ? defaultValue : Double.Parse(n.InnerText.Trim());
    }

    internal static XmlDocument LoadDocument(Stream stream)
    {
      XmlTextReader reader = new XmlTextReader(stream);
      XmlDocument doc = new XmlDocument();
      doc.Load(reader);
      return doc;
    }

  }
}
