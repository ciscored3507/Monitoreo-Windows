param(
  [int]$ApiPort = 5000
)

Write-Host "Configurando IIS como reverse proxy a Kestrel puerto $ApiPort"
Write-Host "Completar con modulo URL Rewrite y ARR segun entorno."
