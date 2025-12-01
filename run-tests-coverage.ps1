Remove-Item -Recurse -Force '.\APLabApp.Tests\TestResults\*' -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force '.\coverage-report' -ErrorAction SilentlyContinue

dotnet test .\APLabApp.Tests\APLabApp.Tests.csproj --collect "XPlat Code Coverage"

reportgenerator `
  -reports:'APLabApp.Tests\TestResults\*\coverage.cobertura.xml' `
  -targetdir:'coverage-report' `
  -reporttypes:'Html'
