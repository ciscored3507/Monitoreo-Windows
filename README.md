![C#](https://img.shields.io/badge/C%23-CSharp-8D2091)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![Windows](https://img.shields.io/badge/Windows-11-0078D6)
![Estado](https://img.shields.io/badge/Estado-Base%20generada-2E7D32)

# Monitoreo Windows - Audit Internal Solution

Monorepo base para una solucion de auditoria interna con:

- `agent/`: agente Windows (service, desktop, contratos, transporte, storage).
- `web/`: backend API REST + gRPC, worker, portal y CLI.
- `docs/`: especificacion, ADRs, OpenAPI, proto y runbooks.
- `.github/workflows`: pipelines iniciales de CI/CD.

La especificacion tecnica de referencia se encuentra en `moniwin.md`.

## Estructura principal

- `agent/Audit.Agent.slnx`
- `web/Audit.Web.slnx`
- `docs/api/openapi.yaml`
- `docs/api/ingest.proto`

## Build y pruebas

```powershell
# Agent
cd agent
dotnet restore
dotnet test

# Web
cd ../web
dotnet restore
dotnet test
dotnet run --project src/Audit.Backend.Api/Audit.Backend.Api.csproj
```

## Estado actual

Esta iteracion deja listo:

1. Estructura de monorepo completa.
2. Contratos base de agente y backend.
3. Endpoints REST iniciales y servicio gRPC de ingesta con idempotencia en memoria.
4. Scripts operativos y estructura de infraestructura para despliegue en Windows Server.
