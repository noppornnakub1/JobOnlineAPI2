namespace JobOnlineAPI.Models
{
    public class JobSearchViewModel
    {
        public required string Query { get; set; }
        public required IEnumerable<Job> Jobs { get; set; }
    }
}
