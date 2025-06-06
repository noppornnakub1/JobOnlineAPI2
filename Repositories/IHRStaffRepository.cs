using JobOnlineAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobOnlineAPI.Repositories
{
    public interface IHRStaffRepository
    {
        Task<IEnumerable<HRStaff>> GetAllHRStaffAsync();
        Task<HRStaff?> GetHRStaffByEmailAsync(string email);
        Task<IEnumerable<dynamic>> GetAllStaffAsyncNew(string? email);
        
    }
}