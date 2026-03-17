# ADR-0002: Ingesta por gRPC streaming

## Decision
Usar gRPC streaming para subida de chunks binarios y REST para control.

## Rationale
gRPC reduce overhead y facilita flujo continuo de datos con validaciones por mensaje.

## Consequence
El contrato `ingest.proto` es fuente unica para agente y backend.
