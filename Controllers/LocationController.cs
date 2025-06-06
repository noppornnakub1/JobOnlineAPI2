using JobOnlineAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;

        public LocationController(ILocationService locationService)
        {
            _locationService = locationService;
        }

        [HttpGet("provinces")]
        public async Task<IActionResult> GetProvinces()
        {
            var provinces = await _locationService.GetProvincesAsync();
            return Ok(provinces);
        }

        [HttpGet("districts/{provinceCode}")]
        public async Task<IActionResult> GetDistricts(int provinceCode)
        {
            var districts = await _locationService.GetDistrictsByProvinceAsync(provinceCode);
            return Ok(districts);
        }

        [HttpGet("subdistricts/{districtId}")]
        public async Task<IActionResult> GetSubDistricts(int districtId)
        {
            var subDistricts = await _locationService.GetSubDistrictsByDistrictAsync(districtId);
            return Ok(subDistricts);
        }

        [HttpGet("postalcode")]
        public async Task<IActionResult> GetPostalCode(int provinceCode, int districtCode, int subDistrictCode)
        {
            var postalCode = await _locationService.GetPostalCodeAsync(provinceCode, districtCode, subDistrictCode);
            return Ok(postalCode);
        }
    }
}