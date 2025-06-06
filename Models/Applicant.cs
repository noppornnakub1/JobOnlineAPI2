namespace JobOnlineAPI.Models
{
    public class Applicant
    {
        public int? ApplicantID { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }
        public string? Resume { get; set; }
        public DateTime? AppliedDate { get; set; }
        public string? JobTitle { get; set; }
        public string? JobLocation { get; set; }
        public string? JobDepartment { get; set; }
        public string? Status { get; set; }
    }
}