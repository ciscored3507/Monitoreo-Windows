# ADR-0004: Envelope encryption con Vault Transit

## Decision
Usar envelope encryption para chunks y proteger DEK con Vault Transit (o KMS equivalente).

## Rationale
Reduce exposicion de llaves y mantiene neutralidad de proveedor.

## Consequence
El agente cifra chunk localmente y persiste DEK solo protegido.
