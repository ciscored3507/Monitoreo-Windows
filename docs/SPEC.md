# Especificacion tecnica

La especificacion fuente del proyecto esta en `moniwin.md`.

Este repositorio implementa un monorepo con:

- `agent/`: agente Windows (captura + spool + transporte).
- `web/`: backend API, worker, portal y CLI.
- `docs/api`: contratos OpenAPI y gRPC.
- `web/infra`: estructura base de IaC y despliegue.

Objetivos de la primera iteracion:

1. Base compilable de soluciones y proyectos.
2. Contratos estables entre agente y backend.
3. API REST + gRPC con semantica de idempotencia.
4. Scripts operativos iniciales para Windows Server y agente.
