# script/deploy.ps1: Create a yak package and push it to the server

$ErrorActionPreference = "Stop" # exit on error

Push-Location
cd (Split-Path $MyInvocation.MyCommand.Path)

# set version
$dist = '..\dist'
$file = "$dist\manifest.yml"
#(Get-Content $file) -replace "version:\s*\S+", "version: $env:yak_package_version" | Set-Content $file

# copy .gha to dist/
# TODO: make script generic by moving files to command line args?
Copy-Item -Path ..\bin\Release\ScriptParasite.gha -Destination $dist
Copy-Item -Path ..\bin\Release\ScriptParasite.pdb -Destination $dist

# zip (create package manually)
Compress-Archive -Path $dist\* -DestinationPath $dist\build.zip -Force

# get yak.exe
$url = 'http://files.mcneel.com/yak/tools/latest/yak.exe'
$yak = '.\yak.exe'
# Write-Host (New-Object System.Net.WebClient).DownloadFile($url, $yak) # not working
Invoke-WebRequest -Uri $url -OutFile $yak

# publish
& $yak version
& $yak push $dist\build.zip

Pop-Location