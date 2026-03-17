Write-Host "== Audit Agent Diagnose =="
Get-Service -Name "AuditAgentService" -ErrorAction SilentlyContinue
Write-Host "Ultimos eventos del servicio:"
Get-WinEvent -LogName Application -MaxEvents 20 | Where-Object { $_.ProviderName -match "Audit" } | Select-Object TimeCreated, Id, Message
