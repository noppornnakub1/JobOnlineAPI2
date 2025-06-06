namespace JobOnlineAPI.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string? ConfirmConsent { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
