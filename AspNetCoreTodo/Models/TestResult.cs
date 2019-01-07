using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProtoBuf;

namespace WebMathTraining.Models
{

  public class TestResult
  {
    //[Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long TestSessionId { get; set; }

    public long UserId { get; set; }

    public double FinalScore { get; set; }

    public double MaximumScore { get; set; }

    public double Percentile { get; set; }

    public DateTime TestStarted { get; set; }

    public DateTime TestEnded { get; set; }

    public byte[] TestResultData { get; set; }

    [NotMapped]
    public TestItemList TestResults
    {
      get
      {
        if (_testResults == null)
        {
          if (TestResultData == null)
            _testResults = new TestItemList();
          else
            _testResults = TestItemList.Deserialize(TestResultData);
        }
        return _testResults;
      }
      set
      {
        _testResults = value;
        if (_testResults == null || _testResults.Count == 0)
          TestResultData = null;
        else
        {
          TestResultData = TestItemList.Serialize(_testResults);
        }
      }
    }

    [NotMapped]
    private TestItemList _testResults;
  }

  [Serializable]
  [ProtoContract]
  [DataContract]
  public class TestItemList
  {
    private readonly List<TestResultItem> _items = new List<TestResultItem>();

    /// <summary>
    /// Gets the items.
    /// </summary>
    /// <value>The items.</value>
    [ProtoMember(1)]
    [DataMember(Order = 1)]
    public List<TestResultItem> Items
    {
      get { return _items; }
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count
    {
      get { return Items.Count; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rr"></param>
    public void Add(TestResultItem item)
    {
      Items.Add(item);
    }

    /// <summary>
    /// 
    /// </summary>
    public void Clear()
    {
      Items.Clear();
    }

    /// <summary>
    /// Deserializes the specified bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns></returns>
    public static TestItemList Deserialize(byte[] bytes)
    {
      using (var stream = new MemoryStream(bytes))
      {
        return Serializer.Deserialize<TestItemList>(stream);
      }
    }

    public static byte[] Serialize(TestItemList list)
    {
      using (var stream = new MemoryStream())
      {
        Serializer.Serialize(stream, list);
        return stream.ToArray();
      }
    }
  }

  [Serializable]
  [ProtoContract]
  [DataContract]
  public class TestResultItem
  {
    [ProtoMember(1)]
    [DataMember(Order = 1)]
    public long QuestionId { get; set; }

    [ProtoMember(2)]
    [DataMember(Order = 2)]
    public string Answer { get; set; }

    [ProtoMember(3)]
    [DataMember(Order = 3)]
    public string CorrectAnswer { get; set; }

    [ProtoMember(4)]
    [DataMember(Order = 4)]
    public double Score { get; set; }
  }
}
