using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using JobOnlineAPI.Models;

namespace JobOnlineAPI.Repositories
{
    public interface IApplicantRepository
    {
        Task<IEnumerable<Applicant>> GetAllApplicantsAsync();
        Task<Applicant> GetApplicantByIdAsync(int id);
        Task<int> AddApplicantAsync(Applicant applicant);
        Task<int> UpdateApplicantAsync(Applicant applicant);
        Task<int> DeleteApplicantAsync(int id);
        IDbConnection GetConnection();
    }
}