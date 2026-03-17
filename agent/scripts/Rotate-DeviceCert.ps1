param(
  [Parameter(Mandatory = $true)][string]$Thumbprint
)

Write-Host "Rotando certificado del agente. Nuevo thumbprint: $Thumbprint"
Write-Host "Implementar integracion con CA interna o endpoint de enrolamiento."
