param(
  [string]$ServiceName = "AuditAgentService"
)

sc.exe stop $ServiceName
sc.exe delete $ServiceName
