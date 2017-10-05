$patching = @('Antiforgery',
'AzureIntegration',
'BasicMiddleware',
'BrowserLink',
'CORS',
'DataProtection',
'Diagnostics',
# 'EntityFrameworkCore',
'Hosting',
'HttpAbstractions',
'HttpSysServer',
'Identity',
'IISIntegration',
'JavaScriptServices',
'KestrelHttpServer',
'Localization',
'MetaPackages',
'Mvc',
'MvcPrecompilation',
'Proxy',
'Razor',
'ResponseCaching',
'Routing',
'Scaffolding',
'Security',
'ServerTests',
'Session',
'StaticFiles',
'WebSockets')

$notpatching = @('Caching',
'Common',
'Configuration',
'DependencyInjection',
'DotNetTools',
'EventNotification',
'FileSystem',
'HtmlAbstractions',
'JsonPatch',
'Logging',
'Microsoft.Data.Sqlite',
'Options',
'Testing')

$all = $notpatching + $patching


$dbtargets = @"
<Project>
  <Import Project="build\sources.props" />
  <Import Project="build\dependencies.props" />
</Project>
"@

$emptyconfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!-- Restore sources should be defined in build/sources.props. -->
  </packageSources>
</configuration>
"@

function exec($_cmd) {
    & $_cmd @args
    if ($lastexitcode -ne 0) {
        throw 'non-zero code'
    }
}

$okfeeds = @(
    'https://dotnet.myget.org/F/aspnet-2-0-2-october2017-patch/api/v3/index.json',
    'https://dotnet.myget.org/F/aspnetcore-master/api/v3/index.json',
    'https://dotnet.myget.org/F/aspnetcore-tools/api/v3/index.json',
    'https://api.nuget.org/v3/index.json'
)

$all | % {
    $repo = $_
    Write-Host -ForegroundColor Magenta $repo
    pushd $repo
    try {
        # exec git ck -- Directory.Build.targets
        # set-content -Encoding utf8 Directory.Build.targets $dbtargets
        # exec git add Directory.Build.targets
        # exec git --no-pager diffc
        # set-content -Encoding utf8 NuGet.config $emptyconfig
        # exec git add NuGet.config
#         [xml] $nuget = get-content NuGet.config
#         $sources = @"
# <Project>
#   <Import Project="`$(DotNetRestoreSourcePropsPath)" Condition="'`$(DotNetRestoreSourcePropsPath)' != ''"/>

#   <PropertyGroup>
#     <RestoreSources>`$(DotNetRestoreSources)</RestoreSources>
#     <RestoreSources Condition="'`$(DotNetBuildOffline)' != 'true'">
#       `$(RestoreSources);

# "@
#         foreach ($feed in $nuget.configuration.packageSources.add) {
#             if (-not ($okfeeds -contains $feed.value)) {
#                 write-host -ForegroundColor Red "Feed in $($feed.value) in $repo"
#             }
#             $sources += "      $($feed.value);`r`n"
#         }
#         $sources += @"
#     </RestoreSources>
#   </PropertyGroup>
# </Project>
# "@
#         set-content build/sources.props $sources -Encoding UTF8
#         exec git add build/sources.props
        exec git c -m "Use MSBuild to set NuGet feeds instead of NuGet.config"
        exec git push
    } catch {
        write-host -ForegroundColor Red "Failed $repo"
    }
    popd
}
