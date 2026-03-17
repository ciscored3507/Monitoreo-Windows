param(
  [string]$Alias = "auditstore",
  [string]$Endpoint = "https://minio.example.local",
  [string]$Bucket = "audit-evidence-prod"
)

Write-Host "Provision object store alias=$Alias bucket=$Bucket endpoint=$Endpoint"
Write-Host "Ejecutar mc alias set, mc mb --with-lock y mc ilm rule add --expire-days 365."
