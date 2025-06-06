using Dapper;
using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.DAL;
using System.Text.Json;
using JobOnlineAPI.Services;
using System.Dynamic;
using System.Data;
using System.Runtime.InteropServices;
using JobOnlineAPI.Filters;
using JobOnlineAPI.Models;
using static System.Net.WebRequestMethods;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicantNewController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ApplicantNewController> _logger;
        private readonly string _basePath;
        private readonly string? _username;
        private readonly string? _password;
        private readonly bool _useNetworkShare;
        private readonly string _applicationFormUri;
        private readonly FileStorageConfig _fileStorageConfig;
        private const string JobTitleKey = "JobTitle";
        private const string JobIdKey = "JobID";
        private const string ApplicantIdKey = "ApplicantID";
        private const string UserIdKey = "UserId";
        public string TypeMail { get; set; } = "-";

        private sealed record ApplicantRequestData(
            int ApplicantId,
            string Status,
            List<ExpandoObject> Candidates,
            string? EmailSend,
            string RequesterMail,
            string RequesterName,
            string RequesterPost,
            string Department,
            string Tel,
            string TelOff,
            string? Remark,
            string JobTitle,
            string TypeMail,
            string NameCon);

        private sealed record JobApprovalData(
            int JobId,
            string ApprovalStatus,
            string? Remark);

        [DllImport("mpr.dll", EntryPoint = "WNetAddConnection2W", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2(ref NetResource netResource, string? password, string? username, int flags);

        [DllImport("mpr.dll", EntryPoint = "WNetCancelConnection2W", CharSet = CharSet.Unicode)]
        private static extern int WNetCancelConnection2(string lpName, int dwFlags, [MarshalAs(UnmanagedType.Bool)] bool fForce);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NetResource
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpLocalName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpRemoteName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpComment;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpProvider;
        }

        public ApplicantNewController(
            DapperContext context,
            IEmailService emailService,
            ILogger<ApplicantNewController> logger,
            FileStorageConfig config)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _fileStorageConfig = config ?? throw new ArgumentNullException(nameof(config));

            _fileStorageConfig.EnvironmentName ??= "Development";
            string hostname = System.Net.Dns.GetHostName();
            _logger.LogInformation("Detected environment: {Environment}, Hostname: {Hostname}", _fileStorageConfig.EnvironmentName, hostname);

            bool isProduction = _fileStorageConfig.EnvironmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);

            if (isProduction)
            {
                if (string.IsNullOrEmpty(_fileStorageConfig.BasePath))
                    throw new InvalidOperationException("Production file storage path is not configured.");
                _basePath = _fileStorageConfig.BasePath;
                _username = null;
                _password = null;
                _useNetworkShare = false;
            }
            else
            {
                if (string.IsNullOrEmpty(_fileStorageConfig.BasePath))
                    throw new InvalidOperationException("File storage path is not configured.");
                _basePath = _fileStorageConfig.BasePath;
                _username = _fileStorageConfig.NetworkUsername;
                _password = _fileStorageConfig.NetworkPassword;
                _useNetworkShare = !string.IsNullOrEmpty(_basePath) && _username != null && _password != null;
            }

            _applicationFormUri = _fileStorageConfig.ApplicationFormUri
                ?? throw new InvalidOperationException("Application form URI is not configured.");

            if (!_useNetworkShare && !Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                _logger.LogInformation("Created local directory: {BasePath}", _basePath);
            }
        }

        private async Task<bool> ConnectToNetworkShareAsync()
        {
            if (!_useNetworkShare)
                return CheckLocalStorage();

            const int maxRetries = 3;
            const int retryDelayMs = 2000;

            string serverName = $"\\\\{new Uri(_basePath).Host}";
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Attempt {Attempt}/{MaxRetries}: Connecting to {BasePath}", attempt, maxRetries, _basePath);
                    DisconnectExistingConnections(serverName);
                    bool connected = AttemptNetworkConnection();
                    if (!connected)
                        continue;

                    ValidateNetworkShare();
                    _logger.LogInformation("Successfully connected to network share: {BasePath}", _basePath);
                    return true;
                }
                catch (System.ComponentModel.Win32Exception win32Ex) when (win32Ex.NativeErrorCode == 1219 && attempt < maxRetries)
                {
                    await Task.Delay(retryDelayMs);
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "Failed to connect to {BasePath} after {MaxRetries} attempts", _basePath, maxRetries);
                        throw;
                    }
                    _logger.LogWarning(ex, "Retrying after delay for {BasePath}", _basePath);
                    await Task.Delay(retryDelayMs);
                }
            }

            return false;
        }

        private bool CheckLocalStorage()
        {
            if (Directory.Exists(_basePath))
            {
                _logger.LogInformation("Using local storage at {BasePath}", _basePath);
                return true;
            }
            _logger.LogError("Local path {BasePath} does not exist or is not accessible.", _basePath);
            throw new DirectoryNotFoundException($"Local path {_basePath} is not accessible.");
        }

        private void DisconnectExistingConnections(string serverName)
        {
            DisconnectPath(_basePath);
            DisconnectPath(serverName);
        }

        private void DisconnectPath(string path)
        {
            int result = WNetCancelConnection2(path, 0, true);
            if (result != 0 && result != 1219)
            {
                var errorMessage = new System.ComponentModel.Win32Exception(result).Message;
                _logger.LogWarning("Failed to disconnect {Path}: {ErrorMessage} (Error Code: {Result})", path, errorMessage, result);
            }
            else
            {
                _logger.LogInformation("Disconnected or no connection to {Path} (Result: {Result})", path, result);
            }
        }

        private bool AttemptNetworkConnection()
        {
            NetResource netResource = new()
            {
                dwType = 1,
                lpRemoteName = _basePath,
                lpLocalName = null,
                lpProvider = null
            };

            _logger.LogInformation("Connecting to {BasePath} with username {Username}", _basePath, _username);
            int result = WNetAddConnection2(ref netResource, _password, _username, 0);
            if (result == 0)
                return true;

            var errorMessage = new System.ComponentModel.Win32Exception(result).Message;
            _logger.LogError("Failed to connect to {BasePath}: {ErrorMessage} (Error Code: {Result})", _basePath, errorMessage, result);
            if (result == 1219)
                return false;

            throw new System.ComponentModel.Win32Exception(result, $"Error connecting to network share: {errorMessage}");
        }

        private void ValidateNetworkShare()
        {
            if (!Directory.Exists(_basePath))
            {
                _logger.LogError("Network share {BasePath} does not exist or is not accessible.", _basePath);
                throw new DirectoryNotFoundException($"Network share {_basePath} is not accessible.");
            }
        }

        private void DisconnectFromNetworkShare()
        {
            if (!_useNetworkShare)
                return;

            try
            {
                string serverName = $"\\\\{new Uri(_basePath).Host}";
                _logger.LogInformation("Disconnecting from network share {BasePath} and server {ServerName}", _basePath, serverName);

                int disconnectResult = WNetCancelConnection2(_basePath, 0, true);
                if (disconnectResult != 0 && disconnectResult != 1219)
                {
                    var errorMessage = new System.ComponentModel.Win32Exception(disconnectResult).Message;
                    _logger.LogWarning("Failed to disconnect from {BasePath}: {ErrorMessage} (Error Code: {DisconnectResult})", _basePath, errorMessage, disconnectResult);
                }
                else
                {
                    _logger.LogInformation("Successfully disconnected or no existing connection to {BasePath} (Result: {DisconnectResult})", _basePath, disconnectResult);
                }

                disconnectResult = WNetCancelConnection2(serverName, 0, true);
                if (disconnectResult != 0 && disconnectResult != 1219)
                {
                    var errorMessage = new System.ComponentModel.Win32Exception(disconnectResult).Message;
                    _logger.LogWarning("Failed to disconnect from {ServerName}: {ErrorMessage} (Error Code: {DisconnectResult})", serverName, errorMessage, disconnectResult);
                }
                else
                {
                    _logger.LogInformation("Successfully disconnected or no existing connection to {ServerName} (Result: {DisconnectResult})", serverName, disconnectResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from network share {BasePath}: {Message}, StackTrace: {StackTrace}", _basePath, ex.Message, ex.StackTrace);
            }
        }

        [HttpPost("submit-application-with-filesV2")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitApplicationWithFilesV2([FromForm] IFormFileCollection files, [FromForm] string jsonData)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonData))
                    return BadRequest("JSON data is required.");

                var request = JsonSerializer.Deserialize<ExpandoObject>(jsonData);
                if (request is not IDictionary<string, object?> req || !req.TryGetValue(JobIdKey, out var jobIdObj) || jobIdObj == null)
                    return BadRequest("Invalid or missing JobID.");

                int jobId = jobIdObj is JsonElement j && j.ValueKind == JsonValueKind.Number
                    ? j.GetInt32()
                    : Convert.ToInt32(jobIdObj);

                await ConnectToNetworkShareAsync();
                try
                {
                    var fileMetadatas = await ProcessFilesAsync(files);
                    var dbResult = await SaveApplicationToDatabaseAsync(req, jobId, fileMetadatas);
                    MoveFilesToApplicantDirectory(dbResult.ApplicantId, fileMetadatas);
                    await SendEmailsAsync(req, dbResult);

                    return Ok(new { ApplicantID = dbResult.ApplicantId, Message = "Application and files submitted successfully." });
                }
                finally
                {
                    DisconnectFromNetworkShare();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON data: {Message}", ex.Message);
                return BadRequest("Invalid JSON data.");
            }
            catch (System.ComponentModel.Win32Exception win32Ex)
            {
                _logger.LogError(win32Ex, "Win32 error: {Message}, ErrorCode: {ErrorCode}", win32Ex.Message, win32Ex.NativeErrorCode);
                return StatusCode(500, new { Error = "Server error", win32Ex.Message, ErrorCode = win32Ex.NativeErrorCode });
            }
            catch (DirectoryNotFoundException dirEx)
            {
                _logger.LogError(dirEx, "Network share not accessible: {Message}", dirEx.Message);
                return StatusCode(500, new { Error = "Server error", dirEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing application: {Message}", ex.Message);
                return StatusCode(500, new { Error = "Server error", ex.Message });
            }
        }

        private async Task<List<Dictionary<string, object>>> ProcessFilesAsync(IFormFileCollection files)
        {
            var fileMetadatas = new List<Dictionary<string, object>>();
            if (files == null || files.Count == 0)
                return fileMetadatas;

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".png", ".jpg" };
            foreach (var file in files)
            {
                if (file.Length == 0)
                {
                    _logger.LogWarning("Skipping empty file: {FileName}", file.FileName);
                    continue;
                }

                var extension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                    throw new InvalidOperationException($"Invalid file type for {file.FileName}. Only PNG, JPG, PDF, DOC, and DOCX are allowed.");

                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(_basePath, fileName);
                var directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException($"Invalid directory path for: {filePath}");

                Directory.CreateDirectory(directoryPath);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                fileMetadatas.Add(new Dictionary<string, object>
                {
                    { "FilePath", filePath.Replace('\\', '/') },
                    { "FileName", fileName },
                    { "FileSize", file.Length },
                    { "FileType", file.ContentType }
                });
            }

            return fileMetadatas;
        }

        private async Task<(int ApplicantId, string ApplicantEmail, string HrManagerEmails, string JobManagerEmails, string JobTitle, string CompanyName)> SaveApplicationToDatabaseAsync(IDictionary<string, object?> req, int jobId, List<Dictionary<string, object>> fileMetadatas)
        {
            using var conn = _context.CreateConnection();
            var param = new DynamicParameters();

            string[] listKeys = ["EducationList", "WorkExperienceList", "SkillsList"];
            foreach (var key in listKeys)
            {
                param.Add(key, req.TryGetValue(key, out var val) && val is JsonElement je && je.ValueKind == JsonValueKind.Array
                    ? je.GetRawText()
                    : "[]");
            }

            param.Add("JsonInput", JsonSerializer.Serialize(req));
            param.Add("FilesList", JsonSerializer.Serialize(fileMetadatas));
            param.Add("JobID", jobId);
            param.Add("ApplicantID", dbType: DbType.Int32, direction: ParameterDirection.Output);
            param.Add("ApplicantEmail", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
            param.Add("HRManagerEmails", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);
            param.Add("JobManagerEmails", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);
            param.Add("JobTitle", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);
            param.Add("CompanyName", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

            await conn.ExecuteAsync("InsertApplicantDataV6", param, commandType: CommandType.StoredProcedure);

            return (
                param.Get<int>("ApplicantID"),
                param.Get<string>("ApplicantEmail"),
                param.Get<string>("HRManagerEmails"),
                param.Get<string>("JobManagerEmails"),
                param.Get<string>("JobTitle"),
                param.Get<string>("CompanyName")
            );
        }

        private void MoveFilesToApplicantDirectory(int applicantId, List<Dictionary<string, object>> fileMetadatas)
        {
            if (fileMetadatas.Count == 0 || applicantId <= 0)
                return;

            var applicantPath = Path.Combine(_basePath, $"applicant_{applicantId}");
            if (!Directory.Exists(applicantPath))
            {
                Directory.CreateDirectory(applicantPath);
                _logger.LogInformation("Created applicant directory: {ApplicantPath}", applicantPath);
            }
            else
            {
                foreach (var oldFile in Directory.GetFiles(applicantPath))
                {
                    System.IO.File.Delete(oldFile);
                    _logger.LogInformation("Deleted old file: {OldFile}", oldFile);
                }
            }

            foreach (var metadata in fileMetadatas)
            {
                var oldFilePath = metadata.GetValueOrDefault("FilePath")?.ToString();
                var fileName = metadata.GetValueOrDefault("FileName")?.ToString();
                if (string.IsNullOrEmpty(oldFilePath) || string.IsNullOrEmpty(fileName))
                {
                    _logger.LogWarning("Skipping file with invalid metadata: {Metadata}", JsonSerializer.Serialize(metadata));
                    continue;
                }

                var newFilePath = Path.Combine(applicantPath, fileName);
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Move(oldFilePath, newFilePath, overwrite: true);
                    _logger.LogInformation("Moved file from {OldFilePath} to {NewFilePath}", oldFilePath, newFilePath);
                }
                else
                {
                    _logger.LogWarning("File not found for moving: {OldFilePath}", oldFilePath);
                }
            }
        }

        private async Task SendEmailsAsync(IDictionary<string, object?> req, (int ApplicantId, string ApplicantEmail, string HrManagerEmails, string JobManagerEmails, string JobTitle, string CompanyName) dbResult)
        {
            var fullNameThai = GetFullName(req, "FirstNameThai", "LastNameThai");
            var jobTitle = req.TryGetValue(JobTitleKey, out var jobTitleObj) ? jobTitleObj?.ToString() ?? "-" : "-";

            using var conn = _context.CreateConnection();
            var results = await conn.QueryAsync<dynamic>("sp_GetDateSendEmailV3", new { JobID = dbResult.ApplicantId }, commandType: CommandType.StoredProcedure);
            var firstHr = results.FirstOrDefault(x => Convert.ToInt32(x.Role) == 2);

            if (!string.IsNullOrEmpty(dbResult.ApplicantEmail))
            {
                string applicantBody = GenerateEmailBody(true, dbResult.CompanyName, fullNameThai, jobTitle, firstHr);
                await _emailService.SendEmailAsync(dbResult.ApplicantEmail, "Application Received", applicantBody, true);
            }

            foreach (var x in results)
            {
                var emailStaff = (x.EMAIL ?? "").Trim();
                if (string.IsNullOrWhiteSpace(emailStaff))
                    continue;

                string managerBody = GenerateEmailBody(false, emailStaff, fullNameThai, jobTitle, null, dbResult.ApplicantId);
                await _emailService.SendEmailAsync(emailStaff, "ONEE Jobs - You've got the new candidate", managerBody, true);
            }
        }

        private static string GetFullName(IDictionary<string, object?> req, string firstNameKey, string lastNameKey)
        {
            req.TryGetValue(firstNameKey, out var firstNameObj);
            req.TryGetValue(lastNameKey, out var lastNameObj);
            return $"{firstNameObj?.ToString() ?? ""} {lastNameObj?.ToString() ?? ""}".Trim();
        }

        private string GenerateEmailBody(bool isApplicant, string recipient, string fullNameThai, string jobTitle, dynamic? hr = null, int applicantId = 0)
        {
            if (isApplicant)
            {
                string companyName = recipient;
                string hrEmail = hr?.EMAIL ?? "-";
                string hrTel = hr?.TELOFF ?? "-";
                string hrName = hr?.NAMETHAI ?? "-";
                return $@"
                    <div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; font-size: 14px; line-height: 1.6;'>
                        <p style='margin: 0; font-weight: bold;'>{companyName}: ได้รับใบสมัครงานของคุณแล้ว</p>
                        <p style='margin: 0;'>เรียน คุณ {fullNameThai}</p>
                        <p>
                            ขอบคุณสำหรับความสนใจในตำแหน่ง <strong>{jobTitle}</strong> ที่บริษัท <strong>{companyName}</strong> ของเรา<br>
                            เราได้รับใบสมัครของท่านเรียบร้อยแล้ว ทีมงานฝ่ายทรัพยากรบุคคลของเราจะพิจารณาใบสมัครของท่าน และจะติดต่อกลับภายใน 7-14 วันทำการ หากคุณสมบัติของท่านตรงตามที่เรากำลังมองหา<br><br>
                            หากมีข้อสงสัยหรือต้องการข้อมูลเพิ่มเติม สามารถติดต่อเราได้ที่อีเมล 
                            <span style='color: blue;'>{hrEmail}</span> หรือโทร 
                            <span style='color: blue;'>{hrTel}</span><br>
                            ขอบคุณอีกครั้งสำหรับความสนใจร่วมงานกับเรา
                        </p>
                        <p style='margin-top: 30px; margin:0'>ด้วยความเคารพ,</p>
                        <p style='margin: 0;'>{hrName}</p>
                        <p style='margin: 0;'>ฝ่ายทรัพยากรบุคคล</p>
                        <p style='margin: 0;'>{companyName}</p>
                        <br>
                        <p style='color:red; font-weight: bold;'>**อีเมลนี้คือข้อความอัตโนมัติ กรุณาอย่าตอบกลับ**</p>
                    </div>";
            }

            return $@"
                <div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; font-size: 14px; line-height: 1.6;'>
                    <p style='margin: 0;'>เรียนทุกท่าน</p>
                    <p style='margin: 0;'>เรื่อง: แจ้งข้อมูลผู้สมัครตำแหน่ง <strong>{jobTitle}</strong></p>
                    <p style='margin: 0;'>ทางฝ่ายรับสมัครงานขอแจ้งให้ทราบว่า คุณ <strong>{fullNameThai}</strong> ได้ทำการสมัครงานเข้ามาในตำแหน่ง <strong>{jobTitle}</strong></p>
                    <p style='margin: 0;'>กรุณาคลิก Link:
                        <a target='_blank' href='{_applicationFormUri}?id={applicantId}'
                            style='color: #007bff; text-decoration: underline;'>
                            {_applicationFormUri}
                        </a>
                        เพื่อดูรายละเอียดและดำเนินการในขั้นตอนต่อไป
                    </p>
                    <br>
                    <p style='color: red; font-weight: bold;'>**อีเมลนี้คือข้อความอัตโนมัติ กรุณาอย่าตอบกลับ**</p>
                </div>";
        }

        [HttpGet("applicant")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(typeof(IEnumerable<dynamic>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApplicants()
        {
            try
            {
                using var connection = _context.CreateConnection();
                var query = "EXEC spGetAllApplicantsWithJobDetails";
                var applicants = await connection.QueryAsync(query);

                return Ok(applicants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve applicants: {Message}", ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("GetDataOpenFor")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(typeof(IEnumerable<dynamic>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDataOpenFor()
        {
            try
            {
                using var connection = _context.CreateConnection();
                var query = "getDateOpenFor";
                var response = await connection.QueryAsync(query);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve applicants: {Message}", ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("applicantByID")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(typeof(IEnumerable<dynamic>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApplicantsById([FromQuery] int? applicantId)
        {
            try
            {
                using var connection = _context.CreateConnection();
                var parameters = new DynamicParameters();

                parameters.Add($"@{ApplicantIdKey}", applicantId);

                var query = $"EXEC spGetAllApplicantsWithJobDetailsNew @{ApplicantIdKey}";
                var applicants = await connection.QueryAsync(query, parameters);

                return Ok(applicants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve applicant by ID {ApplicantId}: {Message}", applicantId, ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("addApplicant")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> PostApplicant([FromBody] Dictionary<string, object?> payload)
        {
            try
            {
                using var connection = _context.CreateConnection();
                var jsonPayload = JsonSerializer.Serialize(payload);

                var parameters = new DynamicParameters();
                parameters.Add("@JsonInput", jsonPayload);
                parameters.Add("@InsertedApplicantID", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_InsertApplicantNew",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                int insertedId = parameters.Get<int>("@InsertedApplicantID");

                return Ok(new
                {
                    Message = "Insert success",
                    ApplicantID = insertedId,
                    Data = payload
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to insert applicant: {Message}", ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("GetCandidate")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(typeof(IEnumerable<dynamic>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFilteredCandidates([FromQuery] string? department, [FromQuery] int? jobId)
        {
            try
            {
                using var connection = _context.CreateConnection();

                var parameters = new DynamicParameters();
                parameters.Add("@Department", department);
                parameters.Add("@JobID", jobId);

                var result = await connection.QueryAsync(
                    "sp_GetCandidateAll",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve candidates for department {Department} and job ID {JobId}: {Message}", department, jobId, ex.Message);
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("GetCandidateData")]
        //[TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(typeof(IEnumerable<dynamic>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApplicantData([FromQuery] int? id, int? userId)
        {
            try
            {
                using var connection = _context.CreateConnection();

                var parameters = new DynamicParameters();
                parameters.Add($"@{ApplicantIdKey}", id);
                parameters.Add($"@{UserIdKey}", userId);
                var result = await connection.QueryAsync(
                    "sp_GetApplicantDataV2",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve applicant data for ID {ApplicantId}: {Message}", id, ex.Message);
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPut("updateApplicantStatus")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateApplicantStatus([FromBody] ExpandoObject? request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogWarning("Request is null in UpdateApplicantStatus");
                    return BadRequest("Request cannot be null.");
                }

                var data = (IDictionary<string, object?>)request;
                var validationResult = ValidateInput(data);
                if (validationResult != null)
                    return validationResult;
                var requestData = ExtractRequestData(data);
                if (requestData == null)
                    return BadRequest("Invalid ApplicantID or Status format.");

                var typeMail = requestData.TypeMail;

                if (typeMail == "Hire")
                {
                    int emailSuccessCount = await SendHireToHrEmails(requestData);
                }
                else if (typeMail == "Selected")
                {
                    int emailSuccessCount = await SendHrEmails(requestData);
                }
                else if (typeMail == "notiMail")
                {
                    int emailSuccessCount = await SendMailNoti(requestData);
                }

                if (typeMail != "notiMail") {
                    await UpdateStatusInDatabaseV2(requestData);
                }
                return Ok(new { message = "อัปเดตสถานะเรียบร้อย" });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating applicant status: {Message}", ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private BadRequestObjectResult? ValidateInput(IDictionary<string, object?> data)
        {
            if (!data.ContainsKey(ApplicantIdKey) || !data.ContainsKey("Status"))
            {
                _logger.LogWarning("Missing required fields in request: ApplicantID or Status");
                return new BadRequestObjectResult("Missing required fields: ApplicantID or Status");
            }

            if (!data.TryGetValue(ApplicantIdKey, out object? applicantIdValue) || applicantIdValue == null ||
                !data.TryGetValue("Status", out object? statusValue) || statusValue == null)
            {
                _logger.LogWarning("Invalid or null values for ApplicantID or Status");
                return new BadRequestObjectResult("Invalid or null values for ApplicantID or Status");
            }

            return null;
        }

        private ApplicantRequestData? ExtractRequestData(IDictionary<string, object?> data)
        {
            if (data[ApplicantIdKey] is not JsonElement applicantIdElement || applicantIdElement.ValueKind != JsonValueKind.Number ||
                data["Status"] is not JsonElement statusElement || statusElement.ValueKind != JsonValueKind.String)
            {
                _logger.LogWarning("ApplicantID must be an integer and Status must be a string");
                return null;
            }

            int applicantId = applicantIdElement.GetInt32();
            string status = statusElement.GetString()!;

            List<ExpandoObject> candidates = ExtractCandidates(data);

            string? emailSend = data.TryGetValue("EmailSend", out object? emailSendObj) &&
                               emailSendObj is JsonElement emailSendElement &&
                               emailSendElement.ValueKind == JsonValueKind.String
                ? emailSendElement.GetString()
                : null;

            string requesterMail = data.TryGetValue("Email", out object? mailObj) ? mailObj?.ToString() ?? "-" : "-";
            string requesterName = data.TryGetValue("NAMETHAI", out object? nameObj) ? nameObj?.ToString() ?? "-" : "-";
            string requesterPost = data.TryGetValue("POST", out object? postObj) ? postObj?.ToString() ?? "-" : "-";
            string tel = data.TryGetValue("Mobile", out object? telObj) ? telObj?.ToString() ?? "-" : "-";
            string telOff = data.TryGetValue("TELOFF", out object? telOffObj) ? telOffObj?.ToString() ?? "-" : "-";
            string TypeMail = data.TryGetValue("TypeMail", out object? TypeMailObj) ? TypeMailObj?.ToString() ?? "-" : "-";
            string Department = data.TryGetValue("Department", out object? DepartmentObj) ? DepartmentObj?.ToString() ?? "-" : "-";
            string NameCon = data.TryGetValue("NameCon", out object? NameConObj) ? NameConObj?.ToString() ?? "-" : "-";
            string? Remark = data.TryGetValue("Remark", out object? remarkObj) &&
                            remarkObj is JsonElement remarkElement &&
                            remarkElement.ValueKind == JsonValueKind.String
                ? remarkElement.GetString()
                : null;

            string jobTitle = data.TryGetValue(JobTitleKey, out object? jobTitleObj) &&
                              jobTitleObj is JsonElement jobTitleElement &&
                              jobTitleElement.ValueKind == JsonValueKind.String
                ? jobTitleElement.GetString() ?? "-"
                : "-";

            return new ApplicantRequestData(
                applicantId,
                status,
                candidates,
                emailSend,
                requesterMail,
                requesterName,
                requesterPost,
                Department,
                tel,
                telOff,
                Remark,
                jobTitle,
                TypeMail,
                NameCon);
        }

        private List<ExpandoObject> ExtractCandidates(IDictionary<string, object?> data)
        {
            if (!data.TryGetValue("Candidates", out object? candidatesObj) || candidatesObj == null)
                return [];

            string? candidatesJson = candidatesObj.ToString();
            if (string.IsNullOrEmpty(candidatesJson))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<ExpandoObject>>(candidatesJson) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize Candidates JSON: {Message}", ex.Message);
                return [];
            }
        }

        private async Task UpdateStatusInDatabase(int applicantId, string status)
        {
            using var connection = _context.CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("@ApplicantID", applicantId);
            parameters.Add("@Status", status);

            await connection.ExecuteAsync(
                "sp_UpdateApplicantStatus",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        private async Task UpdateStatusInDatabaseV2(ApplicantRequestData requestData)
        {
            using var connection = _context.CreateConnection();
            var parameters = new DynamicParameters();

            parameters.Add("@ApplicantID", requestData.ApplicantId);
            parameters.Add("@Status", requestData.Status ?? "");
            if (!string.IsNullOrWhiteSpace(requestData.Remark))
            {
                parameters.Add("@Remark", requestData.Remark);
            }

            await connection.ExecuteAsync(
                "sp_UpdateApplicantStatusV2",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        private async Task<int> SendHireToHrEmails(ApplicantRequestData requestData)
        {
            var candidateNames = requestData.Candidates?
                .Select((candidateObj, index) =>
                {
                    var candidateDict = candidateObj as IDictionary<string, object>;
                    string title = candidateDict.TryGetValue("title", out var titleObj) ? titleObj?.ToString() ?? "" : "";
                    string firstNameThai = candidateDict.TryGetValue("FirstNameThai", out var firstNameObj) ? firstNameObj?.ToString() ?? "" : "";
                    string lastNameThai = candidateDict.TryGetValue("LastNameThai", out var lastNameObj) ? lastNameObj?.ToString() ?? "" : "";
                    return $"ลำดับที่ {index + 1}: {title} {firstNameThai} {lastNameThai}".Trim();
                }).ToList() ?? [];

            string candidateNamesString = string.Join("<br>", candidateNames);

            string Tel = requestData.Tel ?? "-";

            string hrBody = $@"
                <div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; font-size: 14px;'>
                    <p style='font-weight: bold; margin: 0 0 10px 0;'>เรียน คุณสมศรี (ผู้จัดการฝ่ายบุคคล)</p>
                    <br>
                    <p style='margin: 0 0 10px 0;'>
                        เรียน ฝ่ายสารรหาบุคคลากร<br>
                        ทาง Hiring Manager แผนก {requestData.NameCon} <br> คุณ {requestData.RequesterName} เบอร์โทร: {Tel} อีเมล: {requestData.RequesterMail} <br> 
                        มีการส่งคำร้องให้ท่าน ทำการติดต่อผู้สมัครเพื่อตกลงการจ้างงาน ในตำแหน่ง {requestData.JobTitle}
                    </p>
                    <p style='margin: 0 0 10px 0;'>
                        โดยมี ลำดับรายชื่อการติดต่อดังนี้ <br> {candidateNamesString}
                    </p>
                    <br>
                    
                    <p style='margin: 0 0 10px 0;'><span style='color: red; font-weight: bold;'>*</span> โดยให้ทำการติดต่อ ผู้มัครลำดับที่ 1 ก่อน หากเจรจาไม่สสำเร็จ ให้ทำการติดต่อกับผู้มัครลำดับต่อไป <span style='color: red; font-weight: bold;'>*</span></p>
                    <p style='margin: 0 0 10px 0;'><span style='color: red; font-weight: bold;'>*</span> กรุณา Login เข้าสู่ระบบ https://oneejobs.oneeclick.co:7191/LoginAdmin และไปที่ Menu การว่าจ้าง เพื่อตอบกลับคำขอนี้ <span style='color: red; font-weight: bold;'>*</span></p>
                    <br>
                    <p style='color: red; font-weight: bold;'>**Email อัตโนมัติ โปรดอย่าตอบกลับ**</p>
                </div>";
            using var connection = _context.CreateConnection();
            var emailParameters = new DynamicParameters();
            emailParameters.Add("@Role", 2);
            emailParameters.Add("@Department", null);

            var staffList = await connection.QueryAsync<dynamic>(
                "EXEC sp_GetDateSendEmail @Role = @Role, @Department = @Department",
                emailParameters);

            int successCount = 0;
            foreach (var staff in staffList)
            {
                string? hrEmail = staff.EMAIL?.Trim();
                if (string.IsNullOrWhiteSpace(hrEmail))
                    continue;

                try
                {
                    await _emailService.SendEmailAsync(hrEmail, "ONEE Jobs - List of candidates for job interview", hrBody, true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to {HrEmail} for applicant status update: {Message}", hrEmail, ex.Message);
                }
            }

            return successCount;
        }

        private async Task<int> SendHrEmails(ApplicantRequestData requestData)
        {
            var candidateNames = requestData.Candidates?.Select(candidateObj =>
            {
                var candidateDict = candidateObj as IDictionary<string, object>;
                string title = candidateDict.TryGetValue("title", out var titleObj) ? titleObj?.ToString() ?? "" : "";
                string firstNameThai = candidateDict.TryGetValue("firstNameThai", out var firstNameObj) ? firstNameObj?.ToString() ?? "" : "";
                string lastNameThai = candidateDict.TryGetValue("lastNameThai", out var lastNameObj) ? lastNameObj?.ToString() ?? "" : "";
                return $"{title} {firstNameThai} {lastNameThai}".Trim();
            }).ToList() ?? [];

            string candidateNamesString = string.Join(" ", candidateNames);

            string hrBody = $@"
                <div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; font-size: 14px;'>
                    <p style='font-weight: bold; margin: 0 0 10px 0;'>เรียน คุณสมศรี (ผู้จัดการฝ่ายบุคคล)</p>
                    <p style='font-weight: bold; margin: 0 0 10px 0;'>เรื่อง: การเรียกสัมภาษณ์ผู้สมัครตำแหน่ง {requestData.JobTitle}</p>
                    <br>
                    <p style='margin: 0 0 10px 0;'>
                        เรียน ฝ่ายบุคคล<br>
                        ตามที่ได้รับแจ้งข้อมูลผู้สมัครในตำแหน่ง {requestData.JobTitle} จำนวน {candidateNames.Count} ท่าน ผมได้พิจารณาประวัติและคุณสมบัติเบื้องต้นแล้ว และประสงค์จะขอเรียกผู้สมัครดังต่อไปนี้เข้ามาสัมภาษณ์
                    </p>
                    <p style='margin: 0 0 10px 0;'>
                        จากข้อมูลผู้สมัคร ดิฉัน/ผมเห็นว่า {candidateNamesString} มีคุณสมบัติที่เหมาะสมกับตำแหน่งงาน และมีความเชี่ยวชาญในทักษะที่จำเป็นต่อการทำงานในทีมของเรา
                    </p>
                    <br>
                    <p style='margin: 0 0 10px 0;'>ขอความกรุณาฝ่ายบุคคลประสานงานกับผู้สมัครเพื่อนัดหมายการสัมภาษณ์</p>
                    <p style='margin: 0 0 10px 0;'>หากท่านมีข้อสงสัยประการใด กรุณาติดต่อได้ที่เบอร์ด้านล่าง</p>
                    <p style='margin: 0 0 10px 0;'>ขอบคุณสำหรับความช่วยเหลือ</p>
                    <p style='margin: 0 0 10px 0;'>ขอแสดงความนับถือ</p>
                    <p style='margin: 0 0 10px 0;'>{requestData.RequesterName}</p>
                    <p style='margin: 0 0 10px 0;'>{requestData.RequesterPost}</p>
                    <p style='margin: 0 0 10px 0;'>โทร: {requestData.Tel} ต่อ {requestData.TelOff}</p>
                    <p style='margin: 0 0 10px 0;'>อีเมล: {requestData.RequesterMail}</p>
                    <br>
                    <p style='color: red; font-weight: bold;'>**อีเมลนี้เป็นข้อความอัตโนมัติ กรุณาอย่าตอบกลับ**</p>
                </div>";

            using var connection = _context.CreateConnection();
            var emailParameters = new DynamicParameters();
            emailParameters.Add("@Role", 2);
            emailParameters.Add("@Department", null);

            var staffList = await connection.QueryAsync<dynamic>(
                "EXEC sp_GetDateSendEmail @Role = @Role, @Department = @Department",
                emailParameters);

            int successCount = 0;
            foreach (var staff in staffList)
            {
                string? hrEmail = staff.EMAIL?.Trim();
                if (string.IsNullOrWhiteSpace(hrEmail))
                    continue;

                try
                {
                    await _emailService.SendEmailAsync(hrEmail, "ONEE Jobs - List of candidates for job interview", hrBody, true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to {HrEmail} for applicant status update: {Message}", hrEmail, ex.Message);
                }
            }

            return successCount;
        }

        private async Task<int> SendMailNoti(ApplicantRequestData requestData)
        {
            var candidateNames = requestData.Candidates?.Select(candidateObj =>
            {
                var candidateDict = candidateObj as IDictionary<string, object>;
                string title = candidateDict.TryGetValue("title", out var titleObj) ? titleObj?.ToString() ?? "" : "";
                string firstNameThai = candidateDict.TryGetValue("firstNameThai", out var firstNameObj) ? firstNameObj?.ToString() ?? "" : "";
                string lastNameThai = candidateDict.TryGetValue("lastNameThai", out var lastNameObj) ? lastNameObj?.ToString() ?? "" : "";
                return $"{title} {firstNameThai} {lastNameThai}".Trim();
            }).ToList() ?? [];

            string fromRegis = "https://oneejobs.oneeclick.co:7191/login?jobId=51&com";

            string reqBody = $@"
                <div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; font-size: 14px;'>
                    <p style='font-weight: bold; margin: 0 0 10px 0;'>เรียน {requestData.RequesterName}</p>
                    <p style='font-weight: bold; margin: 0 0 10px 0;'>เรื่อง: ผลสัมภาษณ์ผู้สมัครตำแหน่ง {requestData.JobTitle}</p>
                    <br>
                    <p style='margin: 0 0 10px 0;'>
                        ตามที่ท่านได้สมัครในตำแหน่ง {requestData.JobTitle} ทางบริษัทได้พิจณาให้คุณผ่านการคัดเลือก กรุณาเข้าไปกรอกรายละเอียดของท่าน ตามลิงก์ด้านล่าง
                    </p>
                    <p style='margin: 0 0 10px 0;'>
                        Clik : {fromRegis}
                    </p>
                    <br>
                    <p style='color: red; font-weight: bold;'>**อีเมลนี้เป็นข้อความอัตโนมัติ กรุณาอย่าตอบกลับ**</p>
                </div>";

            using var connection = _context.CreateConnection();
            int successCount = 0;
            try
            {
                await _emailService.SendEmailAsync(requestData.RequesterMail, "ONEE Jobs - List of selected candidates", reqBody, true);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {RequesterMail} for applicant status update: {Message}", requestData.RequesterMail, ex.Message);
            }

            return successCount;

        }

        [HttpPut("updateJobApprovalStatus")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateJobApprovalStatus([FromBody] ExpandoObject? request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogWarning("Request is null in UpdateJobApprovalStatus");
                    return BadRequest("Request cannot be null.");
                }

                var data = (IDictionary<string, object?>)request;
                var validationResult = ValidateJobApprovalInput(data);
                if (validationResult != null)
                    return validationResult;

                var approvalData = ExtractJobApprovalData(data);
                if (approvalData == null)
                    return BadRequest("Invalid JobID or ApprovalStatus format.");

                await UpdateJobApprovalInDatabase(approvalData);

                return Ok(new { message = "อัปเดตสถานะของงานเรียบร้อย" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating job approval status: {Message}", ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private BadRequestObjectResult? ValidateJobApprovalInput(IDictionary<string, object?> data)
        {
            if (!data.ContainsKey(JobIdKey) || !data.ContainsKey("ApprovalStatus") || !data.ContainsKey("Remark"))
            {
                _logger.LogWarning("Missing required fields in request: JobID, ApprovalStatus, or Remark");
                return new BadRequestObjectResult("Missing required fields: JobID, ApprovalStatus, or Remark");
            }

            if (!data.TryGetValue(JobIdKey, out object? jobIdObj) || jobIdObj == null ||
                !data.TryGetValue("ApprovalStatus", out object? approvalStatusObj) || approvalStatusObj == null)
            {
                _logger.LogWarning("Invalid or null values for JobID or ApprovalStatus");
                return new BadRequestObjectResult("Invalid or null values for JobID or ApprovalStatus");
            }

            return null;
        }

        private JobApprovalData? ExtractJobApprovalData(IDictionary<string, object?> data)
        {
            if (data[JobIdKey] is not JsonElement jobIdElement || jobIdElement.ValueKind != JsonValueKind.Number ||
                data["ApprovalStatus"] is not JsonElement approvalStatusElement || approvalStatusElement.ValueKind != JsonValueKind.String)
            {
                _logger.LogWarning("JobID must be an integer and ApprovalStatus must be a string");
                return null;
            }

            int jobId = jobIdElement.GetInt32();
            string approvalStatus = approvalStatusElement.GetString()!;

            if (jobId == 0 || string.IsNullOrEmpty(approvalStatus))
            {
                _logger.LogWarning("JobID or ApprovalStatus cannot be null or invalid");
                return null;
            }

            string? remark = data.TryGetValue("Remark", out object? remarkObj) &&
                            remarkObj is JsonElement remarkElement &&
                            remarkElement.ValueKind == JsonValueKind.String
                ? remarkElement.GetString()
                : null;

            return new JobApprovalData(jobId, approvalStatus, remark);
        }

        private async Task UpdateJobApprovalInDatabase(JobApprovalData approvalData)
        {
            using var connection = _context.CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("@JobID", approvalData.JobId);
            parameters.Add("@ApprovalStatus", approvalData.ApprovalStatus);
            parameters.Add("@Remark", approvalData.Remark);

            await connection.ExecuteAsync(
                "EXEC sp_UpdateJobApprovalStatus @JobID, @ApprovalStatus, @Remark",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        [HttpGet("GetPDPAContent")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPDPAContent()
        {
            try
            {
                using var connection = _context.CreateConnection();

                var result = await connection.QueryFirstOrDefaultAsync(
                    "sp_GetDataPDPA",
                    commandType: CommandType.StoredProcedure
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PDPA content: {Message}", ex.Message);
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPut("UpdateConfirmConsent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateConfirmConsent([FromBody] ExpandoObject? request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogWarning("Request is null in UpdateConfirmConsent");
                    return BadRequest("Request cannot be null.");
                }

                var data = (IDictionary<string, object?>)request;
                var validationResult = ValidateConsentInput(data);
                if (validationResult != null)
                    return validationResult;

                var (userId, confirmConsent) = ExtractConsentData(data);
                if (userId == 0)
                    return BadRequest("Invalid UserId format.");
                if (string.IsNullOrEmpty(confirmConsent))
                    return BadRequest("ConfirmConsent cannot be null or empty.");

                using var connection = _context.CreateConnection();
                var parameters = new DynamicParameters();
                parameters.Add("@UserId", userId);
                parameters.Add("@ConfirmConsent", confirmConsent);
                var query = "EXEC UpdateUserConsent @UserId, @ConfirmConsent";
                var result = await connection.QuerySingleOrDefaultAsync<dynamic>(query, parameters);

                if (result == null)
                    return NotFound("User not found or update failed.");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating consent for user ID {UserId}: {Message}", User, ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private BadRequestObjectResult? ValidateConsentInput(IDictionary<string, object?> data)
        {
            if (!data.ContainsKey("UserId") || !data.ContainsKey("confirmConsent"))
            {
                _logger.LogWarning("Missing required fields in request: UserId or ConfirmConsent");
                return BadRequest("Missing required fields: UserId or ConfirmConsent");
            }

            if (!data.TryGetValue("UserId", out var userIdObj) || userIdObj == null ||
                !data.TryGetValue("confirmConsent", out var _))
            {
                _logger.LogWarning("Invalid or null values for UserId or ConfirmConsent");
                return BadRequest("Invalid or null values for UserId or ConfirmConsent");
            }

            return null;
        }

        private static (int UserId, string? ConfirmConsent) ExtractConsentData(IDictionary<string, object?> data)
        {
            int userId = 0;
            string? confirmConsent = null;

            if (data["confirmConsent"] is JsonElement confirmConsentElement &&
                confirmConsentElement.ValueKind == JsonValueKind.String)
            {
                confirmConsent = confirmConsentElement.GetString() ?? string.Empty;
            }

            if (data["UserId"] is JsonElement userIdElement)
            {
                if (userIdElement.ValueKind == JsonValueKind.Number)
                {
                    userId = userIdElement.GetInt32();
                }
                else if (int.TryParse(userIdElement.GetString(), out var id))
                {
                    userId = id;
                }
            }

            return (userId, confirmConsent);
        }
    }
}