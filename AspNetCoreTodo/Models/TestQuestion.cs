using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        public byte[] AnswerChoices { get; set; }

        [Required]
        public TestAnswerType AnswerType { get; set; }

        [NotMapped]
        public List<string> AnswerChoiceList { get; set; }

        [Required]
        public string FinalAnswer { get; set; }

        public string Source { get; set; }

        [NotMapped]
        private List<string> _answerChoiceList;
    }
}
