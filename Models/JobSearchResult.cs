namespace JobOnlineAPI.Models
{
    public class JobSearchResult
    {
        public required Job Job { get; set; }
        public required string MatchedField { get; set; }
    }
}