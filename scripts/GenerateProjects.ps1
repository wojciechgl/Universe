#!/usr/bin/env powershell

[xml] $graphDef = Get-Content "$PSScriptRoot/graph.xml"

foreach ($repo in $graphDef.Sources.GitRepo) {
    $name = $repo.Name
    $test = $repo.TestOnly -eq 'true'
    $doc = @"
<Project>
  <Import Project="`$(MSBuildExtensionsPath)\`$(MSBuildToolsVersion)\Microsoft.Common.props" />


"@

    if ($repo.BuildSettings) {
        $doc += "  <PropertyGroup>`r`n"

        foreach ($setting in (Get-Member -InputObject $repo.BuildSettings -MemberType Properties))
        {
            $propName = $setting.Name
            $value = $repo.BuildSettings[$propName].InnerText
            $doc += "    <$propName>$value</$propName>`r`n"
        }
        $doc += "  </PropertyGroup>`r`n`r`n"
    }

    $doc += @"
  <ItemGroup>
    <ProjectReference Include=`"..\..\ref\External\External.proj`" />

"@

   if ($repo.DependsOn) {
      foreach ($dep in $repo.DependsOn)
      {
          $depName = $dep.GitRepo
          $path = if ($test) { "..\..\src\$depName\$depName.repoproj" } else { "..\$depName\$depName.repoproj" }
          $doc += "    <ProjectReference Include=`"$path`" />`r`n"
        }
    }


    $doc += @"
  </ItemGroup>

  <Import Project="`$(MSBuildToolsPath)\Microsoft.Common.targets" />
</Project>
"@

    $dir = if ($test) { "test" } else { "src" }
    New-Item -ItemType Directory "$PSScriptRoot/../$dir/$name/" -ErrorAction SilentlyContinue
    Set-Content "$PSScriptRoot/../$dir/$name/$name.repoproj" $doc
}
