using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebMathTraining.Models
{
    [NotMapped]
    public class TestRequest
    {
        public string Id { get; set; }

        public string Category { get; set; }

        public int TotalQuestions { get; set; }

        public int LowLevel { get; set; }

        public int HighLevel { get; set; }

        public string SourceFilter { get; set; }

        public DateTime RequestTime { get; set; }

        public string RequestUser { get; set; }
    }
}
