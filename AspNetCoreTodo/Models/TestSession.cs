using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.IO;
using System.Linq;
using ProtoBuf;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebMathTraining.Utilities;

namespace WebMathTraining.Models
{
  public class TestSession
  {
    public TestSession()
    {
      PlannedStart = DateTime.UtcNow;
      PlannedEnd = DateTime.UtcNow.AddMinutes(30);
    }

    public Guid Id { get; set; }

    //[Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ObjectId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public byte[] TestQuestionData { get; set; }

    [NotMapped]
    public TestQuestionList TestQuestions
    {
      get
      {
        return _testQuestions ?? (_testQuestions = TestQuestionData == null
                 ? new TestQuestionList()
                 : TestQuestionList.Deserialize(TestQuestionData));
      }
      set
      {
        _testQuestions = value;
        if (_testQuestions == null || _testQuestions.Count == 0)
          TestQuestionData = null;
        else
        {
          TestQuestionData = TestQuestionList.Serialize(_testQuestions);
        }
      }
    }

    public DateTime PlannedStart { get; set; }

    [NotMapped]
    public DateTime PlannedStartLocal { get { return PlannedStart.ToLocalTime(); } set { PlannedStart = value.ToUniversalTime(); } }

    public DateTime PlannedEnd { get; set; }

    [NotMapped]
    public DateTime PlannedEndLocal { get { return PlannedEnd.ToLocalTime(); } set { PlannedEnd = value.ToUniversalTime(); } }

    public byte[] TesterData { get; set; }

    [NotMapped]
    public TesterList Testers
    {
      get
      {
        if (_testers == null)
        {
          if (TesterData == null)
            _testers = new TesterList();
          else
            _testers = TesterList.Deserialize(TesterData);
        }
        return _testers;
      }
      set
      {
        _testers = value;
        if (_testers == null || _testers.Count == 0)
          TesterData = null;
        else
        {
          TesterData = TesterList.Serialize(_testers);
        }
      }
    }

    public DateTime LastUpdated { get; set; }

    [NotMapped]
    public DateTime LastUpdatedLocal { get { return LastUpdated.ToLocalTime(); } set { LastUpdated = value.ToUniversalTime(); } }

    [NotMapped]
    public TimeSpan SessionTimeSpan => PlannedEnd - PlannedStart;

    [NotMapped]
    public string SessionTime
    {
      get { return SessionTimeSpan.Display(); }
    }

    [NotMapped]
    public int QuestionRequest { get; set; }

    public int TargetGrade { get; set; }

    #region Methods

    public bool IsRegisteredUser(long userId)
    {
      return Testers.Items.Any(u => u.TesterId == userId);
    }

    #endregion

    #region Data

    [NotMapped]
    private TestQuestionList _testQuestions;

    [NotMapped]
    private TesterList _testers;

    #endregion
  }

  [Serializable]
  [ProtoContract]
  [DataContract]
  public class TestQuestionList : IEnumerable<TestQuestionItem>
  {
    private readonly List<TestQuestionItem> _items = new List<TestQuestionItem>();

    /// <summary>
    /// Gets the items.
    /// </summary>
    /// <value>The items.</value>
    [ProtoMember(1)]
    [DataMember(Order = 1)]
    public List<TestQuestionItem> Items
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
    /// <param name="item"></param>
    public void Add(TestQuestionItem item)
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
    public static TestQuestionList Deserialize(byte[] bytes)
    {
      using (var stream = new MemoryStream(bytes))
      {
        return Serializer.Deserialize<TestQuestionList>(stream);
      }
    }

    public static byte[] Serialize(TestQuestionList list)
    {
      using (var stream = new MemoryStream())
      {
        Serializer.Serialize(stream, list);
        return stream.ToArray();
      }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>1</filterpriority>
    public IEnumerator<TestQuestionItem> GetEnumerator()
    {
      return Items.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }

  [Serializable]
  [ProtoContract]
  [DataContract]
  public class TestQuestionItem
  {
    [DataMember(Order = 1)]
    [ProtoMember(1, DataFormat = DataFormat.TwosComplement)]
    public int Idx { get; set; }

    [ProtoMember(2)]
    [DataMember(Order = 2)]
    public long QuestionId { get; set; }

    [ProtoMember(3)]
    [DataMember(Order = 3)]
    public double ScorePoint { get; set; }

    [ProtoMember(4)]
    [DataMember(Order = 4)]
    public double PenaltyPoint { get; set; }
  }

  [Serializable]
  [ProtoContract]
  [DataContract]
  public class TesterList
  {
    private readonly List<TesterItem> _items = new List<TesterItem>();

    /// <summary>
    /// Gets the items.
    /// </summary>
    /// <value>The items.</value>
    [ProtoMember(1)]
    [DataMember(Order = 1)]
    public List<TesterItem> Items
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
    /// <param name="item"></param>
    public void Add(TesterItem item)
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
    public static TesterList Deserialize(byte[] bytes)
    {
      using (var stream = new MemoryStream(bytes))
      {
        return Serializer.Deserialize<TesterList>(stream);
      }
    }

    public static byte[] Serialize(TesterList list)
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
  public class TesterItem
  {
    [ProtoMember(1)]
    [DataMember(Order = 1)]
    public long TesterId { get; set; }

    [ProtoMember(2)]
    [DataMember(Order = 2)]
    public int Grade { get; set; }

    [ProtoMember(3)]
    [DataMember(Order = 3)]
    public string Group { get; set; }
  }
}
