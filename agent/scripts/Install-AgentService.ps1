param(
  [string]$ServiceName = "AuditAgentService",
  [string]$ExePath = "C:\Program Files\AuditAgent\Audit.Agent.Service.exe"
)

sc.exe create $ServiceName binPath= "$ExePath" start= auto
sc.exe start $ServiceName
