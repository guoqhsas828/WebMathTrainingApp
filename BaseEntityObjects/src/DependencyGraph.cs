using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace BaseEntity.Shared
{
  /// <summary>
  ///  Represent a DAG (directed acyclic graph).
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <remarks>
  ///   This class implements a read-only <c>IList&lt;T&gt;</c> interface such
  ///   that each element appears only once in the sequence and the parents
  ///   always appear before any of their children.
  /// </remarks>
  public class DependencyGraph<T> : IList<T>
  {
    #region Data

    private readonly T[] all_;     // flat array representation, useful for enumeration.
    private readonly Node[] root_; // DAG representation, useful for parallel tasks.

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyGraph&lt;T&gt;"/> class.
    /// </summary>
    /// <param name="items">The list of initial elements.</param>
    /// <param name="getParents">The delegate to get parent elements.</param>
    /// <remarks></remarks>
    public DependencyGraph(IEnumerable<T> items,
      Func<T, IEnumerable<T>> getParents)
    {
      // We use a dictionary to keep the mapping of element to it's children.
      var dict = new Dictionary<T, Node>();
      foreach (var t in items)
      {
        Node node;
        if (dict.TryGetValue(t, out node)) continue;
        node = new Node(t);
        dict.Add(t, node);
        BuildGraph(dict, node, getParents);
      }
      // root contains the elements which depend on nothing.
      var root = root_ = dict.Values.Where(n => n.ParentCount == 0).ToArray();
      if (root.Length == 0 && dict.Count > 0)
      {
        throw new ArgumentException("Cyclic dependency detected");
      }
#if DEBUG
      // Let's do some further check in debug mode.
      VerifyNoCycles(dict.Keys, getParents);
#endif

      // Build a flat array of all elements
      var list = new List<T>();
      for (int i = 0, n = root.Length; i < n; ++i)
        DoNode(root[i], list.Add);
      all_ = list.ToArray();

      // Set all the counters to 0.
      ResetCount(root);
    }

    private static void BuildGraph(
      IDictionary<T, Node> dict, Node current,
      Func<T, IEnumerable<T>> getParents)
    {
      foreach (var p in getParents(current.Self))
      {
        Node node;
        if (!dict.TryGetValue(p, out node))
        {
          node = new Node(p);
          dict.Add(p, node);
          BuildGraph(dict, node, getParents);
        }
        if (!node.Children.Contains(current))
        {
          node.Children.Add(current);
          ++current.ParentCount;
        }
      }
    }

    [Conditional("DEBUG")]
    private static void VerifyNoCycles(IEnumerable<T> items,
      Func<T, IEnumerable<T>> getParents)
    {
      if(items.HasCyclicDependency(getParents))
        throw new ArgumentException("Cyclic dependency detected");
    }
    #endregion Constructor

    #region Methods

    /// <summary>
    ///   Return a sequence in the order from the children to parents.
    /// </summary>
    /// <returns>A reverse ordered sequence.</returns>
    public IEnumerable<T> ReverseOrdered()
    {
      for (int i = all_.Length; --i >= 0; )
      {
        yield return all_[i];
      }
    }

    /// <summary>
    ///  Walk through dependency graph and perform an action for each element.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <remarks>
    ///   <para>>The action is called exactly once for each element and the call occurs
    ///   only when all the parents of the element complete the action.</para>
    ///  <para>>This function is essentially equivalent to the following codes:</para>
    ///  <code>
    ///    foreach(var element in graph) action(element);
    ///  </code>
    /// </remarks>
    public void ForEach(Action<T> action)
    {
      var all = all_;
      if (all == null) return;
      for (int i = 0, n = all.Length; i < n; ++i)
        action(all[i]);
    }

    /// <summary>
    ///  Walk through dependency graph and perform an action for each element.
    /// </summary>
    /// <param name="action">The action</param>
    /// <remarks>>This function makes sure that the parents always run before the children
    ///  while it tries to run all the works with the same order of dependency in parallel.</remarks>
    public void ParallelForEach(Action<T> action)
    {
      var root = root_;
      if (root == null || root_.Length == 0) return;
      ResetCount(root);
      using (var doneEvent = new ManualResetEvent(false))
      {
        var state = new State
          {
            RemainingCount = all_.Length,
            DoneEvent = doneEvent
          };
        for (int i = 0, n = root.Length; i < n; ++i)
        {
          ParallelDoNode(root[i], action, state);
        }
        doneEvent.WaitOne();
      }
    }

    /// <summary>
    ///   Start tasks in parallel based on dependency order.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="action"></param>
    /// <param name="state"></param>
    private static void ParallelDoNode(Node data, Action<T> action, State state)
    {
      ThreadPool.UnsafeQueueUserWorkItem(obj =>
        {
          var node = obj as Node;
          if (node == null) return;
          action(node.Self);
          if (Interlocked.Decrement(ref state.RemainingCount) <= 0)
          {
            state.DoneEvent.Set();
            return;
          }
          var list = node.Children;
          if (list == null) return;
          for (int i = 0, n = list.Count; i < n; ++i)
          {
            node = list[i];
            // If all of its parents finished their tasks...
            if (Interlocked.Increment(ref node.Count) >= node.ParentCount)
              ParallelDoNode(node, action, state);
          }
        }, data);
    }

    /// <summary>
    ///   Performs an action for the element and each of its children based on the dependency order.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="action"></param>
    private static void DoNode(Node node, Action<T> action)
    {
      action(node.Self);
      var list = node.Children;
      if (list == null) return;
      for (int i = 0, n = list.Count; i < n; ++i)
      {
        node = list[i];
        // If all parents finished their tasks, start this node.
        if (++node.Count >= node.ParentCount)
          DoNode(node, action);
      }
    }

    /// <summary>
    ///   Recursively sets all counters to zero
    /// </summary>
    /// <param name="nodes"></param>
    private static void ResetCount(IList<Node> nodes)
    {
      if (nodes == null) return;
      for (int i = 0, n = nodes.Count; i < n; ++i)
      {
        var node = nodes[i];
        node.Count = 0;
        ResetCount(node.Children);
      }
    }

    #endregion Methods

    #region IList<T> Members

    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns>IEnumerator{`0}.</returns>
    public IEnumerator<T> GetEnumerator()
    {
      return (all_ as IEnumerable<T>).GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return all_.GetEnumerator();
    }

    /// <summary>
    /// Indexes the of.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>System.Int32.</returns>
    public int IndexOf(T item)
    {
      return Array.IndexOf(all_, item);
    }

    /// <summary>
    /// Inserts the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="item">The item.</param>
    /// <exception cref="System.InvalidOperationException">List is readonly</exception>
    public void Insert(int index, T item)
    {
      throw new InvalidOperationException("List is readonly");
    }

    /// <summary>
    /// Removes at.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <exception cref="System.InvalidOperationException">List is readonly</exception>
    public void RemoveAt(int index)
    {
      throw new InvalidOperationException("List is readonly");
    }

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>`0.</returns>
    /// <exception cref="System.InvalidOperationException">List is readonly</exception>
    public T this[int index]
    {
      get { return all_[index]; }
      set { throw new InvalidOperationException("List is readonly"); }
    }

    /// <summary>
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <exception cref="System.InvalidOperationException">List is readonly</exception>
    public void Add(T item)
    {
      throw new InvalidOperationException("List is readonly");
    }

    /// <summary>
    /// Clears this instance.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">List is readonly</exception>
    public void Clear()
    {
      throw new InvalidOperationException("List is readonly");
    }

    /// <summary>
    /// Determines whether [contains] [the specified item].
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c> if [contains] [the specified item]; otherwise, <c>false</c>.</returns>
    public bool Contains(T item)
    {
      return all_.Contains(item);
    }

    /// <summary>
    /// Copies to.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="arrayIndex">Index of the array.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
      all_.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <value>The count.</value>
    /// <returns>
    /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
    ///   </returns>
    public int Count
    {
      get { return all_.Length; }
    }

    /// <summary>
    /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
    /// </summary>
    /// <value><c>true</c> if this instance is read only; otherwise, <c>false</c>.</value>
    /// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.
    ///   </returns>
    public bool IsReadOnly
    {
      get { return true; }
    }

    /// <summary>
    /// Removes the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
    /// <exception cref="System.InvalidOperationException">List is readonly</exception>
    public bool Remove(T item)
    {
      throw new InvalidOperationException("List is readonly");
    }

    #endregion

    #region Nested type: Node

    private class Node
    {
      public readonly IList<Node> Children;
      public readonly T Self;
      public int ParentCount;
      [Mutable] internal int Count;

      public Node(T t)
      {
        Self = t;
        Children = new List<Node>();
        ParentCount = Count = 0;
      }
    }

    private class State
    {
      public int RemainingCount;
      public ManualResetEvent DoneEvent;
    }
    #endregion
  }

  #region Extension methods
  /// <summary>
  ///  Extension methods for Dependency Graphs
  /// </summary>
  /// <remarks></remarks>
  public static class DependencyGraph
  {
    /// <summary>
    ///  Build the dependency graph for a list of items.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items">The items.</param>
    /// <param name="getParents">The get parents.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static DependencyGraph<T> ToDependencyGraph<T>(
      this IEnumerable<T> items,
      Func<T, IEnumerable<T>> getParents)
    {
      return new DependencyGraph<T>(items, getParents);
    }

    /// <summary>
    ///  Get in the population all the descendants of the specified items.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items">The items.</param>
    /// <param name="population">The population</param>
    /// <param name="getParents">The delegate to get parents.  This may be called several times for each element.</param>
    /// <returns>A list of the specified items and all their descendents, in the dependency order.</returns>
    /// <remarks></remarks>
    public static IList<T> GetDescendants<T>(
      this IEnumerable<T> items,
      IEnumerable<T> population,
      Func<T, IEnumerable<T>> getParents)
    {
      if (items == null) return null;
      var parents = new HashSet<T>(items);
      if (population == null)
        return parents.ToDependencyGraph(getParents);

      var list = new List<T>();
      parents.Union(population).ToDependencyGraph(getParents).ForEach(item =>
      {
        if (parents.Contains(item))
        {
          list.Add(item);
          return;
        }
        foreach (var parent in getParents(item))
        {
          if (!parents.Contains(parent)) continue;
          parents.Add(item);
          list.Add(item);
          return;
        }
      });
      return list;
    }

    /// <summary>
    ///  Check cyclic dependency for a list of items.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items"></param>
    /// <param name="getParents"></param>
    /// <returns></returns>
    public static bool HasCyclicDependency<T>(
      this IEnumerable<T> items,
      Func<T, IEnumerable<T>> getParents)
    {
      return CreateTopologicalSort(items, getParents) == null;
    }

    private static List<T> CreateTopologicalSort<T>(
      IEnumerable<T> items,
      Func<T, IEnumerable<T>> getParents)
    {
      // Build up the dependencies graph
      var dependenciesToFrom = new Dictionary<T, List<T>>();
      var dependenciesFromTo = new Dictionary<T, List<T>>();
      foreach (var item in items)
      {
        // Note that op depends on each of its parents
        var parents = new List<T>();
        if(dependenciesToFrom.ContainsKey(item)) continue;
        dependenciesToFrom.Add(item, parents);

        // Note that each of op.Dependencies is relied on by op.Id
        foreach (var parent in getParents(item))
        {
          parents.Add(parent);

          List<T> children;
          if (!dependenciesFromTo.TryGetValue(parent, out children))
          {
            children = new List<T>();
            dependenciesFromTo.Add(parent, children);
          }
          children.Add(item);
        }
      }

      // Create the sorted list
      var overallPartialOrderingItems = new List<T>(dependenciesToFrom.Count);
      var thisIterationItems = new List<T>(dependenciesToFrom.Count);
      while (dependenciesToFrom.Count > 0)
      {
        thisIterationItems.Clear();
        foreach (var item in dependenciesToFrom)
        {
          // If an item has zero input operations, remove it.
          if (item.Value.Count == 0)
          {
            thisIterationItems.Add(item.Key);

            // Remove all outbound edges
            List<T> children;
            if (dependenciesFromTo.TryGetValue(item.Key, out children))
            {
              foreach (var depId in children)
              {
                dependenciesToFrom[depId].Remove(item.Key);
              }
            }
          }
        }

        // If nothing was found to remove, there's no valid sort.
        if (thisIterationItems.Count == 0) return null;

        // Remove the found items from the dictionary and 
        // add them to the overall ordering
        foreach (var id in thisIterationItems) dependenciesToFrom.Remove(id);
        overallPartialOrderingItems.AddRange(thisIterationItems);
      }

      return overallPartialOrderingItems;
    }
  }
  #endregion
}
