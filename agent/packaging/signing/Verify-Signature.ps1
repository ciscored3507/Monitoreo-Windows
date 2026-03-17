param(
  [Parameter(Mandatory = $true)][string]$MsixPath
)

Write-Host "Verificando firma: $MsixPath"
signtool verify /pa "$MsixPath"
