# ADR-0001: Separacion Desktop y Service

## Decision
Separar captura en proceso desktop y orquestacion/subida en Windows Service.

## Rationale
El service en Session 0 no es apto para interaccion de escritorio, mientras la captura requiere sesion de usuario.

## Consequence
Se define contrato IPC por Named Pipes entre ambos procesos.
