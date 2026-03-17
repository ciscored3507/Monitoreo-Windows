param(
  [Parameter(Mandatory = $true)][string]$ServiceName,
  [Parameter(Mandatory = $true)][string]$ExePath
)

sc.exe create $ServiceName binPath= "$ExePath" start= auto
sc.exe start $ServiceName
