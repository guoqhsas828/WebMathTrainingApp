using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace StoreManager.Services
{
    // This class is used by the application to send email for account confirmation and password reset.
    // For more details see https://go.microsoft.com/fwlink/?LinkID=532713
    public class EmailSender : IEmailSender
    {
        //dependency injection
        private SendGridOptions _sendGridOptions { get; }
        private IFunctional _functional { get; }
        private SmtpOptions _smtpOptions { get; }

        public EmailSender(IOptions<SendGridOptions> sendGridOptions,
            IFunctional functional,
            IOptions<SmtpOptions> smtpOptions)
        {
            _sendGridOptions = sendGridOptions.Value;
            _functional = functional;
            _smtpOptions = smtpOptions.Value;
        }


        public Task SendEmailAsync(string email, string subject, string message)
        {
            //sendgrid is become default
            if (_sendGridOptions.IsDefault)
            {
                _functional.SendEmailBySendGridAsync(_sendGridOptions.SendGridKey,
                                                    _sendGridOptions.FromEmail,
                                                    _sendGridOptions.FromFullName,
                                                    subject,
                                                    message,
                                                    email)
                                                    .Wait();
            }

            //smtp is become default
            if (_smtpOptions.IsDefault)
            {
                _functional.SendEmailByGmailAsync(_smtpOptions.fromEmail,
                                            _smtpOptions.fromFullName,
                                            subject,
                                            message,
                                            email,
                                            email,
                                            _smtpOptions.smtpUserName,
                                            _smtpOptions.smtpPassword,
                                            _smtpOptions.smtpHost,
                                            _smtpOptions.smtpPort,
                                            _smtpOptions.smtpSSL)
                                            .Wait();
            }

            
            return Task.CompletedTask;
        }

      public async Task SendSmsMessage(string msgText, string phoneNumber)
      {
        var accountSid = _smtpOptions.AcctSid;
        var authToken = _smtpOptions.AcctToken;
        var toPhoneNumber = phoneNumber.StartsWith("+") ? phoneNumber : (phoneNumber.Length ==10 ? "+1" : "+") + phoneNumber;
        TwilioClient.Init(accountSid, authToken);

        var message = MessageResource.Create(
          body: msgText,
          from: new Twilio.Types.PhoneNumber(_smtpOptions.FromNumber),
          to: new Twilio.Types.PhoneNumber(toPhoneNumber)
        );
      }
  }
}
