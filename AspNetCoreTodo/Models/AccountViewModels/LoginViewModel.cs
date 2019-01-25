using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models.AccountViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "userNameRequired")]
        [Display(Name="User Name")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "passwordRequired")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}
