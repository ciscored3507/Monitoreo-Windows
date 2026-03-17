param(
  [string]$VaultAddress = "https://vault.example.local",
  [string]$KeyName = "audit-dek"
)

Write-Host "Configurar Transit en $VaultAddress con key $KeyName"
Write-Host "Ejemplo: vault secrets enable transit ; vault write -f transit/keys/$KeyName"
