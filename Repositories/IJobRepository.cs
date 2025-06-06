using JobOnlineAPI.Models;

namespace JobOnlineAPI.Repositories
{
    public interface IJobRepository
    {
        Task<IEnumerable<Job>> GetAllJobsAsync();
        Task<Job> GetJobByIdAsync(int id);
        Task<int> AddJobAsync(Job job);
        Task<int> UpdateJobAsync(Job job);
        Task<int> DeleteJobAsync(int id);
    }
}