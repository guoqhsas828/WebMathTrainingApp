using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SendGrid;

namespace WebMathTraining.Services
{
  // This class is used by the application to send email for account confirmation and password reset.
  // For more details see https://go.microsoft.com/fwlink/?LinkID=532713
  public class EmailSender : IEmailSender
  {
    private IConfiguration Configuration { get; set; }

    public EmailSender(IConfiguration config)
    {
      Configuration = config;
    }

    public Task SendEmailAsync(string email, string subject, string message)
    {
      if (String.IsNullOrEmpty(email))
        return Task.CompletedTask;

      using (var mail = new MailMessage
      {
        From = new MailAddress(Constants.ClientSupportEmail),
        Subject = subject,
        Body = message,
        IsBodyHtml = true
      })
      {
        mail.To.Add(email);

        try
        {
          if (Configuration.GetSection("EmailCredentials") == null)
            return Task.CompletedTask;

          using (var smtp = new SmtpClient
          {
            Host = "smtp.sendgrid.net",
            Credentials = new System.Net.NetworkCredential
            (Configuration.GetSection("EmailCredentials")["AcctUserName"],
              Configuration.GetSection("EmailCredentials")["AcctPassword"]),
            Port = 587,
            EnableSsl = true
          })
          {

            //Or Your SMTP Server Address
            // ***use valid credentials***
            //Or your Smtp Email ID and Password
            smtp.Send(mail);


            return Task.CompletedTask;
          }
        }
        catch (Exception ex)
        {
          return Task.CompletedTask;
        }
      }
    }
  }
}

