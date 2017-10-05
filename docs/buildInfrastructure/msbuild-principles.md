MSBuild Principles
==================

### `ProjectReference`

All projects in Microsoft.AspNetCore.sln must implement the [Project Reference Protocol](https://github.com/Microsoft/msbuild/blob/v15.3.409.57025/documentation/ProjectReference-Protocol.md).
In a normal MSBuild project (one that compiles .NET assemblies), the files passed around using this protocol are \*.dll files. In our implementation, we pass other types of files, such as complete NuGet packages, \*.zip and \*.tar.gz files, etc. These items are called `Artifacts`/

### `Artifact`

Artifacts describe a file that is produced and must always contain the metadata `Type`. This is used for consuming projects to determine how to consume the file.

**ItemSpec** = The file path

#### Well-known metadata
  - PackageId (for NuGet packages)
  - Version
  - Type
    - NuGetPackage
    - NuGetSymbolsPackage
    - ZipArchive
    - TarballArchive
