using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using ProtoBuf;
using StoreManager.Models;
using ICloneable = System.ICloneable;


namespace WebMathTraining.Models
{
 // [Entity(EntityId = 102, TableName = "TestQuestions", Key = new string[] { "Id" })]
  public class TestQuestion : CatalogEntityModel //PersistentObject
  {
    public TestQuestion()
    {
      Category = TestCategory.Math;
      Level = 1;
    }

//    [GuidProperty(AllowNull = false)]
    //public Guid Id { get; set; }
    [Key]
    public int ObjectId
    {
      get { return Id; }
      set { Id = value; }
    }


//    [EnumProperty]
    public TestCategory Category { get; set; }



//    [NumericProperty]
    public int Level { get; set; }



//    [Required]
////    [ManyToOneProperty]
//    public TestImage QuestionImage
//    {

//      get { return (TestImage)ObjectRef.Resolve(questionImage_); }

//      set { questionImage_ = ObjectRef.Create(value); }

//    }
    public int QuestionImageId { get; set; }


//    [BinaryBlobProperty]
    public byte[] AnswerStream { get; set; }



    //public string Source { get; set; }

    [NotMapped]
    public TestAnswer TestAnswer
    {
      get
      {
        if (_testAnswer == null)
        {

          if (AnswerStream != null)

          {

            using (var stream = new MemoryStream(AnswerStream))

            {

              _testAnswer = Serializer.Deserialize<TestAnswer>(stream);

            }

          }

        }

        return _testAnswer;

      }

      set

      {

        _testAnswer = value;

        if (value != null)

        {

          using (var stream = new MemoryStream())

          {

            Serializer.Serialize(stream, _testAnswer);

            AnswerStream = stream.ToArray();

          }

        }

      }

    }



    #region Data



    [NotMapped]private TestAnswer _testAnswer;

    //[NotMapped]

    //private ObjectRef questionImage_;



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

    [DisplayName("Answer Tip")]

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

    [NotMapped]

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

    [NotMapped]

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



    [NotMapped]

    private double? _numericAnswer;



    [NotMapped]

    private double? _numericAccuracy;



    #endregion

  }

}