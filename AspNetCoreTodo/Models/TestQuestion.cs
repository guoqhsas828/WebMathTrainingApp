using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using ProtoBuf;

namespace WebMathTraining.Models
{
    public enum TestAnswerType
    {
        None = 0,
        SingleChoice,
        MultipleChoice,
        Integer,
        Number,
        Text
    }

  public class TestQuestion
  {
    public string Id { get; set; }

    public string Category { get; set; }

    public int Level { get; set; }

    [Required]
    public TestImage QuestionImage { get; set; }

    public byte[] AnswerStream { get; set; }

    public string Source { get; set; }

    [NotMapped]
    public TestAnswer TestAnswer
    {
      get
      {
        if (_testAnswer == null)
        {
          using (var stream = new MemoryStream(AnswerStream))
          {
            _testAnswer = Serializer.Deserialize<TestAnswer>(stream);
          }
        }
        return _testAnswer;
      }
      set
      {
        _testAnswer = value;
        using (var stream = new MemoryStream())
        {
          Serializer.Serialize(stream, _testAnswer);
          AnswerStream = stream.ToArray();
        }
      }
    }

    #region Data

    [NotMapped]
    private TestAnswer _testAnswer;

    #endregion
  }

  [Serializable]
  [ProtoContract]
  [DataContract]
  public class TestAnswer : ICloneable
  {
    [ProtoMember(1)]
    [DataMember(Order = 1, IsRequired = true)]
    public TestAnswerType AnswerType { get; set; }

    [ProtoMember(2)]
    [DataMember(Order = 2)]
    public string AnswerChoice1 { get; set; }

    [ProtoMember(3)]
    [DataMember(Order = 3)]
    public string AnswerChoice2 { get; set; }

    [ProtoMember(4)]
    [DataMember(Order = 4)]
    public string AnswerChoice3 { get; set; }

    [ProtoMember(5)]
    [DataMember(Order = 5)]
    public string AnswerChoice4 { get; set; }

    [ProtoMember(6)]
    [DataMember(Order = 6)]
    public string AnswerChoice5 { get; set; }

    [ProtoMember(7)]
    [DataMember(Order = 7)]
    public string AnswerChoice6 { get; set; }

    [ProtoMember(8)]
    [DataMember(Order = 8)]
    public double NumericAnswer
    {
      get { return _numericAnswer.HasValue ? _numericAnswer.Value : default(double); }
      set { _numericAnswer = value; }
    }

    [XmlIgnore]
    [Browsable(false)]
    public bool NumericAnswerSpecified
    {
      get
      {
        return _numericAnswer.HasValue;
      }
    }

    [ProtoMember(9)]
    [DataMember(Order = 9)]
    public double NumericAccuracy
    {
      get { return _numericAccuracy.HasValue ? _numericAccuracy.Value : default(double); }
      set { _numericAccuracy = value; }
    }

    [XmlIgnore]
    [Browsable(false)]
    public bool NumericAccuracySpecified
    {
      get
      {
        return _numericAccuracy.HasValue;
      }
    }

    [ProtoMember(10)]
    [DataMember(Order = 10)]
    public string TextAnswer { get; set; }

    [XmlIgnore]
    [Browsable(false)]
    public bool TextAnswerSpecified
    {
      get { return !string.IsNullOrEmpty(TextAnswer); }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public object Clone()
    {
      return MemberwiseClone();
    }

    #region Data

    private double? _numericAnswer;
    private double? _numericAccuracy;

    #endregion
  }
}
