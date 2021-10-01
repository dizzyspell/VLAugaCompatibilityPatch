# Adapted code from: https://gist.github.com/kizzx2/9282ea8b0e016960135d3c9ab88419e1
# Shout-out to that feller for saving my life with this :)

$ori = @{}
Try {
  # Load.env file
  if(Test-Path ".\.env") {
    foreach($line in (Get-Content ".\.env")) {
      # Skip comments and such
      if($line -Match '^\s*$' -Or $line -Match '^#') {
        continue
      }

      $key, $val = $line.Split("=")

      # Store existing Env values if present so they can be reset later, or mark 
      # the variable for removal
      $ori[$key] = if(Test-Path Env:\$key) { (Get-Item Env:\$key).Value } else { "" }

      # Set the variable
      New-Item -Name $key -Value $val -ItemType Variable -Path Env: -Force > $null
    }
  }

  # Actually do the Deploy!
  dotnet build
  if (Test-Path $env:VALHEIM_LOCATION\BepInEx\plugins\VLAugaCompatibilityPatch.dll) { 
    rm $env:VALHEIM_LOCATION\BepInEx\plugins\VLAugaCompatibilityPatch.dll 
  }
  cp .\bin\Debug\net472\VLAugaCompatibilityPatch.dll $env:VALHEIM_LOCATION\BepInEx\plugins
} catch {
  Write-Host "Can't deploy - Please ensure a .env file is present in the directory and that it contains the proper key(s)"
} Finally {
  # For each variable, either put it back how it was before or remove it!
  foreach($key in $ori.Keys) {
    New-Item -Name $key -Value $ori.Item($key) -ItemType Variable -Path Env: -Force > $null
  }
}
