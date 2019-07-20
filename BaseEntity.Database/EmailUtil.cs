// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Net.Mail;
using System.Text;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public static class EmailUtil
  {
    /// <summary>
    /// Sends the email.
    /// </summary>
    /// <param name="toEmailStrings">To email address strings.</param>
    /// <param name="ccEmailStrings">The cc email address strings.</param>
    /// <param name="bccEmailStrings">The BCC email address strings.</param>
    /// <param name="fromEmail">From email address.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="content">The content.</param>
    /// <param name="isBodyHtml">if set to <c>true</c> [is body HTML].</param>
    /// <returns></returns>
    public static string SendEmail(string[] toEmailStrings, string[] ccEmailStrings, string[] bccEmailStrings, string fromEmail, string subject, string content,
      bool isBodyHtml = true)
    {
      try
      {
        //uses settings in app.config, has overrides to set settings manually.
        using (var smtpClient = new SmtpClient())
        {
          using (var mail = new MailMessage())
          {
            mail.Subject = subject;
            mail.Body = content;
            mail.IsBodyHtml = isBodyHtml;
            mail.SubjectEncoding = Encoding.UTF8;
            mail.BodyEncoding = Encoding.UTF8;

            //send from 
            try
            {
              mail.From = new MailAddress(fromEmail);
            }
            catch (Exception ex)
            {
              return string.Format("Failed to send email because of invalid from email address - {0}\r\n{1}", fromEmail, ex.Message);
            }

            foreach (string email in toEmailStrings)
            {
              try
              {
                mail.To.Add(email.Trim());
              }
              catch (Exception ex)
              {
                return string.Format("Failed to send email because of invalid to email address - {0}\r\n{1}", email, ex.Message);
              }
            }

            foreach (string email in ccEmailStrings)
            {
              try
              {
                mail.CC.Add(email.Trim());
              }
              catch (Exception ex)
              {
                return string.Format("Failed to send email because of invalid cc email address - {0}\r\n{1}", email, ex.Message);
              }
            }

            foreach (string email in bccEmailStrings)
            {
              try
              {
                mail.Bcc.Add(email.Trim());
              }
              catch (Exception ex)
              {
                return string.Format("Failed to send email because of invalid bcc email address - {0}\r\n{1}", email, ex.Message);
              }
            }

            // send out email
            smtpClient.Send(mail);

            return null;
          }
        }
      }
      catch (SmtpException smtpException)
      {
        return string.Format("Failed to send email:{0} : {1} ", smtpException.StatusCode, smtpException.Message);
      }
      catch (Exception   ex)
      {
        return string.Format("Failed to send email: : {0} ", ex.Message);
      }
    }
  }
}