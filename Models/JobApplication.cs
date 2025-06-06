using System.Text.Json.Serialization;

namespace JobOnlineAPI.Models
{
    public class JobApplication
    {
        public int? ApplicationID { get; set; }
        [JsonRequired]
        public int ApplicantID { get; set; }
        [JsonRequired]
        public int JobID { get; set; }
        public string? Status { get; set; }
        [JsonRequired]
        public DateTime SubmissionDate { get; set; }
        public DateTime? InterviewDate { get; set; }
        public string? Result { get; set; }
    }
}