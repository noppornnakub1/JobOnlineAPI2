using System.Data;
using Dapper;
using JobOnlineAPI.Models;
using Microsoft.Data.SqlClient;

namespace JobOnlineAPI.Repositories
{
    public class JobApplicationRepository(IConfiguration configuration) : IJobApplicationRepository
    {
        private readonly string _connectionString = configuration?.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' is not found.");

        public IDbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<IEnumerable<JobApplication>> GetAllJobApplicationsAsync()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string sql = "SELECT * FROM JobApplications";
            return await db.QueryAsync<JobApplication>(sql);
        }

        public async Task<JobApplication> GetJobApplicationByIdAsync(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string sql = "SELECT * FROM JobApplications WHERE ApplicationID = @Id";
            var jobApplication = await db.QueryFirstOrDefaultAsync<JobApplication>(sql, new { Id = id });
            return jobApplication ?? throw new InvalidOperationException($"No job application found with ID {id}");
        }

        public async Task<int> AddJobApplicationAsync(JobApplication jobApplication)
        {
            ArgumentNullException.ThrowIfNull(jobApplication);
            if (jobApplication.ApplicantID <= 0 || jobApplication.JobID <= 0)
            {
                throw new ArgumentException("ApplicantID and JobID must be positive integers.");
            }
            if (string.IsNullOrEmpty(jobApplication.Status))
            {
                jobApplication.Status = "Pending";
            }
            if (jobApplication.SubmissionDate == default)
            {
                jobApplication.SubmissionDate = DateTime.UtcNow;
            }

            return await ExecuteInsertJobApplicationAsync(jobApplication);
        }

        private async Task<int> ExecuteInsertJobApplicationAsync(JobApplication jobApplication)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string sql = @"
                INSERT INTO JobApplications (ApplicantID, JobID, Status, SubmissionDate)
                VALUES (@ApplicantID, @JobID, @Status, @SubmissionDate);
                SELECT CAST(SCOPE_IDENTITY() AS int)";
            return await db.QuerySingleAsync<int>(sql, jobApplication);
        }

        public async Task<int> UpdateJobApplicationAsync(JobApplication jobApplication)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string sql = @"
                UPDATE JobApplications
                SET ApplicantID = @ApplicantID,
                    JobID = @JobID,
                    Status = @Status,
                    SubmissionDate = @SubmissionDate,
                    InterviewDate = @InterviewDate,
                    Result = @Result
                WHERE ApplicationID = @ApplicationID";
            return await db.ExecuteAsync(sql, jobApplication);
        }

        public async Task<int> DeleteJobApplicationAsync(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string sql = "DELETE FROM JobApplications WHERE ApplicationID = @Id";
            return await db.ExecuteAsync(sql, new { Id = id });
        }
    }
}