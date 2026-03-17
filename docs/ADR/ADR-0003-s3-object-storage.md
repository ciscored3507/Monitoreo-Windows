# ADR-0003: Evidencia en object storage S3-compatible

## Decision
Guardar evidencia cifrada en bucket S3-compatible con lifecycle de 365 dias.

## Rationale
Permite escalabilidad, retencion por politica y opcion legal hold.

## Consequence
La metadata se guarda en SQL y el contenido en object storage.
