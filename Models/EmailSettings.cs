namespace JobOnlineAPI.Models
{
    public class EmailSettings
    {
        public required string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public required string SmtpUser { get; set; }
        public required string SmtpPass { get; set; }
        public required string FromEmail { get; set; }
        public required string SenderName { get; set; }
        public required bool UseSSL { get; set; }

        
    }
}
