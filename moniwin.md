# Especificación técnica generable por Cursor para solución de auditoría interna en C# con agente Windows y backend web en Windows Server

## Resumen ejecutivo y decisiones base

Esta especificación define un **mono‑repo** con dos raíces: `agent/` (agente Windows 11) y `web/` (backend + portal web) para una solución de auditoría interna orientada a **50–250 endpoints** y **5–20 analistas concurrentes**, con backend en **Windows Server** y **retención de 365 días**. El diseño prioriza: **robustez offline**, **idempotencia**, **cifrado extremo a extremo (en tránsito y reposo)**, **trazabilidad/auditoría**, y **operación sencilla on‑prem** sin depender de un proveedor cloud específico.

Decisiones técnicas “ancla” (para reducir ambigüedad al generar código completo):

- **Captura en Windows** mediante APIs nativas:  
  - Video/screen: Windows.Graphics.Capture (flujo de captura de pantalla/ventana a video). citeturn0search0turn0search16  
  - Audio del sistema: WASAPI loopback (documentación Win32; versión en español disponible). citeturn0search4turn0search1  
  - Codificación: Media Foundation H.264 encoder (IMFTransform) para H.264 (perfil según sistema), orientado a eficiencia. citeturn0search5turn0search2  
- **Arquitectura del agente por procesos**: Desktop (sesión de usuario) para captura + Service (Session 0) para spool/telemetría/subida, debido al aislamiento de Session 0 introducido desde Windows Vista para separar servicios del escritorio interactivo. citeturn11search0turn11search9turn11search1  
- **Backend .NET LTS**: se recomienda usar una versión **LTS** por duración de soporte (3 años) según política oficial de soporte. citeturn2search7turn2search11  
- **Comunicación**:
  - REST para control (enrolamiento/políticas/heartbeat) y portal.  
  - gRPC streaming para ingesta de chunks binarios (alto rendimiento, soporte oficial en .NET). citeturn0search3turn0search6turn9search11  
- **Hosting en Windows Server**: Kestrel como servidor recomendado; opción con proxy inverso (IIS) documentada como configuración soportada. citeturn2search5turn2search9turn2search1  
- **Errores REST estandarizados**: RFC 7807 (Problem Details) + middleware nativo de ASP.NET para emitir `application/problem+json`. citeturn3search0turn3search1  
- **Seguridad criptográfica**:
  - TLS 1.3 para transporte (diseñado para prevenir escucha/manipulación/forja). citeturn8search3  
  - AES como base para cifrado simétrico (FIPS 197). citeturn9search0  
  - DPAPI para protección local de secretos en Windows (sin gestionar llaves manualmente). citeturn2search2turn2search6  
  - “Envelope encryption” con motor KMS/HSM; opción recomendada vendor‑neutral: Vault Transit (“encryption as a service”). citeturn6search6turn6search2turn6search10  
- **Object storage S3‑compatible** (on‑prem o cloud) para evidencia, con lifecycle 365d; ejemplos y reglas disponibles (S3 lifecycle). citeturn4search2turn4search14  
  - Opción WORM/legal hold: S3 Object Lock o equivalentes en implementaciones S3 compatibles; legal hold no tiene duración fija y se mantiene hasta removerlo. citeturn4search3turn5search1  
- **Observabilidad**: OpenTelemetry en .NET (instrumentación y exportación) según guía oficial. citeturn1search3turn1search7  
- **Hardening**: aplicar líneas base de seguridad recomendadas. citeturn6search5turn6search13  
- **Modelado de amenazas**: STRIDE como marco para identificar clases de amenazas y mitigaciones. citeturn6search0turn6search8  

Mención única de organizaciones (para contexto): entity["company","Microsoft","software company"] publica la documentación base de Windows/.NET; entity["company","GitHub","code hosting company"] provee el runner para workflows de CI/CD; entity["company","HashiCorp","software company"] mantiene Vault; entity["company","MinIO","object storage company"] es una opción S3‑compatible on‑prem; entity["company","Amazon Web Services","cloud provider"] documenta S3 lifecycle/Object Lock; entity["organization","OpenID Foundation","standards body"] define OIDC; entity["organization","IETF","standards body"] publica RFCs (TLS/OAuth); entity["organization","NIST","us standards agency"] publica AES (FIPS 197); entity["organization","OWASP","security foundation"] publica guías de logging seguro.

## Estructura del mono‑repo y comandos para que Cursor genere el proyecto

### Árbol de carpetas del repo

> Objetivo: que Cursor pueda generar **todo** el código + tests + scripts + IaC con rutas estables.

```text
audit-internal-solution/
  README.md
  LICENSE
  .editorconfig
  .gitattributes
  docs/
    SPEC.md                         # este documento convertido a markdown
    ADR/
      ADR-0001-agent-process-split.md
      ADR-0002-grpc-ingest.md
      ADR-0003-s3-object-storage.md
      ADR-0004-vault-transit-envelope.md
    api/
      openapi.yaml
      ingest.proto
    runbooks/
      RB-01-ingest-backlog.md
      RB-02-cert-rotation.md
      RB-03-retention-legal-hold.md
      RB-04-incident-response.md
  agent/
    Audit.Agent.sln
    global.json
    Directory.Build.props
    Directory.Packages.props
    src/
      Audit.Agent.Contracts/
      Audit.Agent.Common/
      Audit.Agent.Security/
      Audit.Agent.Storage/
      Audit.Agent.Capture/
      Audit.Agent.Audio/
      Audit.Agent.Encoding/
      Audit.Agent.Transport/
      Audit.Agent.Telemetry/
      Audit.Agent.Service/
      Audit.Agent.Desktop/
      Audit.Agent.Bootstrapper/
    tests/
      Audit.Agent.UnitTests/
      Audit.Agent.IntegrationTests/
    packaging/
      msix/
        Audit.Agent.Package/
          Package.appxmanifest
          Audit.Agent.Package.wapproj
        appinstaller/
          Audit.Agent.appinstaller
      signing/
        Sign-MSIX.ps1
        Verify-Signature.ps1
    scripts/
      Install-AgentService.ps1
      Uninstall-AgentService.ps1
      Rotate-DeviceCert.ps1
      Diagnose-Agent.ps1
  web/
    Audit.Web.sln
    global.json
    Directory.Build.props
    Directory.Packages.props
    src/
      Audit.Backend.Contracts/
      Audit.Backend.Domain/
      Audit.Backend.Application/
      Audit.Backend.Infrastructure/
      Audit.Backend.Api/
      Audit.Backend.Worker/
      Audit.Web.Portal/
      Audit.Admin.Cli/
    tests/
      Audit.Backend.UnitTests/
      Audit.Backend.IntegrationTests/
      Audit.Web.E2E/
      Audit.LoadTests/
        k6/
    infra/
      terraform/
        modules/
          windows_server_apphost/
          sql/
          object_storage_s3_compatible/
          vault/
        envs/
          onprem-dev/
          onprem-prod/
          aws-option/
          azure-option/
      ansible/
        inventories/
        playbooks/
        roles/
      dsc/
      scripts/
        Deploy-WindowsServer.ps1
        Configure-IIS-ReverseProxy.ps1
        Install-WindowsServices.ps1
        Provision-ObjectStore.ps1
        Configure-VaultTransit.ps1
      sql/
        migrations/
        seed/
  .github/
    workflows/
      agent-ci.yml
      agent-release-msix.yml
      web-ci.yml
      web-deploy-windowsserver.yml
      loadtest-k6.yml
```

### Comandos base para generar proyectos .NET

Plantillas integradas disponibles con `dotnet new` (y `dotnet new list`) se documentan oficialmente. citeturn9search7  

Ejecuta desde la raíz `audit-internal-solution/`:

```bash
# Crear soluciones
dotnet new sln -n Audit.Agent -o agent
dotnet new sln -n Audit.Web   -o web

# Agent: librerías base
dotnet new classlib -n Audit.Agent.Contracts   -o agent/src/Audit.Agent.Contracts
dotnet new classlib -n Audit.Agent.Common      -o agent/src/Audit.Agent.Common
dotnet new classlib -n Audit.Agent.Security    -o agent/src/Audit.Agent.Security
dotnet new classlib -n Audit.Agent.Storage     -o agent/src/Audit.Agent.Storage
dotnet new classlib -n Audit.Agent.Capture     -o agent/src/Audit.Agent.Capture
dotnet new classlib -n Audit.Agent.Audio       -o agent/src/Audit.Agent.Audio
dotnet new classlib -n Audit.Agent.Encoding    -o agent/src/Audit.Agent.Encoding
dotnet new classlib -n Audit.Agent.Transport   -o agent/src/Audit.Agent.Transport
dotnet new classlib -n Audit.Agent.Telemetry   -o agent/src/Audit.Agent.Telemetry

# Agent: Windows Service (Worker Service)
dotnet new worker -n Audit.Agent.Service -o agent/src/Audit.Agent.Service

# Agent: tests
dotnet new xunit -n Audit.Agent.UnitTests -o agent/tests/Audit.Agent.UnitTests
dotnet new xunit -n Audit.Agent.IntegrationTests -o agent/tests/Audit.Agent.IntegrationTests

# Web: backend
dotnet new classlib -n Audit.Backend.Contracts     -o web/src/Audit.Backend.Contracts
dotnet new classlib -n Audit.Backend.Domain        -o web/src/Audit.Backend.Domain
dotnet new classlib -n Audit.Backend.Application   -o web/src/Audit.Backend.Application
dotnet new classlib -n Audit.Backend.Infrastructure -o web/src/Audit.Backend.Infrastructure

dotnet new webapi -n Audit.Backend.Api -o web/src/Audit.Backend.Api
dotnet new worker -n Audit.Backend.Worker -o web/src/Audit.Backend.Worker

# Web: portal (elige uno)
# Opción A: MVC/Razor Pages
dotnet new mvc -n Audit.Web.Portal -o web/src/Audit.Web.Portal
# Opción B (alternativa): Blazor Server
# dotnet new blazorserver -n Audit.Web.Portal -o web/src/Audit.Web.Portal

# Web: tests
dotnet new xunit -n Audit.Backend.UnitTests -o web/tests/Audit.Backend.UnitTests
dotnet new xunit -n Audit.Backend.IntegrationTests -o web/tests/Audit.Backend.IntegrationTests
```

Para el Windows Service, la guía oficial describe `dotnet new worker` como base y cómo ejecutarlo como servicio. citeturn1search10turn1search14turn1search6  

### Generación del proyecto WinUI 3 / Windows App SDK

Para el proceso Desktop (captura en sesión de usuario), se recomienda WinUI 3 sobre Windows App SDK; la documentación define WinUI 3 como el framework moderno nativo. citeturn1search0turn1search4  

Como la plantilla se crea típicamente desde Visual Studio (no siempre por CLI), instrucción para Cursor:

- Crear `agent/src/Audit.Agent.Desktop/` como proyecto WinUI 3 empaquetado, según guía de “crear la primera app WinUI 3”. citeturn1search12turn1search16  
- Luego Cursor integra referencias a librerías `Audit.Agent.*` y añade interop/WinRT para Windows.Graphics.Capture. citeturn0search0turn0search16  

## Agente Windows listo para implementación

### Propósito y límites operativos del agente

El agente debe cumplir tres objetivos operativos:

1) **Capturar** (video de pantalla/ventana + audio del sistema) de forma controlada por política. citeturn0search0turn0search4  
2) **Persistir localmente** en spool cifrado y **subir de forma confiable** (offline-first) con idempotencia.  
3) **Minimizar riesgo**: bajo impacto de CPU/IO, rotación de archivos, hardening y telemetría segura.

La separación Desktop/Service se basa en Session 0 isolation (servicios aislados del escritorio interactivo), lo cual guía el diseño de captura. citeturn11search0turn11search9  

### Diagrama mermaid de arquitectura interna del agente

```mermaid
flowchart TB
  subgraph Desktop["Proceso Desktop (sesión de usuario)"]
    UI[UI mínima / tray opcional]
    CAP[Captura pantalla/ventana\nWindows.Graphics.Capture]
    AUD[Captura audio\nWASAPI loopback (opcional app-loopback)]
    ENC[Encode H.264/AAC\nMedia Foundation]
    CHK[Chunker 5s + thumbnails]
    UI --> CAP --> ENC --> CHK
    UI --> AUD --> ENC
  end

  subgraph Service["Windows Service (Session 0)"]
    ORCH[Orquestador + scheduler]
    IPC[IPC Named Pipes]
    Q[(SQLite Queue)]
    FS[Encrypted FileStore\n%ProgramData%]
    UP[gRPC uploader mTLS]
    REST[REST: policy/heartbeat]
    TEL[OpenTelemetry + logs]
    ORCH --> IPC
    ORCH --> Q --> UP
    ORCH --> FS --> UP
    ORCH --> REST
    ORCH --> TEL
  end

  CHK --> FS
  Desktop -. Named Pipes .-> IPC
```

### Componentes/proyectos del agente: propósito, interfaces y dependencias

A continuación se especifica qué debe generar Cursor por proyecto:

#### `Audit.Agent.Contracts`
Contrato estable entre módulos (DTOs REST + protobuf gRPC + enums de error).

Interfaces públicas:
- `ProblemCodes` (constantes string)
- DTOs: `PolicyDto`, `HeartbeatDto`, `EnrollRequest/Response`, `UploadResultDto`
- `ingest.proto` (idéntico al backend; “single source of truth”)

Dependencias: ninguna (solo BCL).

#### `Audit.Agent.Capture`
Wrapper de Windows.Graphics.Capture para Desktop. La documentación de captura a vídeo describe clases clave (GraphicsCaptureItem, GraphicsCaptureSession, frame pool) y pipeline a MediaStreamSource. citeturn0search0  

Interfaces públicas:
- `ICaptureSource` → `IAsyncEnumerable<VideoFrame>` (frame + timestamp)
- `CaptureTarget` (enum: Display / Window)
- `CaptureSettings` (fps target, size, cursor)

Contratos internos:
- `FrameTimestamp` (monotonic + wallclock)
- `PixelFormat` (DXGI format)

Dependencias:
- Desktop/WinRT, Direct3D interop (especificar en csproj).

#### `Audit.Agent.Audio`
Audio loopback según documentación WASAPI (loopback capt
ura audio “rendered”). citeturn0search4turn0search1  
Nota: la versión en español indica que loopback por defecto puede capturar mezcla de sesiones incluso desde un servicio en Session 0; esto se documenta explícitamente y puede influir si deseas mover audio al servicio. citeturn0search4  

Interfaces públicas:
- `IAudioSource` → `IAsyncEnumerable<AudioFrame>` (PCM16/float)
- `AudioSettings` (sample rate, channels, deviceId, mode)

Opcional:
- soporte de “application loopback capture” basado en sample oficial. citeturn0search7  

#### `Audit.Agent.Encoding`
Codificación y chunking. Media Foundation H.264 encoder expone IMFTransform; se documenta perfil Baseline/Main/High y sus interfaces. citeturn0search5turn0search2  

Interfaces públicas:
- `IEncoderPipeline.Start(SessionMeta meta, EncoderSettings settings)`
- `IEncoderPipeline.Push(VideoFrame frame)` / `Push(AudioFrame frame)`
- `IEncoderPipeline.TryRotateChunk(out ChunkArtifact chunk)`

Contratos internos:
- `ChunkArtifact`:
  - `ChunkId` (GUID)
  - `SessionId` (GUID)
  - `SegmentIndex` (int)
  - `CaptureStartUtc`, `CaptureEndUtc`
  - `LocalEncryptedPath`
  - `Sha256Hex`
  - `Bytes`
  - `ContentType` (por defecto `video/mp4`)

#### `Audit.Agent.Storage`
Spool local con:
- SQLite para cola/estado.
- File store para binarios cifrados.

Interfaces públicas:
- `IChunkQueue.Enqueue(ChunkArtifact meta)`
- `IChunkQueue.TryLeaseBatch(int max, TimeSpan lease, out List<QueuedChunk> batch)`
- `IChunkQueue.MarkUploaded(chunkId)`
- `IChunkQueue.MarkFailed(chunkId, reason, nextRetryUtc)`
- `IChunkQueue.MoveToDeadLetter(chunkId, reason)`

#### `Audit.Agent.Transport`
- REST para policy/heartbeat.
- gRPC streaming para ingesta.

gRPC en .NET está documentado como framework de alto rendimiento, y hay guía oficial para crear cliente/servidor. citeturn0search3turn0search12  

Interfaces públicas:
- `IAgentControlClient.GetPolicyAsync()`
- `IAgentControlClient.PostHeartbeatAsync()`
- `IIngestClient.UploadChunksAsync(IAsyncEnumerable<UploadChunk> chunks)`

mTLS/cert auth: en el cliente, se configura HttpClientHandler con certificado cliente; en server, autenticación de certificados se hace a nivel TLS antes de ASP.NET y el handler puede resolver el certificado a ClaimsPrincipal. citeturn2search0  

#### `Audit.Agent.Security`
DPAPI: .NET expone ProtectedData y explica que DPAPI cifra con credenciales de usuario o máquina, evitando manejar llaves explícitamente. citeturn2search2turn2search6  

Interfaces públicas:
- `ILocalSecretProtector.Protect(byte[] data, scope)`
- `ILocalSecretProtector.Unprotect(byte[] blob, scope)`
- `IEnvelopeCryptor.EncryptChunk(Stream plaintext, SessionKeyMaterial km)` (AES‑GCM)
- `IEnvelopeCryptor.DecryptChunk(Stream ciphertext, SessionKeyMaterial km)`

#### `Audit.Agent.Telemetry`
Observabilidad .NET con OpenTelemetry: se documenta instrumentación en código y exportación (OTLP). citeturn1search3turn1search7  

Interfaces públicas:
- `TelemetryBootstrap.Configure(...)`
- `Metrics`:
  - `agent.capture.frames_total`
  - `agent.encode.chunks_total`
  - `agent.upload.success_total`, `agent.upload.fail_total`
  - `agent.queue.depth`
  - `agent.upload.bytes_total`
- Traces:
  - `CaptureSession.Start/Stop`
  - `Chunk.Upload` (con chunk_id)

### Contrato IPC Desktop ↔ Service (Named Pipes)

Especificación:
- Transporte: Named Pipes (`\\.\pipe\AuditAgentIPC`)
- Mensajes: JSON (rápido de implementar) o MessagePack (más eficiente)
- Autenticación local: ACL del pipe para permitir únicamente usuario interactivo + cuenta del servicio.

Comandos/acciones IPC:
- `StartCapture(policyVersion, targets...)`
- `StopCapture(sessionId)`
- `SubmitChunk(sessionId, chunkId, encryptedPath, sha256, bytes, startUtc, endUtc)`
- `HealthReport(cpu, backlog, lastError)`

### Esquema SQLite del spool y estrategia de reintentos/idempotencia

Requisitos:
- Operación offline.
- Cola con leasing para evitar doble envío.
- Dead-letter local para análisis.

DDL (SQLite):

```sql
PRAGMA journal_mode=WAL;

CREATE TABLE IF NOT EXISTS sessions (
  session_id TEXT PRIMARY KEY,
  device_id  TEXT NOT NULL,
  policy_id  TEXT NOT NULL,
  started_utc TEXT NOT NULL,
  ended_utc   TEXT NULL,
  state       INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS chunk_queue (
  chunk_id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL,
  segment_index INTEGER NOT NULL,
  sha256_hex TEXT NOT NULL,
  bytes INTEGER NOT NULL,
  capture_start_utc TEXT NOT NULL,
  capture_end_utc TEXT NOT NULL,
  encrypted_path TEXT NOT NULL,
  status INTEGER NOT NULL,            -- 0=pending,1=leased,2=uploaded,3=deadletter
  attempts INTEGER NOT NULL DEFAULT 0,
  next_retry_utc TEXT NULL,
  lease_until_utc TEXT NULL,
  last_error TEXT NULL,
  created_utc TEXT NOT NULL,
  FOREIGN KEY(session_id) REFERENCES sessions(session_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_chunk_session_segment
  ON chunk_queue(session_id, segment_index);

CREATE INDEX IF NOT EXISTS ix_chunk_status_retry
  ON chunk_queue(status, next_retry_utc);
```

Idempotencia:
- REST/gRPC aceptan `Idempotency-Key = chunk_id`.
- Se exige `X-Content-SHA256 = sha256_hex` para detectar colisión/mismatch.
- Semántica:
  - Si `chunk_id` existe y hash coincide → OK (no duplicar).
  - Si `chunk_id` existe pero hash distinto → conflicto (corrupción o bug) y mover a dead-letter.

## Backend y portal web con contratos API completos (OpenAPI + gRPC)

### Vista de componentes y hosting en Windows Server

Backend se divide en:

- `Audit.Backend.Api`: REST + gRPC + autent/autorización + ProblemDetails.  
- `Audit.Backend.Worker`: jobs asíncronos (verificación, retención, auditoría, manifests).  
- `Audit.Web.Portal`: UI web para analistas/admins (MVC o Blazor Server).

Hosting:
- Kestrel es el servidor recomendado para ASP.NET Core. citeturn2search5  
- Uso con proxy inverso (IIS) se describe como válido y soportado; Kestrel puede vivir solo o detrás de proxy. citeturn2search1turn2search9  

Autenticación de certificados (mTLS) en server:
- La autenticación por certificados se realiza en el nivel TLS, antes de llegar a ASP.NET; el handler puede resolver certificado a `ClaimsPrincipal`. citeturn2search0  

### Diagrama mermaid de interacción agent ↔ backend

```mermaid
flowchart LR
  subgraph A["Agent Windows"]
    D[Desktop capture/encode]
    S[Service\nspool+upload]
    Q[(SQLite + encrypted files)]
    D --> Q --> S
  end

  subgraph B["Windows Server backend"]
    RP[Reverse proxy opcional (IIS)]
    API[API .NET (REST)]
    GRPC[gRPC Ingest]
    W[Workers .NET]
    DB[(SQL: metadatos/RBAC/auditoría)]
    OS[(Object storage S3-compatible)]
    VAULT[Envelope KMS (Vault Transit opcional)]
  end

  subgraph P["Portal web"]
    U[Analistas/Admins]
  end

  S -- mTLS + REST --> API
  S -- mTLS + gRPC stream --> GRPC
  GRPC --> OS
  GRPC --> DB
  API --> DB
  W --> DB
  W --> OS
  W --> VAULT
  U -- OIDC/JWT --> API
  U --> RP --> API
```

### OpenAPI 3.1 (YAML) con ProblemDetails y esquemas de seguridad

Nota importante: OpenAPI 3.1 incluye `mutualTLS` como tipo de security scheme; sin embargo, herramientas/librerías pueden no soportarlo completamente (p. ej., modelado en ciertas librerías). citeturn10search5turn10search3  

OpenAPI `web/docs/api/openapi.yaml`:

```yaml
openapi: 3.1.0
info:
  title: Audit Backend API
  version: "1.0.0"
servers:
  - url: https://audit.example.local

components:
  securitySchemes:
    MutualTLS:
      type: mutualTLS
      description: >
        Autenticación mTLS para endpoints de agente. El certificado cliente
        se mapea a device_id por thumbprint SHA-256.

    OIDC:
      type: openIdConnect
      openIdConnectUrl: https://idp.example.local/.well-known/openid-configuration
      description: OIDC para usuarios (analistas/admins).

  schemas:
    ProblemDetails:
      type: object
      description: RFC7807 Problem Details con extensiones.
      properties:
        type: { type: string }
        title: { type: string }
        status: { type: integer }
        detail: { type: string }
        instance: { type: string }
        error_code: { type: string }
        correlation_id: { type: string }
      required: [type, title, status]

    EnrollRequest:
      type: object
      required: [hostname, os_version, device_fingerprint]
      properties:
        hostname: { type: string, maxLength: 255 }
        os_version: { type: string, maxLength: 100 }
        device_fingerprint: { type: string, description: "SHA256 hex" }
        csr_pem: { type: string, nullable: true }

    EnrollResponse:
      type: object
      required: [tenant_id, device_id, policy_id]
      properties:
        tenant_id: { type: string, format: uuid }
        device_id: { type: string, format: uuid }
        policy_id: { type: string, format: uuid }
        device_cert_pem: { type: string, nullable: true }

    PolicyDto:
      type: object
      required: [policy_id, version, capture, upload]
      properties:
        policy_id: { type: string, format: uuid }
        version: { type: integer }
        capture:
          type: object
          required: [chunk_seconds, fps]
          properties:
            chunk_seconds: { type: integer, minimum: 2, maximum: 15 }
            fps: { type: integer, minimum: 1, maximum: 30 }
            resolution:
              type: string
              enum: ["720p","1080p","source"]
            targets:
              type: array
              items:
                type: object
                required: [type, value]
                properties:
                  type: { type: string, enum: ["display","windowTitleContains","processName"] }
                  value: { type: string }
            audio_enabled: { type: boolean }
            exclude_titles:
              type: array
              items: { type: string }
        upload:
          type: object
          required: [grpc_endpoint, max_parallel]
          properties:
            grpc_endpoint: { type: string }
            max_parallel: { type: integer, minimum: 1, maximum: 8 }

    HeartbeatRequest:
      type: object
      required: [device_time_utc, agent_version, queue_depth]
      properties:
        device_time_utc: { type: string, format: date-time }
        agent_version: { type: string }
        queue_depth: { type: integer, minimum: 0 }
        last_upload_utc: { type: string, format: date-time, nullable: true }
        health_flags:
          type: array
          items: { type: string }

    HeartbeatResponse:
      type: object
      required: [server_time_utc]
      properties:
        server_time_utc: { type: string, format: date-time }

paths:
  /api/v1/agent/enroll:
    post:
      summary: Enrolamiento inicial de dispositivo
      security:
        - MutualTLS: []
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: "#/components/schemas/EnrollRequest" }
            examples:
              sample:
                value:
                  hostname: "PC-001"
                  os_version: "Windows 11 23H2"
                  device_fingerprint: "9f...ab"
                  csr_pem: null
      responses:
        "201":
          description: Enrolled
          content:
            application/json:
              schema: { $ref: "#/components/schemas/EnrollResponse" }
        "400":
          description: Bad request
          content:
            application/problem+json:
              schema: { $ref: "#/components/schemas/ProblemDetails" }
              example:
                type: "urn:problems:invalid_payload"
                title: "Invalid request"
                status: 400
                detail: "hostname missing"
                error_code: "invalid_payload"
        "409":
          description: Already enrolled
          content:
            application/problem+json:
              schema: { $ref: "#/components/schemas/ProblemDetails" }

  /api/v1/agent/policy:
    get:
      summary: Get policy of current device
      security:
        - MutualTLS: []
      responses:
        "200":
          description: Policy
          content:
            application/json:
              schema: { $ref: "#/components/schemas/PolicyDto" }
        "401":
          description: Unauthorized
          content:
            application/problem+json:
              schema: { $ref: "#/components/schemas/ProblemDetails" }

  /api/v1/agent/heartbeat:
    post:
      summary: Agent heartbeat
      security:
        - MutualTLS: []
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: "#/components/schemas/HeartbeatRequest" }
      responses:
        "200":
          description: OK
          content:
            application/json:
              schema: { $ref: "#/components/schemas/HeartbeatResponse" }

  /api/v1/admin/devices:
    get:
      summary: List devices (admin/analyst)
      security:
        - OIDC: []
      parameters:
        - in: query
          name: q
          schema: { type: string }
        - in: query
          name: status
          schema: { type: string, enum: ["active","revoked","offline"] }
      responses:
        "200":
          description: List
        "401":
          description: Unauthorized

  /api/v1/evidence/sessions:
    get:
      summary: Query capture sessions
      security:
        - OIDC: []
      parameters:
        - in: query
          name: device_id
          required: true
          schema: { type: string, format: uuid }
        - in: query
          name: from_utc
          required: true
          schema: { type: string, format: date-time }
        - in: query
          name: to_utc
          required: true
          schema: { type: string, format: date-time }
      responses:
        "200":
          description: Sessions
```

ProblemDetails y manejo de errores en ASP.NET están documentados (AddProblemDetails + middleware). citeturn3search1turn3search5  

### gRPC `.proto` completo para ingesta con idempotencia

`web/docs/api/ingest.proto` (idéntico en agent y web):

```proto
syntax = "proto3";
package audit.ingest.v1;

message ChunkMeta {
  string tenant_id = 1;          // uuid string
  string device_id = 2;          // uuid string
  string session_id = 3;         // uuid string
  int32  segment_index = 4;      // 0..N
  string chunk_id = 5;           // uuid string (Idempotency-Key)
  string sha256_hex = 6;         // hex del contenido (plaintext o ciphertext, definido en spec)
  int64  bytes = 7;
  int64  capture_start_unix_ms = 8;
  int64  capture_end_unix_ms = 9;
  string content_type = 10;      // "video/mp4"
  map<string,string> tags = 11;  // opcional
}

message UploadChunkRequest {
  ChunkMeta meta = 1;
  bytes data = 2;                // chunk
}

message UploadChunkResponse {
  string chunk_id = 1;
  bool stored = 2;               // false si ya existía (idempotente)
  string object_key = 3;
}

message UploadError {
  string chunk_id = 1;
  string error_code = 2;         // p.ej. "hash_mismatch"
  string message = 3;
}

service IngestService {
  rpc UploadChunks(stream UploadChunkRequest) returns (stream UploadChunkResponse);
}
```

gRPC en .NET y creación de servicios se documentan oficialmente. citeturn0search3turn0search12  

### Esqueleto de handler gRPC (C#) con validaciones críticas

```csharp
// web/src/Audit.Backend.Api/Grpc/IngestService.cs
using Grpc.Core;
using audit.ingest.v1;

public sealed class IngestServiceImpl : IngestService.IngestServiceBase
{
    private readonly IChunkIngestor _ingestor;
    private readonly ILogger<IngestServiceImpl> _logger;

    public IngestServiceImpl(IChunkIngestor ingestor, ILogger<IngestServiceImpl> logger)
    {
        _ingestor = ingestor;
        _logger = logger;
    }

    public override async Task UploadChunks(
        IAsyncStreamReader<UploadChunkRequest> requestStream,
        IServerStreamWriter<UploadChunkResponse> responseStream,
        ServerCallContext context)
    {
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            var req = requestStream.Current;

            // Validación mínima
            if (req?.Meta is null || string.IsNullOrWhiteSpace(req.Meta.ChunkId) || req.Data.IsEmpty)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_chunk"));

            try
            {
                var result = await _ingestor.StoreAsync(req.Meta, req.Data.Memory, context.CancellationToken);

                await responseStream.WriteAsync(new UploadChunkResponse
                {
                    ChunkId = req.Meta.ChunkId,
                    Stored = result.Stored,
                    ObjectKey = result.ObjectKey
                });
            }
            catch (HashMismatchException ex)
            {
                // chunk_id repetido con hash distinto → corrupción o bug
                _logger.LogWarning(ex, "hash mismatch for chunk {ChunkId}", req.Meta.ChunkId);
                throw new RpcException(new Status(StatusCode.AlreadyExists, "hash_mismatch"));
            }
        }
    }
}
```

## Modelo de datos SQL, migraciones y almacenamiento de evidencia con retención 365d

### Tablas SQL “mínimas pero suficientes” (metadatos + acceso + auditoría)

Base: SQL Server o PostgreSQL. (Ambas soportan índices y particionado; en Windows Server SQL Server suele ser operativo; PostgreSQL es alternativa).

Tablas (tipos expresados en estilo SQL Server; Cursor puede mapear a PostgreSQL):

**`tenants`**
- `tenant_id UNIQUEIDENTIFIER PK`
- `name NVARCHAR(200)`
- `created_utc DATETIME2`

**`users`**
- `user_id UNIQUEIDENTIFIER PK`
- `tenant_id UNIQUEIDENTIFIER FK`
- `subject NVARCHAR(200)` (sub OIDC)
- `email NVARCHAR(320)`
- `status TINYINT`
- Índice: `(tenant_id, subject) UNIQUE`

**`roles`**
- `role_id SMALLINT PK`
- `name NVARCHAR(50)` (Admin, Auditor, Supervisor, Viewer)

**`permissions`** (opcional si quieres granularidad)
- `permission_key NVARCHAR(100) PK` (e.g. `evidence.read`, `case.write`)

**`role_permissions`**
- `role_id SMALLINT`
- `permission_key NVARCHAR(100)`
- PK `(role_id, permission_key)`

**`devices`**
- `device_id UNIQUEIDENTIFIER PK`
- `tenant_id UNIQUEIDENTIFIER FK`
- `hostname NVARCHAR(255)`
- `os_version NVARCHAR(100)`
- `agent_version NVARCHAR(50)`
- `status TINYINT` (active/revoked/offline)
- `last_seen_utc DATETIME2`
- `fingerprint_sha256 VARBINARY(32)`
- Índices: `(tenant_id, hostname)`, `(tenant_id, status)`

**`device_certificates`**
- `cert_id UNIQUEIDENTIFIER PK`
- `device_id UNIQUEIDENTIFIER FK`
- `thumbprint_sha256 VARBINARY(32) UNIQUE`
- `not_before_utc DATETIME2`
- `not_after_utc DATETIME2`
- `revoked_utc DATETIME2 NULL`

**`policies`**
- `policy_id UNIQUEIDENTIFIER PK`
- `tenant_id UNIQUEIDENTIFIER FK`
- `version INT`
- `json NVARCHAR(MAX)`
- `created_utc DATETIME2`
- Índice: `(tenant_id, policy_id, version) UNIQUE`

**`policy_assignments`**
- `assignment_id UNIQUEIDENTIFIER PK`
- `policy_id UNIQUEIDENTIFIER`
- `device_id UNIQUEIDENTIFIER`
- `(policy_id, device_id) UNIQUE`

**`capture_sessions`**
- `session_id UNIQUEIDENTIFIER PK`
- `device_id UNIQUEIDENTIFIER`
- `policy_id UNIQUEIDENTIFIER`
- `started_utc DATETIME2`
- `ended_utc DATETIME2 NULL`
- `state TINYINT`
- Índice: `(device_id, started_utc DESC)`

**`chunks`**
- `chunk_id UNIQUEIDENTIFIER PK`
- `session_id UNIQUEIDENTIFIER FK`
- `segment_index INT`
- `sha256 VARBINARY(32)`
- `bytes BIGINT`
- `capture_start_utc DATETIME2`
- `capture_end_utc DATETIME2`
- `object_key NVARCHAR(1024)`
- `content_type NVARCHAR(100)`
- `created_utc DATETIME2`
- Constraint unique: `(session_id, segment_index)`
- Índice: `(capture_start_utc)`, `(session_id)`

**`audit_log`** (append‑only)
- `audit_id BIGINT IDENTITY PK`
- `tenant_id UNIQUEIDENTIFIER`
- `actor_type TINYINT` (User/Device/System)
- `actor_id UNIQUEIDENTIFIER`
- `action NVARCHAR(100)`
- `target_type NVARCHAR(50)`
- `target_id UNIQUEIDENTIFIER NULL`
- `ts_utc DATETIME2`
- `correlation_id UNIQUEIDENTIFIER`
- `details_json NVARCHAR(MAX)`

### Migraciones EF Core (ejemplo)

EF Core migraciones se documentan como mecanismo para actualizar el esquema incrementalmente. citeturn3search2turn3search10turn3search6  

Ejemplo de migración inicial (extracto):

```csharp
// web/src/Audit.Backend.Infrastructure/Persistence/Sql/Migrations/20260316_Initial.cs
using Microsoft.EntityFrameworkCore.Migrations;

public partial class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "devices",
            columns: table => new
            {
                device_id = table.Column<Guid>(nullable: false),
                tenant_id = table.Column<Guid>(nullable: false),
                hostname = table.Column<string>(maxLength: 255, nullable: false),
                os_version = table.Column<string>(maxLength: 100, nullable: false),
                agent_version = table.Column<string>(maxLength: 50, nullable: true),
                status = table.Column<byte>(nullable: false),
                last_seen_utc = table.Column<DateTime>(nullable: true),
                fingerprint_sha256 = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_devices", x => x.device_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_devices_tenant_hostname",
            table: "devices",
            columns: new[] { "tenant_id", "hostname" },
            unique: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "devices");
    }
}
```

### Almacenamiento de evidencia en object storage S3‑compatible

**Naming convention**
- Bucket: `audit-evidence-{env}`
- Key: `t={tenantId}/d={deviceId}/y=YYYY/m=MM/day=DD/s={sessionId}/seg={000123}.mp4.enc`

**Lifecycle 365 días**
- En S3, puedes usar `Expiration` para eliminar objetos; hay ejemplos oficiales de configuración de lifecycle. citeturn4search2turn4search17  

Ejemplo JSON (si tu S3-compatible acepta JSON; S3 oficial usa XML, pero muchos gateways aceptan JSON vía tooling; define como plantilla conceptual):

```json
{
  "Rules": [
    {
      "ID": "expire-365d",
      "Status": "Enabled",
      "Filter": { "Prefix": "" },
      "Expiration": { "Days": 365 }
    }
  ]
}
```

**WORM / legal hold**
- Legal hold en S3 Object Lock impide overwrite/delete y no tiene duración fija; se remueve manualmente con permisos adecuados. citeturn4search3turn4search11  

### Comandos concretos para MinIO Client (mc): bucket con Object Lock + lifecycle 365d

MinIO documenta creación de bucket con lock habilitado vía `mc mb --with-lock`. citeturn5search2turn5search6  
Para expiración, la documentación indica que puedes crear regla para expirar objetos tras N días y que 365 días es un ejemplo típico. citeturn4search8turn4search0  

```bash
# Configurar alias (ejemplo)
mc alias set auditstore https://minio.example.local ACCESSKEY SECRETKEY

# Crear bucket con object locking habilitado
mc mb --with-lock auditstore/audit-evidence-prod

# Regla lifecycle: expirar objetos tras 365 días
mc ilm rule add --expire-days 365 auditstore/audit-evidence-prod

# Ver reglas
mc ilm rule ls auditstore/audit-evidence-prod
```

Legal hold (según docs de mc legalhold):

```bash
# Activar legal hold para un objeto (ejemplo)
mc legalhold set ON auditstore/audit-evidence-prod/t=.../d=.../y=.../seg=000123.mp4.enc
```

La documentación de `mc legalhold` y de object locking/immutability describe que legal hold puede aplicarse y requiere bucket con locking habilitado. citeturn5search12turn4search5turn5search9  

## Flujos de autenticación/autorización y diseño de seguridad con STRIDE

### Autenticación de usuarios (analistas/admins): OIDC/OAuth2/JWT

OIDC amplía OAuth 2.0 para autenticación (SSO) y usa id_token; la doc de la plataforma de identidad explica el rol de OIDC como extensión de OAuth 2.0. citeturn8search0turn8search1  
JWT define un formato compacto para transferir claims. citeturn8search2  

Especificación:
- Portal web: flujo OIDC (authorization code) en front.
- Backend API: valida JWT Bearer y aplica policies por roles/permissions.

Claims mínimas (JWT access token):
- `sub`, `iss`, `aud`, `exp`, `iat`
- `tid` (tenant id interno)
- `roles`: `["Admin","Auditor",...]`
- `perms`: `["evidence.read","case.write",...]` (si lo usas)
- `scope`: opcional (si integras con un IdP que emite scopes)

### Autenticación de agentes: mTLS + mapeo de certificado a device_id

ASP.NET Core soporta autenticación por certificados mediante `Microsoft.AspNetCore.Authentication.Certificate`; se valida TLS y se puede resolver el certificado a `ClaimsPrincipal`. citeturn2search0  

Regla de mapeo:
- Extraer `thumbprint_sha256` del certificado cliente.
- Buscar en `device_certificates.thumbprint_sha256`.
- Si existe y no está revocado → asignar `device_id` y `tenant_id` al principal.
- Si no existe/revocado → 401.

### Cifrado en tránsito y reposo

TLS 1.3 (RFC 8446) especifica que TLS busca prevenir escucha/manipulación/forja. citeturn8search3  
AES (FIPS 197) define el estándar de cifrado simétrico aprobado para proteger datos electrónicos. citeturn9search0  

Estrategia recomendada:
- En tránsito: TLS (idealmente 1.3) para REST y gRPC.
- En reposo:
  - Chunks cifrados en el endpoint con AES‑GCM (DEK por sesión).
  - DEK protegido con envelope encryption (Vault Transit o KMS/HSM).

### Envelope encryption con Vault Transit (ejemplo listo para Cursor)

Vault Transit se documenta como “encryption as a service” y no almacena los datos enviados. citeturn6search6turn6search14  
Ejemplo de política/paths para permitir encrypt/decrypt está en tutorial oficial. citeturn6search2  

Ejemplo: proteger DEK (32 bytes) creando `ciphertext`:

```csharp
// agent/src/Audit.Agent.Security/VaultTransitClient.cs
using System.Net.Http.Json;
using System.Text;

public sealed class VaultTransitClient
{
    private readonly HttpClient _http;

    public VaultTransitClient(HttpClient http) => _http = http;

    public async Task<string> EncryptDekAsync(string keyName, byte[] dek, CancellationToken ct)
    {
        var b64 = Convert.ToBase64String(dek);

        // Vault HTTP API: /v1/transit/encrypt/:name (ver docs del engine transit)
        var url = $"/v1/transit/encrypt/{keyName}";
        var payload = new { plaintext = b64 };

        var resp = await _http.PostAsJsonAsync(url, payload, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<VaultEncryptResponse>(cancellationToken: ct);
        return json!.data.ciphertext;
    }

    private sealed class VaultEncryptResponse
    {
        public VaultEncryptData data { get; set; } = new();
    }
    private sealed class VaultEncryptData
    {
        public string ciphertext { get; set; } = "";
    }
}
```

### DPAPI local (Windows): protección de secretos cacheados

DPAPI en .NET se documenta como acceso a la API de protección de datos que cifra usando información del equipo o del usuario, evitando generar/almacenar llaves manualmente. citeturn2search2turn2search6  

Ejemplo: cachear el DEK en memoria y persistirlo **solo** cifrado por DPAPI (Machine scope) cuando sea necesario (para recuperación tras reboot).

### Firma MSIX y distribución

MSIX se describe como formato moderno de empaquetado para apps Windows. citeturn9search1  
SignTool se documenta para firmar paquetes y verificar que no se modifiquen tras la firma. citeturn1search1  
App Installer file define dónde está el paquete y cómo se actualiza, soportando HTTPS/HTTP/SMB. citeturn9search2  

### STRIDE: amenazas y mitigaciones mínimas por componente

El modelo STRIDE se usa para clasificar amenazas y simplificar conversaciones de seguridad. citeturn6search0  

Matriz mínima (extracto):

- **Spoofing** (suplantación)
  - Agente: mTLS + mapeo de certificado a device_id; revocación por tabla.
  - Usuario: OIDC + validación JWT iss/aud/exp.
- **Tampering** (modificación)
  - Chunk: hash SHA‑256 + AEAD (AES‑GCM) + idempotencia por chunk_id/hash.
  - DB: constraints/índices + auditoría append-only.
- **Repudiation** (repudio)
  - `audit_log` y correlación de requests por `correlation_id`.
- **Information Disclosure** (divulgación)
  - Cifrado en reposo + control de acceso por roles/permisos; logging sin PII.
- **Denial of Service** (DoS)
  - Rate limiting REST; límites gRPC; backpressure (max_parallel).
- **Elevation of Privilege**
  - Separación de cuentas (servicio vs usuario), ACLs, políticas de autorización por operación.

## Observabilidad, pruebas, CI/CD, IaC y runbooks operativos

### OpenTelemetry en .NET: configuración base y señales

Observabilidad con OpenTelemetry en .NET se documenta con dos enfoques: instrumentación en código y fuera de proceso; el documento oficial enfatiza librería OpenTelemetry como mecanismo configurable. citeturn1search3  

Config mínimo (backend API):

```csharp
// web/src/Audit.Backend.Api/Program.cs (fragmento)
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
  .WithTracing(tracing =>
  {
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddHttpClientInstrumentation();
    tracing.AddOtlpExporter(); // OTLP endpoint por config
  })
  .WithMetrics(metrics =>
  {
    metrics.AddAspNetCoreInstrumentation();
    metrics.AddHttpClientInstrumentation();
    metrics.AddOtlpExporter();
  });

var app = builder.Build();
app.Run();
```

(El artículo oficial muestra implementación y patrones de exportación). citeturn1search3turn1search7  

Alertas recomendadas (small scale):
- `agent.queue.depth > 500` por > 15 min
- `grpc.upload.error_rate > 2%` por 10 min
- `backend.ingest.latency_p95 > 2s` por 10 min
- `objectstore.put_errors > 0` por 5 min

### Estrategia de pruebas con ejemplos (unitarias, integración, E2E, carga)

E2E (portal): Playwright .NET tiene guía de instalación y uso con `dotnet new ...` y ejecución de tests. citeturn7search0  
Carga: k6 documenta “API load testing” y buenas prácticas (smoke, stress, soak). citeturn7search1turn7search4  

Ejemplo Playwright (E2E login + listado devices):

```csharp
// web/tests/Audit.Web.E2E/DeviceListTests.cs
using Microsoft.Playwright;
using Xunit;

public class DeviceListTests
{
    [Fact]
    public async Task Should_Show_Device_List_Page()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        await page.GotoAsync("https://audit.example.local/");

        // Asume que OIDC está mockeado en entorno E2E o que hay bypass de test
        await page.ClickAsync("text=Devices");
        await page.WaitForSelectorAsync("table[data-testid='devices-table']");
    }
}
```

Ejemplo k6 (REST listado):

```javascript
// web/tests/Audit.LoadTests/k6/query_devices.js
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: 10, duration: '5m'
};

export default function () {
  const res = http.get('https://audit.example.local/api/v1/admin/devices', {
    headers: { Authorization: `Bearer ${__ENV.ACCESS_TOKEN}` }
  });

  check(res, { 'status 200': r => r.status === 200 });
}
```

### CI/CD (GitHub Actions) y despliegue a Windows Server (WinRM/Ansible/PowerShell)

Guía oficial de autenticación/cert y hosting:
- Hosting ASP.NET en IIS con proxy inverso a Kestrel está documentado. citeturn1search10turn2search9  
- Para WinRM con Ansible, la doc explica que se requiere listener y `Enable-PSRemoting -Force`. citeturn7search2turn7search5  
- Firma MSIX con SignTool: doc oficial. citeturn1search1  

**Workflow Agent CI (resumen)**:
- build + tests
- build MSIX
- sign MSIX

**Workflow Web CI**:
- build + tests
- publish API/Worker/Portal
- artifact

**Workflow Deploy** (self-hosted runner):
- copiar artefactos al servidor (SMB/WinRM)
- configurar IIS reverse proxy (si aplica)
- instalar/actualizar Windows Services (API/Worker si los corres como servicios)
- reiniciar app pools/servicios

Script PowerShell mínimo para instalar un Worker Service como Windows Service (inspirado en patrón documentado de Worker Service Windows Service). citeturn1search10turn1search2  

```powershell
# web/infra/scripts/Install-WindowsServices.ps1
param(
  [string]$ServiceName,
  [string]$ExePath
)

sc.exe create $ServiceName binPath= "$ExePath" start= auto
sc.exe start  $ServiceName
```

### IaC vendor-neutral: Terraform + Ansible/DSC (estructura y snippets)

Terraform define módulos como colección de recursos para administrar juntos; su estructura se documenta en overview. citeturn7search3turn7search6  
DSC se documenta como plataforma declarativa para configurar sistemas. citeturn1search3turn7search3  

Snippet conceptual (Terraform módulo “apphost”):
- Variables: hostname, ports, cert paths, artifact urls
- Output: fqdn, service names

Ansible (WinRM): la guía describe configuración de WinRM y listeners. citeturn7search2turn7search13  

### Checklist operativo y runbooks (mínimos)

**Checklist seguridad operativo**
- TLS: validar configuración (protocolos/ciphers) y rotación de certificados.
- mTLS: revocar certificados comprometidos (tabla + CRL/OCSP si aplica).
- Object storage:
  - Confirmar lifecycle 365d activo.
  - Verificar legal hold aplicado solo a casos necesarios.
- Backups:
  - DB backups (diario + pruebas de restore).
  - Configuración (IaC + secretos en vault).
- Logs:
  - Evitar PII; conservar auditoría de accesos; logging seguro (guía OWASP). citeturn6search3turn6search11  
- Hardening:
  - Aplicar security baselines. citeturn6search5turn6search1  
- Threat model:
  - Revisar STRIDE por release mayor. citeturn6search0turn6search8  

**Runbook RB-01: ingest backlog**
- Señales: `queue.depth` creciendo, `upload.p95` alto, errores `UNAVAILABLE`.
- Acciones:
  1) Verificar disponibilidad de object storage (PUT/HEAD).
  2) Aumentar `max_parallel` en policy (prudente).
  3) Elevar recursos del Worker (CPU/RAM).
  4) Si persiste: pausar captura por política (modo degradado).

**Runbook RB-02: rotación de certificados**
- Generar nuevo cert por device.
- Actualizar `device_certificates` (insert nuevo, revocar viejo).
- Validar reconexión del agente (heartbeat ok).
- Auditar acciones en `audit_log`.

**Runbook RB-03: retención y legal hold**
- Confirmar lifecycle 365d aplicado.
- Para caso legal:
  - Activar legal hold en prefijo/caso.
  - Registrar `audit_log` de la acción.
- Para liberar:
  - Remover legal hold explícitamente, luego permitir expiración.

**Runbook RB-04: respuesta a incidente**
- Revocar certificados/tokens.
- Congelar casos (legal hold).
- Exportar auditoría y evidencias requeridas.
- Post-mortem y ajuste de políticas.

## Tablas comparativas y recomendaciones finales para implementar ya

### Object storage (on-prem) vs cloud como opción

| Opción | On‑prem | S3‑compatible | Lifecycle | WORM/legal hold | Complejidad operativa | Recomendación small scale |
|---|---:|---:|---:|---:|---:|---|
| MinIO | Sí | Sí | Sí (mc ilm) citeturn4search8turn4search4 | Sí (object lock + legalhold) citeturn5search2turn5search12 | Media | Alta (si ya tienes equipo infra) |
| Ceph RGW | Sí | Sí (doc indica compatibilidad S3) citeturn5search0turn5search4 | Sí (depende config) | Sí (legal hold/bucket ops) citeturn5search1turn5search3 | Alta | Solo si ya existe Ceph |
| S3 | No | Sí (nativo) | Sí (ejemplos lifecycle) citeturn4search2turn4search10 | Sí (Object Lock) citeturn4search3 | Baja | Opción si aceptas cloud |

### Chunking y codecs

| Parámetro | Recomendación | Nota clave |
|---|---|---|
| Chunk duration | 5s | balance entre objetos/overhead y resiliencia |
| Video | H.264 | soportado por encoder Media Foundation citeturn0search5 |
| Audio | loopback WASAPI | doc Win32 loopback citeturn0search4 |
| Transporte | gRPC streaming | doc oficial gRPC .NET citeturn0search3 |

### Elección de DB (small scale)

| DB | Pros | Contras | Recomendación |
|---|---|---|---|
| SQL Server | integración natural Windows Server; tooling | licenciamiento posible | recomendable si ya está disponible |
| PostgreSQL | OSS; rendimiento sólido | operación en Windows puede requerir disciplina | viable si equipo ya opera Postgres |

### Instrucciones concretas para Cursor: build, tests, MSIX, despliegue

**Build y tests**
```bash
# Agent
cd agent
dotnet restore
dotnet test

# Web
cd ../web
dotnet restore
dotnet test
dotnet publish src/Audit.Backend.Api/Audit.Backend.Api.csproj -c Release -o out/api
dotnet publish src/Audit.Backend.Worker/Audit.Backend.Worker.csproj -c Release -o out/worker
dotnet publish src/Audit.Web.Portal/Audit.Web.Portal.csproj -c Release -o out/portal
```

**MSIX**
- MSIX overview y firma con SignTool están documentados; usar `SignTool` para firmar y validar. citeturn9search1turn1search1  
- App Installer file soporta actualización desde web/SMB. citeturn9search2  

```powershell
# Compilar wapproj (ruta ejemplo)
dotnet build .\agent\packaging\msix\Audit.Agent.Package\Audit.Agent.Package.wapproj -c Release

# Firmar MSIX (script del repo, que llama a signtool con SHA256)
.\agent\packaging\signing\Sign-MSIX.ps1 -MsixPath ".\agent\packaging\msix\**\AppPackages\**\*.msix"
```

**Despliegue Windows Server**
- Si usas WinRM+Ansible, habilita PS Remoting (según docs). citeturn7search2  
- En el servidor:
  - Copiar `web/out/*` a `C:\Audit\` (o ruta estándar)
  - Instalar servicios con `Install-WindowsServices.ps1`
  - Configurar IIS reverse proxy si aplica (según hosting guía). citeturn2search9turn2search1