using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebMathTraining.Models
{
    [NotMapped]
    public class TestResult
    {
        public string TestSessionId { get; set; }

        public string UserId { get; set; }

        public double[] FinalScores { get; set; }
    }
}
