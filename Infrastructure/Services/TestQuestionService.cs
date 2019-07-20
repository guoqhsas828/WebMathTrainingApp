using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using WebMathTraining.Models;
using StoreManager.Interfaces;
using StoreManager.Models;
using StoreManager.Specifications;

namespace WebMathTraining.Services
{
  public interface ITestQuestionService<T> where T : struct
  {
    T CreateTestImage(byte[] imageData, string imageName, string contentType,string container=null);
    T CreateOrUpdate(T id, T imageId, int level, string textAnswer, TestCategory category = TestCategory.Math,
    TestAnswerType answerChoice = TestAnswerType.Text);
    int CountQuestions();
    void DeleteQuestion(T id);
    TestQuestion FindTestQuestion(T id);
  }

  public class TestQuestionService : ITestQuestionService<int>
  {
    private readonly ICatalogRepository<TestImage> _imageRepository;
    private readonly ICatalogRepository<TestQuestion> _questionRepository;

    public TestQuestionService(ICatalogRepository<TestImage> imageRepo, ICatalogRepository<TestQuestion> questionRepo)
    {
      _imageRepository = imageRepo;
      _questionRepository = questionRepo;
    }

    public TestQuestion FindTestQuestion(int id)
    {
      return _questionRepository.GetByIdAsync(id).Result;
    }

    public int CountQuestions()
    {
      return _questionRepository.ListAllAsync().Result.Count();
    }

    public int CreateTestImage(byte[] imageData, string imageName, string contentType, string container=null)
    {
      var image = _imageRepository.ListAsync(new TestImageFilterSpecification(contentType, imageName)).Result.FirstOrDefault();
      var containerType = CloudContainer.None;
      if (!String.IsNullOrEmpty(container) && !Enum.TryParse<CloudContainer>(container, out containerType))
      {
        containerType = CloudContainer.None;
      }

      if (image == null)
      {
        image = new TestImage()
        {
          ContentType = contentType,
          Data = imageData,
          Length = imageData?.Length ?? 0,
          Name = imageName,
          Width = containerType
        };

        _imageRepository.AddAsync(image).Wait();

      }
      else
      {
        //Image exists, do update
        image.ContentType = contentType;
        image.Width = containerType;
        image.Data = imageData;
        image.Length = imageData?.Length ?? 0;
        _imageRepository.UpdateAsync(image).Wait();
      }

      return image.Id;
    }

    public int CreateOrUpdate(int id, int imageId, int level, string textAnswer, TestCategory category = TestCategory.Math,
      TestAnswerType answerChoice = TestAnswerType.Text)
    {
      TestQuestion entity = null;
      try
      {
        var image = _imageRepository.ListAsync(new TestImageFilterSpecification(imageId)).Result.FirstOrDefault();
        if (image != null)
        {
          entity = _questionRepository.ListAsync(new TestQuestionFilterSpecification(id, imageId)).Result.FirstOrDefault();

          if (entity == null)
          {
            entity = new TestQuestion()
            {
              Category = category,
              Level = level,
              TestAnswer = new TestAnswer() {AnswerType = answerChoice, TextAnswer = textAnswer},
              QuestionImageId = imageId
            };
            _questionRepository.AddAsync(entity).Wait();
          }
          else
          {
            entity.Category = category;
            entity.Level = level;
            entity.QuestionImageId = imageId;
            entity.TestAnswer = new TestAnswer()
              {AnswerType = answerChoice, TextAnswer = textAnswer};

            _questionRepository.UpdateAsync(entity).Wait();
          }
        }
      }
      catch (Exception)
      {
        return -1;
      }

      return entity?.ObjectId ?? 0;
    }

    public void DeleteQuestion(int id)
    {
      var q = _questionRepository.GetByIdAsync(id).Result;
      if (q != null)
      {
        _questionRepository.DeleteAsync(q).Wait();
      }
    }
  }
}
