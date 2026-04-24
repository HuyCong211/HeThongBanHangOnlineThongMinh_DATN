using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Net.Mail;
using System.Configuration;

namespace DoAn_Ver2.Common
{
    public class MailHelper
    {
        public static void SendMail(string toEmail, string subject, string content)
        {
            var fromEmailAddress = ConfigurationManager.AppSettings["FromEmailAddress"];

            var smtpSection = (System.Net.Configuration.SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");
            string fromEmail = smtpSection.From;
            string password = smtpSection.Network.Password;
            string host = smtpSection.Network.Host;
            int port = smtpSection.Network.Port;
            bool enableSsl = smtpSection.Network.EnableSsl;

            try
            {
                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(fromEmail, "Men Store Admin");
                    mail.To.Add(toEmail);
                    mail.Subject = subject;
                    mail.Body = content;
                    mail.IsBodyHtml = true;

                    using (SmtpClient smtp = new SmtpClient(host, port))
                    {
                        smtp.Credentials = new NetworkCredential(fromEmail, password);
                        smtp.EnableSsl = enableSsl;
                        smtp.Send(mail);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex; 
            }
        }
    }
}