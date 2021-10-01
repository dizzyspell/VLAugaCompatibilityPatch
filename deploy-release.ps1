  dotnet build -c Release
  if (-Not (Test-Path ".\releases")) { mkdir ".\releases" }
  if (Test-Path ".\releases\VLAugaCompatibilityPatch.dll") { rm .\releases\VLAugaCompatibilityPatch.dll }
  cp .\bin\Release\net472\VLAugaCompatibilityPatch.dll .\releases