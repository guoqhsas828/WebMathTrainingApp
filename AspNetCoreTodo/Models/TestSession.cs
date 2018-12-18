using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebMathTraining.Models
{
    [NotMapped]
    public class TestSession
    {
        public string SessionId { get; set; }

        public string Category { get; set; }

        public string[] QuestionIds { get; set; }

        public double[] Points { get; set; }

        public DateTime PlannedStart { get; set; }

        public DateTime PlannedEnd { get; set; }

        public DateTime TestStart { get; set; }

        public DateTime TestEnd { get; set; }

        public string[] TestUserIds { get; set; }

    }
}
