namespace JobOnlineAPI.Models
{
    public class HRStaff
    {
        public int HRID { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }
        public required string Role { get; set; }
    }
}