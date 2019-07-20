using WebMathTraining.Models;
using StoreManager.Models;

namespace StoreManager.Specifications
{
  public class TestSessionResultFilterSpecification : BaseSpecification<TestResult>
  {
    public TestSessionResultFilterSpecification(int? sessionId)
        : base(i => (!sessionId.HasValue || i.TestSessionId == sessionId))
    {
    }

  }

  public class TestImageFilterSpecification : BaseSpecification<TestImage>
  {
    public TestImageFilterSpecification(string contentType, string imageName)
        : base(i => string.Compare(i.Name, imageName, true) == 0 && string.Compare(i.ContentType, contentType, true) == 0)
    {
    }

    public TestImageFilterSpecification(string excludeContentType)
        : base(i => string.Compare(i.ContentType, excludeContentType, true) != 0)
    {
    }

    public TestImageFilterSpecification(int? imageId)
        : base(i => (!imageId.HasValue || i.Id == imageId))
    {
    }
  }

  public class TestQuestionFilterSpecification : BaseSpecification<TestQuestion>
  {
    public TestQuestionFilterSpecification(int? id, int? imageId)
        : base(i => (!id.HasValue || i.Id == id) || (imageId.HasValue && i.QuestionImageId == imageId))
    {
    }

    public TestQuestionFilterSpecification(int targetTestGrade)
      : base(q => q.Level >= targetTestGrade - 1 && q.Level <= targetTestGrade + 1)
    {
    }
  }

  public class TestGroupFilterSpecification : BaseSpecification<TestGroup>
  {
    public TestGroupFilterSpecification(string groupName)
        : base(i => string.Compare(i.Name, groupName, true) == 0)
    {
    }

    public TestGroupFilterSpecification(int? id)
    : base(i => (!id.HasValue || i.Id == id))
    {
    }
  }

  public class TestSessionFilterSpecification : BaseSpecification<TestSession>
  {
    public TestSessionFilterSpecification(string name)
    : base(i => string.Compare(i.Name, name, true) == 0)
    {
    }
  }

}
