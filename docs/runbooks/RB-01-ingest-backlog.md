# RB-01 Ingest backlog

1. Verificar disponibilidad del object storage (PUT/HEAD).
2. Revisar salud de API gRPC y latencia.
3. Ajustar `max_parallel` de policy gradualmente.
4. Si persiste, activar modo degradado (pausar captura).
