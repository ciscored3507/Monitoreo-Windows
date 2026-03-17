param(
  [Parameter(Mandatory = $true)][string]$MsixPath,
  [string]$TimestampUrl = "http://timestamp.digicert.com"
)

Write-Host "Firmando paquete MSIX: $MsixPath"
Write-Host "Ajusta certificado y password segun tu PKI interna."
signtool sign /fd SHA256 /tr $TimestampUrl /td SHA256 "$MsixPath"
