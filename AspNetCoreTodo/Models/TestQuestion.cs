using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Protobuf;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Google.Protobuf.Compatibility;
using Google.Protobuf.WellKnownTypes;

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

        public byte[] TestAnswer { get; set; }

        public string Source { get; set; }

    }

  [Serializable]
  [ProtoContract]
  [DataContract]
  public class TestAnswer : ICloneable
  {
    //[ProtoMember()]
    [DataMember(Order = 1, IsRequired = true)]
    public TestAnswerType AnswerType { get; set; }

    [DataMember(Order = 2)]
    public string AnswerChoice1 { get; set; }

    [DataMember(Order = 3)]
    public string AnswerChoice2 { get; set; }

    [DataMember(Order = 4)]
    public string AnswerChoice3 { get; set; }

    [DataMember(Order = 5)]
    public string AnswerChoice4 { get; set; }

    [DataMember(Order = 6)]
    public string AnswerChoice5 { get; set; }

    [DataMember(Order = 7)]
    public string AnswerChoice6 { get; set; }

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

    [DataMember(Order = 10)]
    public string TextAnswer { get; set; }

    [XmlIgnore]
    [Browsable(false)]
    public bool TextAnswerSpecified
    {
      get { return !string.IsNullOrEmpty(TextAnswer); }
    }

    public object Clone()
    {
      return MemberwiseClone();
    }

    #region Data
if (riskResult != null)
                {
                  using (var stream = new MemoryStream(riskResult))
                  {
                    resultList.Items.Add(Serializer.Deserialize<RiskResult>(stream));
                  }
                }
    private double? _numericAnswer;
    private double? _numericAccuracy;
using (var stream = new MemoryStream())
      {
        Serializer.Serialize(stream, riskResult);
        _insertTradeRiskResultCommand.Parameters["riskResult"].Value = stream.ToArray();
      }
    #endregion
  }
}
