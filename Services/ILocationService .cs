namespace JobOnlineAPI.Services
{
    public interface ILocationService
    {
        Task<IEnumerable<dynamic>> GetProvincesAsync();
        Task<IEnumerable<dynamic>> GetDistrictsByProvinceAsync(int provinceCode);
        Task<IEnumerable<dynamic>> GetSubDistrictsByDistrictAsync(int districtId);
        Task<string?> GetPostalCodeAsync(int provinceCode, int districtCode, int subDistrictCode);
    }
}
