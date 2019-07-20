using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StoreManager.Models.AccountViewModels
{
    public class ForgotPasswordViewModel
    {
    [Required]
      [EmailAddress]
      [Display(Name = "Email")]
      public string Email { get; set; }

      [Required]
      [Display(Name = "User Name")]
      public string UserName { get; set; }
  }
}
