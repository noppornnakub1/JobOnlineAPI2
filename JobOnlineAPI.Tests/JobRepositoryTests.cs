using Moq;
using Xunit;
using JobOnlineAPI.Repositories;
using JobOnlineAPI.Models;
using JobOnlineAPI.Services;

namespace JobOnlineAPI.JobOnlineAPI.Tests
{
    public class JobRepositoryTests
    {
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly JobRepository _repository;

        public JobRepositoryTests()
        {
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c.GetConnectionString("DefaultConnection"))
                .Returns("Server=localhost;Database=TestDB;Trusted_Connection=True;");

            _emailServiceMock = new Mock<IEmailService>();
            _repository = new JobRepository(_configurationMock.Object, _emailServiceMock.Object);
        }

        [Fact]
        public async Task AddJobAsync_ValidJob_ReturnsJobId()
        {
            var job = new Job
            {
                JobTitle = "Developer",
                JobDescription = "Code stuff",
                Requirements = "C#",
                Location = "Test",
                ExperienceYears = "3",
                NumberOfPositions = 1,
                Department = "IT",
                JobStatus = "Open",
                CreatedByRole = "HR"
            };

            _emailServiceMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.CompletedTask);

            var jobId = await _repository.AddJobAsync(job);

            Assert.True(jobId > 0);
        }
    }
}