namespace EmailWorkerService.Models
{
    public class EmailSettings
    {
        public string ImapServer { get; set; } = string.Empty;
        public int ImapPort { get; set; }
        public string EmailAddress { get; set; } = string.Empty;
        public string AppPassword { get; set; } = string.Empty;
    }
}