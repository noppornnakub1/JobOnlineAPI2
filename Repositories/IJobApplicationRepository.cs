using JobOnlineAPI.Models;
using Microsoft.AspNetCore.Builder;
using System.Data;

namespace JobOnlineAPI.Repositories
{
    public interface IJobApplicationRepository
    {
        Task<int> AddJobApplicationAsync(JobApplication jobApplication);
        IDbConnection GetConnection();
    }
}
