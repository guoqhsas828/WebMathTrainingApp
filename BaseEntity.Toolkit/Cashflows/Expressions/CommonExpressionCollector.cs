/*
 *  -2015. All rights reserved.
 */


using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  class CommonExpressionCollector
  {
    private readonly Dictionary<Evaluable, object> _counters
      = new Dictionary<Evaluable, object>();

    internal IEnumerable<Evaluable> GetCommonExpressions()
    {
      return _counters.Where(p => p.Value is IList<object>).Select(p => p.Key);
    }

    internal void Process(IEnumerable<Evaluable> node)
    {
      VisitChildren(node);
    }

    internal void Process(Evaluable evaluable)
    {
      VisitChildren(evaluable as IEnumerable<Evaluable>);
    }

    private void AddParent(Evaluable node, object parent)
    {
      if (node == null) return;

      object data;
      if (_counters.TryGetValue(node, out data))
      {
        var parents = data as List<object>;
        if (parents == null)
        {
          if (!ReferenceEquals(data, parent))
          {
            _counters[node] = new List<object>{data, parent};
          }
          return;
        }
        if (!parents.Contains(parent)) parents.Add(parent);
        return;
      }

      _counters.Add(node, parent);
      VisitChildren(node as IEnumerable<Evaluable>);
    }

    private void VisitChildren(IEnumerable<Evaluable> node)
    {
      if (node == null) return;
      foreach (var child in node)
      {
        AddParent(child, node);
      }
    }

  }
}
