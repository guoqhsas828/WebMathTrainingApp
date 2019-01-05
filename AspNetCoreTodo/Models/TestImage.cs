using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebMathTraining.Models
{
  public class TestImage
  {
    public Guid Id { get; set; }

    public string Name { get; set; }

    public byte[] Data { get; set; }

    public int Length { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string ContentType { get; set; }

    //[Key]
    //[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    //public long ObjectId { get; set; }
  }
}
