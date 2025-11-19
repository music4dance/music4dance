# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade DanceLib\DanceLibrary.csproj
4. Upgrade m4dModels\m4dModels.csproj
5. Upgrade m4d\m4d.csproj
6. Upgrade m4d.Tests\m4d.Tests.csproj
7. Upgrade SelfCrawler\SelfCrawler.csproj
8. Upgrade m4dModels.Tests\m4dModels.Tests.csproj
9. Upgrade DanceTests\DanceLibrary.Tests.csproj
10. Run unit tests to validate upgrade in the projects listed below:
   - DanceTests\DanceLibrary.Tests.csproj
   - m4dModels.Tests\m4dModels.Tests.csproj
   - m4d.Tests\m4d.Tests.csproj

## Settings

This section contains settings and data used by execution steps.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                                           | Current Version | New Version           | Description                                            |
|:-------------------------------------------------------|:---------------:|:---------------------:|:-------------------------------------------------------|
| CoreWCF.ConfigurationManager                           |                 | 1.8.0                 | Replacement for System.ServiceModel packages           |
| CoreWCF.Http                                           |                 | 1.8.0                 | Replacement for System.ServiceModel packages           |
| CoreWCF.NetTcp                                         |                 | 1.8.0                 | Replacement for System.ServiceModel packages           |
| CoreWCF.Primitives                                     |                 | 1.8.0                 | Replacement for System.ServiceModel packages           |
| CoreWCF.WebHttp                                        |                 | 1.8.0                 | Replacement for System.ServiceModel packages           |
| Microsoft.AspNetCore.Authentication.Facebook           | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.AspNetCore.Authentication.Google             | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore      | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.AspNetCore.Identity.UI                       | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson                | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.EntityFrameworkCore.InMemory                 | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.EntityFrameworkCore.SqlServer                | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.EntityFrameworkCore.Tools                    | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.Extensions.Configuration                     | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.Extensions.Configuration.Abstractions        | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.Extensions.Configuration.EnvironmentVariables| 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.Extensions.Configuration.UserSecrets         | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.Extensions.Logging.AzureAppServices          | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.Extensions.Logging.Debug                     | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| Microsoft.VisualStudio.Web.CodeGeneration.Design       | 9.0.0           | 10.0.0-rc.1.25458.5   | Recommended for .NET 10.0                              |
| System.Drawing.Common                                  | 9.0.9           | 10.0.0                | Recommended for .NET 10.0                              |
| System.ServiceModel.Duplex                             | 6.0.0           |                       | Replace with CoreWCF packages                          |
| System.ServiceModel.Http                               | 8.1.2           |                       | Replace with CoreWCF packages                          |
| System.ServiceModel.NetTcp                             | 8.1.2           |                       | Replace with CoreWCF packages                          |
| System.ServiceModel.Primitives                         | 8.1.2           |                       | Replace with CoreWCF packages                          |
| System.ServiceModel.Security                           | 6.0.0           |                       | Replace with CoreWCF packages                          |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### DanceLib\DanceLibrary.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`
  - Add `<ImplicitUsings>enable</ImplicitUsings>` for consistency with other projects

Other changes:
  - Remove redundant using statements from C# files that are now automatically included (System, System.Collections.Generic, System.Linq, System.Threading.Tasks, etc.)

#### m4dModels\m4dModels.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`
  - Add `<ImplicitUsings>enable</ImplicitUsings>` for consistency with other projects

NuGet packages changes:
  - Microsoft.AspNetCore.Identity.EntityFrameworkCore should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.EntityFrameworkCore.SqlServer should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.EntityFrameworkCore.Tools should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)

Other changes:
  - Remove redundant using statements from C# files that are now automatically included (System, System.Collections.Generic, System.Linq, System.Threading.Tasks, etc.)

#### m4d\m4d.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.AspNetCore.Authentication.Facebook should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.AspNetCore.Authentication.Google should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.AspNetCore.Identity.UI should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.AspNetCore.Mvc.NewtonsoftJson should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.EntityFrameworkCore.SqlServer should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.EntityFrameworkCore.Tools should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Logging.AzureAppServices should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Logging.Debug should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.VisualStudio.Web.CodeGeneration.Design should be updated from `9.0.0` to `10.0.0-rc.1.25458.5` (*recommended for .NET 10.0*)
  - System.Drawing.Common should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - System.ServiceModel.Duplex should be removed and replaced with CoreWCF packages (version 1.8.0)
  - System.ServiceModel.Http should be removed and replaced with CoreWCF packages (version 1.8.0)
  - System.ServiceModel.NetTcp should be removed and replaced with CoreWCF packages (version 1.8.0)
  - System.ServiceModel.Primitives should be removed and replaced with CoreWCF packages (version 1.8.0)
  - System.ServiceModel.Security should be removed and replaced with CoreWCF packages (version 1.8.0)

Other changes:
  - Update `using System.ServiceModel;` to `using CoreWCF;` in files that use WCF functionality

#### m4d.Tests\m4d.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Configuration should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration.Abstractions should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)

#### SelfCrawler\SelfCrawler.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Configuration should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration.EnvironmentVariables should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration.UserSecrets should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)

#### m4dModels.Tests\m4dModels.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.EntityFrameworkCore.InMemory should be updated from `9.0.9` to `10.0.0` (*recommended for .NET 10.0*)

#### DanceTests\DanceLibrary.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`