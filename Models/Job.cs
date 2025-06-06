using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace JobOnlineAPI.Models
{
    public class Job
    {
        public int? JobID { get; set; }

        [Required(ErrorMessage = "Job title is required.")]
        public string JobTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Job description is required.")]
        public string JobDescription { get; set; } = string.Empty;

        [Required(ErrorMessage = "Requirements are required.")]
        public string Requirements { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location is required.")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "ExperienceYears is required.")]
        public string ExperienceYears { get; set; } = string.Empty;

        [JsonRequired]
        [Range(0, 100, ErrorMessage = "Number of positions must be between 0 and 100.")]
        public int NumberOfPositions { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Job status is required.")]
        public string JobStatus { get; set; } = "Open";

        public string ApprovalStatus { get; set; } = "Pending";
        public int? ApplicantCount { get; set; }
        public DateTime? PostedDate { get; set; }

        [Required(ErrorMessage = "Closing date is required.")]
        public DateTime? ClosingDate { get; set; }

        public int? CreatedBy { get; set; }
        public string CreatedByRole { get; set; } = string.Empty;
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? HighlightedJobTitle { get; set; }
        public string? HighlightedDepartment { get; set; }
        public string? HighlightedLocation { get; set; }
        public string? HighlightedDescription { get; set; }
        public string? HighlightedRequirements { get; set; }
        public string? Email { get; set; }
        public int? AdminID { get; set; }
        public string? TELOFF { get; set; }
        public string? NAMETHAI { get; set; }
        public string? Role { get; set; }
        public string? NAMECOSTCENT { get; set; }
        public string? Remark { get; set; }
        public string? OpenFor { get; set; }
    }
}