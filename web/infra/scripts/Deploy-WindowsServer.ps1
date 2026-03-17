param(
  [string]$ArtifactRoot = "C:\Audit\drop"
)

Write-Host "Desplegando artefactos desde $ArtifactRoot"
New-Item -ItemType Directory -Force -Path "C:\Audit" | Out-Null
Copy-Item -Path "$ArtifactRoot\*" -Destination "C:\Audit" -Recurse -Force
