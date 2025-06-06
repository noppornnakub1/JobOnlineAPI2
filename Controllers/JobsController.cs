using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.Models;
using JobOnlineAPI.Repositories;
using JobOnlineAPI.Filters;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController(IJobRepository jobRepository) : ControllerBase
    {
        private readonly IJobRepository _jobRepository = jobRepository;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Job>>> GetAllJobs()
        {
            var jobs = await _jobRepository.GetAllJobsAsync();
            return Ok(jobs);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Job>> GetJobById(int id)
        {
            var job = await _jobRepository.GetJobByIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }
            return Ok(job);
        }

        [HttpPost]
        public async Task<ActionResult<Job>> AddJob(Job job)
        {
            if (job == null)
            {
                return BadRequest();
            }

            int newId = await _jobRepository.AddJobAsync(job);
            job.JobID = newId;
            return CreatedAtAction(nameof(GetJobById), new { id = newId }, job);
        }

        [HttpPut("{id}")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        public async Task<IActionResult> UpdateJob(int id, Job job)
        {
            if (id != job.JobID)
            {
                return BadRequest("Job ID mismatch.");
            }

            var existingJob = await _jobRepository.GetJobByIdAsync(id);
            if (existingJob == null)
            {
                return NotFound("Job not found.");
            }

            int rowsAffected = await _jobRepository.UpdateJobAsync(job);
            if (rowsAffected <= 0)
            {
                return StatusCode(500, "Update failed.");
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        public async Task<IActionResult> DeleteJob(int id)
        {
            var existingJob = await _jobRepository.GetJobByIdAsync(id);
            if (existingJob == null)
            {
                return NotFound();
            }

            await _jobRepository.DeleteJobAsync(id);
            return NoContent();
        }
    }
}