param(
  [Parameter(Mandatory = $true)][string]$Thumbprint,
  [string]$ServiceName = "AuditAgentService",
  [string]$AppSettingsPath = "C:\Program Files\AuditAgent\appsettings.json"
)

Write-Host "Rotando certificado del agente. Nuevo thumbprint: $Thumbprint"

if (-not (Test-Path $AppSettingsPath)) {
  throw "No se encontro appsettings en $AppSettingsPath"
}

$json = Get-Content -Path $AppSettingsPath -Raw | ConvertFrom-Json
if (-not $json.Backend) {
  $json | Add-Member -MemberType NoteProperty -Name Backend -Value ([PSCustomObject]@{})
}

$currentThumb = $json.Backend.ClientCertificateThumbprint
$json.Backend.PreviousCertificateThumbprint = $currentThumb
$json.Backend.ClientCertificateThumbprint = $Thumbprint
$json.Backend.ClientCertificateStoreName = "My"
$json.Backend.ClientCertificateStoreLocation = "CurrentUser"

$json | ConvertTo-Json -Depth 10 | Set-Content -Path $AppSettingsPath -Encoding UTF8

Write-Host "Configuracion actualizada. Reiniciando servicio $ServiceName..."
sc.exe stop $ServiceName | Out-Null
Start-Sleep -Seconds 2
sc.exe start $ServiceName | Out-Null
Write-Host "Rotacion aplicada."
