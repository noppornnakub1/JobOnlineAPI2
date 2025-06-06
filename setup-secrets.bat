@echo off
echo Setting up User Secrets for JobOnlineAPI...
cd /d %~dp0
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=10.10.0.23;Database=JobOnlineDB;User Id=JobOnlineUser;Password=%1;Trusted_Connection=False;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:DefaultConnectionHRMS" "Server=10.10.0.23;Database=HRMS;User Id=JobOnlineUser;Password=%1;Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True;"
echo User Secrets configured successfully.
pause