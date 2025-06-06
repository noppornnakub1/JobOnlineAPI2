using System.Data;
using System.Dynamic;
using System.Text.Json;
using Dapper;
using JobOnlineAPI.Models;
using JobOnlineAPI.Repositories;
using JobOnlineAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobOnlineAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicantsController : ControllerBase
    {
        private readonly IApplicantRepository _applicantRepository;
        private readonly IJobApplicationRepository _jobApplicationRepository;
        private readonly IEmailService _emailService;
        private readonly string _resumeBasePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "resumes");
        private readonly string[] _allowedExtensions = [".pdf", ".doc", ".docx"];

        public ApplicantsController(IApplicantRepository applicantRepository, IJobApplicationRepository jobApplicationRepository, IEmailService emailService)
        {
            _applicantRepository = applicantRepository;
            _jobApplicationRepository = jobApplicationRepository;
            _emailService = emailService;

            if (!Directory.Exists(_resumeBasePath))
            {
                Directory.CreateDirectory(_resumeBasePath);
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Applicant>>> GetAllApplicants()
        {
            var applicants = await _applicantRepository.GetAllApplicantsAsync();
            return Ok(applicants);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Applicant>> GetApplicantById(int id)
        {
            var applicant = await _applicantRepository.GetApplicantByIdAsync(id);
            if (applicant == null)
            {
                return NotFound();
            }
            return Ok(applicant);
        }

        [HttpPost]
        public async Task<ActionResult<Applicant>> AddApplicant(Applicant applicant)
        {
            int newId = await _applicantRepository.AddApplicantAsync(applicant);
            applicant.ApplicantID = newId;
            return CreatedAtAction(nameof(GetApplicantById), new { id = newId }, applicant);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateApplicant(int id, Applicant applicant)
        {
            if (id != applicant.ApplicantID)
            {
                return BadRequest();
            }

            var existingApplicant = await _applicantRepository.GetApplicantByIdAsync(id);
            if (existingApplicant == null)
            {
                return NotFound();
            }

            await _applicantRepository.UpdateApplicantAsync(applicant);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteApplicant(int id)
        {
            var existingApplicant = await _applicantRepository.GetApplicantByIdAsync(id);
            if (existingApplicant == null)
            {
                return NotFound();
            }

            await _applicantRepository.DeleteApplicantAsync(id);
            return NoContent();
        }

        [HttpPost("upload-resume")]
        public async Task<IActionResult> UploadResume(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                return BadRequest("Invalid file type. Only PDF, DOC, and DOCX are allowed.");

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_resumeBasePath, uniqueFileName);

            var fullPath = Path.GetFullPath(filePath);
            if (!fullPath.StartsWith(Path.GetFullPath(_resumeBasePath)))
                return BadRequest("Invalid file path.");

            try
            {
                using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving file: {ex.Message}");
            }

            return Ok(new { filePath = $"/resumes/{uniqueFileName}" });
        }

        [HttpPost("submit-application")]
        public async Task<IActionResult> SubmitApplication([FromForm] Applicant applicant, [FromForm] int jobId, [FromForm] IFormFile? resume)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (jobId == 0)
            {
                return BadRequest("JobID is required.");
            }

            string? resumePath = null;
            if (resume != null && resume.Length > 0)
            {
                var extension = Path.GetExtension(resume.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                    return BadRequest("Invalid file type. Only PDF, DOC, and DOCX are allowed.");

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(_resumeBasePath, uniqueFileName);

                var fullPath = Path.GetFullPath(filePath);
                if (!fullPath.StartsWith(Path.GetFullPath(_resumeBasePath)))
                    return BadRequest("Invalid file path.");

                try
                {
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await resume.CopyToAsync(stream);
                    }
                    resumePath = $"/resumes/{uniqueFileName}";
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error saving resume: {ex.Message}");
                }
            }

            int applicantId = await _applicantRepository.AddApplicantAsync(applicant);

            var jobApplication = new JobApplication
            {
                ApplicantID = applicantId,
                JobID = jobId,
                Status = "Submitted",
                SubmissionDate = DateTime.Now
            };

            await _jobApplicationRepository.AddJobApplicationAsync(jobApplication);

            return Ok(new { applicantId, jobApplication, resumePath });
        }

        [HttpPost("submit-application-dynamic")]
        public async Task<IActionResult> SubmitApplicationDynamic([FromBody] ExpandoObject request)
        {
            if (request == null || !((IDictionary<string, object?>)request).Any())
                return BadRequest("Invalid input.");

            var requestDictionary = (IDictionary<string, object?>)request;
            if (!requestDictionary.TryGetValue("JobID", out var jobIdObj) || jobIdObj == null)
                return BadRequest("JobID is required.");

            try
            {
                var parameters = CreateDynamicParameters(requestDictionary);
                AddOutputParameters(parameters);

                using var connection = _applicantRepository.GetConnection();
                await connection.ExecuteAsync("InsertApplicantDataV2", parameters, commandType: CommandType.StoredProcedure);

                var result = ExtractApplicationResult(parameters);
                if (result.ApplicantId == null)
                    return BadRequest("ApplicantID was not generated by the stored procedure.");

                await SendApplicationEmails(result);

                return Ok(new { ApplicantID = result.ApplicantId, Message = "Application submitted and emails sent successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
                return StatusCode(500, new { Error = "Internal Server Error", Details = ex.Message });
            }
        }

        private static DynamicParameters CreateDynamicParameters(IDictionary<string, object?> requestDictionary)
        {
            var parameters = new DynamicParameters();
            foreach (var kvp in requestDictionary)
            {
                if (kvp.Value is JsonElement jsonElement)
                    AddJsonElementParameter(parameters, kvp.Key, jsonElement);
                else
                    AddNonJsonParameter(parameters, kvp.Key, kvp.Value);
            }
            return parameters;
        }

        private static void AddJsonElementParameter(DynamicParameters parameters, string key, JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    var stringValue = jsonElement.GetString() ?? "";
                    parameters.Add(key, stringValue, DbType.String, size: stringValue.Length > 0 ? stringValue.Length : 1);
                    break;
                case JsonValueKind.Number:
                    if (jsonElement.TryGetInt32(out int intValue))
                        parameters.Add(key, intValue, DbType.Int32);
                    else if (jsonElement.TryGetDouble(out double doubleValue))
                        parameters.Add(key, doubleValue, DbType.Double);
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    parameters.Add(key, jsonElement.GetBoolean(), DbType.Boolean);
                    break;
                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    var rawJson = jsonElement.GetRawText();
                    parameters.Add(key, rawJson, DbType.String, size: rawJson.Length);
                    break;
                case JsonValueKind.Null:
                    parameters.Add(key, null);
                    break;
                default:
                    throw new ArgumentException($"Unsupported JSON value for key '{key}'.");
            }
        }

        private static void AddNonJsonParameter(DynamicParameters parameters, string key, object? value)
        {
            if (value is string strValue)
                parameters.Add(key, strValue, DbType.String, size: strValue.Length > 0 ? strValue.Length : 1);
            else
                parameters.Add(key, value);
        }

        private static void AddOutputParameters(DynamicParameters parameters)
        {
            parameters.Add("ApplicantID", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("ApplicantEmail", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
            parameters.Add("HRManagerEmails", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);
            parameters.Add("JobManagerEmails", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);
            parameters.Add("JobTitle", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
            parameters.Add("FirstNameEng", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
            parameters.Add("LastNameEng", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
            parameters.Add("FirstNameThai", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
            parameters.Add("LastNameThai", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
            parameters.Add("comName", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
        }

        private static (int? ApplicantId, string ApplicantEmail, string HRManagerEmails, string JobManagerEmails, string JobTitle, string FullNameEng, string FullNameThai, string CompanyName) ExtractApplicationResult(DynamicParameters parameters)
        {
            return (
                parameters.Get<int?>("ApplicantID"),
                parameters.Get<string>("ApplicantEmail") ?? "",
                parameters.Get<string>("HRManagerEmails") ?? "",
                parameters.Get<string>("JobManagerEmails") ?? "",
                parameters.Get<string>("JobTitle") ?? "",
                $"{parameters.Get<string>("FirstNameEng")} {parameters.Get<string>("LastNameEng")}".Trim(),
                $"{parameters.Get<string>("FirstNameThai")} {parameters.Get<string>("LastNameThai")}".Trim(),
                parameters.Get<string>("comName") ?? ""
            );
        }

        private async Task SendApplicationEmails((int? ApplicantId, string ApplicantEmail, string HRManagerEmails, string JobManagerEmails, string JobTitle, string FullNameEng, string FullNameThai, string CompanyName) result)
        {
            const string Tel = "09785849824";
            if (!string.IsNullOrEmpty(result.ApplicantEmail))
            {
                string applicantBody = $"""
                    <div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
                        <p style='font-size: 20px'>{result.CompanyName}: ได้รับใบสมัครงานของคุณแล้ว</p>
                        <p style='font-size: 20px'>เรียน คุณ {result.FullNameThai}</p>
                        <p style='font-size: 20px'>
                        ขอบคุณสำหรับความสนใจในตำแหน่ง {result.JobTitle} ที่บริษัท {result.CompanyName} ของเรา
                        เราขอยืนยันว่าได้รับใบสมัครของท่านเรียบร้อยแล้ว ทีมงานฝ่ายทรัพยากรบุคคลของเรากำลังพิจารณาใบสมัครของท่านและจะติดต่อกลับภายใน 7-14 วันทำการ หากคุณสมบัติของท่านตรงตามที่เรากำลังมองหา
                        หากท่านมีข้อสงสัยหรือต้องการข้อมูลเพิ่มเติม สามารถติดต่อเราได้ที่อีเมล <span style='color: blue;'>{result.HRManagerEmails}</span> หรือโทร <span style='color: blue;'>{Tel}</span>
                        ขอบคุณอีกครั้งสำหรับความสนใจร่วมงานกับเรา
                        </p>
                        <h2 style='font-size: 20px'>ด้วยความเคารพ,</h2>
                        <h2 style='font-size: 20px'>{result.FullNameThai}</h2>
                        <h2 style='font-size: 20px'>ฝ่ายทรัพยากรบุคคล</h2>
                        <h2 style='font-size: 20px'>{result.CompanyName}</h2>
                        <h2 style='font-size: 20px'>**อีเมลล์นี้ คือ ข้อความอัตโนมัติ กรุณาอย่าตอบกลับ**</h2>
                    </div>
                    """;

                await _emailService.SendEmailAsync(result.ApplicantEmail, "Application Received", applicantBody, true);
            }

            var managerEmails = $"{result.HRManagerEmails},{result.JobManagerEmails}"
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim());

            foreach (var email in managerEmails)
            {
                string managerBody = $"""
                    <div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
                        <p style='font-size: 22px'>**Do not reply**</p>
                        <p style='font-size: 20px'>Hi All,</p>
                        <p style='font-size: 20px'>We’ve received a new job application from <strong style='font-weight: bold'>{result.FullNameEng}</strong> for the <strong style='font-weight: bold'>{result.JobTitle}</strong> position.</p>
                        <p style='font-size: 20px'>For more details, please click <a target='_blank' href='https://oneejobs.oneeclick.co:7191/ApplicationForm/ApplicationFormView?id={result.ApplicantId}'>https://oneejobs.oneeclick.co</a></p>
                    </div>
                    """;
                await _emailService.SendEmailAsync(email, "New Job Application Received", managerBody, true);
            }
        }
    }
}