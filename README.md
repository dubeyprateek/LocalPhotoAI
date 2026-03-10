# LocalPhotoAI

# LocalPhotoAI

A local-first, AI-powered photo processing application built with .NET 9.

Upload photos from your desktop or mobile device over your local network, queue them for AI-powered processing, browse results in a web gallery, and download edited images -- all running privately on your own machine with zero cloud dependencies.

---

## How It Works (3 Steps)

```
1. DESCRIBE          2. UPLOAD              3. DOWNLOAD
+--------------+     +----------------+     +------------------+
| Type what    | --> | Drop your      | --> | Preview results, |
| you want:    |     | photos here    |     | select & download|
| "vintage",   |     | (drag & drop   |     | your edited      |
| "enhance",   |     |  or tap to     |     | photos           |
| "warm tones" |     |  browse)       |     |                  |
+--------------+     +----------------+     +------------------+
```

1. **Describe your edit** -- Type what you want in plain English (e.g., *"make it vintage"*, *"enhance"*, *"warm tones"*, *"grayscale and sharpen"*). Optionally click **Refine Prompt** for AI-powered rewording.
2. **Upload your photos** -- Drag and drop or tap to select images. They are queued and processed automatically in the background.
3. **Preview and download** -- Processed images appear as thumbnails in the output gallery. Select the ones you want and download them.

Your input photos stay visible throughout processing. No internet needed -- everything runs on your machine.

---

## Supported Transformations

Type any of these in your prompt. You can combine multiple in one request (e.g., *"grayscale and sharpen"*).

| What You Type | What It Does |
|---|---|
| `grayscale`, `greyscale`, `black and white`, `b&w`, `monochrome` | Convert to grayscale |
| `sepia`, `vintage`, `retro`, `old photo`, `aged` | Apply sepia/vintage tone |
| `blur 5`, `soft`, `dreamy`, `glow` | Gaussian blur (optional radius) |
| `sharpen`, `crisp`, `detail` | Sharpen edges |
| `brighten 20`, `brightness` | Increase brightness (optional percent) |
| `darken 15` | Decrease brightness |
| `contrast 30`, `dramatic`, `cinematic` | Adjust contrast |
| `warm`, `sunset`, `golden` | Warm color shift (boost reds) |
| `cool`, `cold`, `blue tone` | Cool color shift (boost blues) |
| `saturate`, `vivid`, `pop`, `vibrant`, `colorful` | Boost color saturation |
| `enhance`, `improve`, `auto`, `fix`, `optimize` | Auto-enhance (brighten + contrast + sharpen) |
| `invert`, `negative` | Invert colors |
| `resize 800x600` | Resize to specific dimensions |
| `rotate 90` | Rotate by degrees |
| `flip horizontal`, `mirror` | Flip horizontally |
| `flip vertical` | Flip vertically |

Unrecognized prompts automatically apply a subtle enhance (brighten + contrast + sharpen) instead of defaulting to grayscale.

---

## Download and Run (No .NET Required)

Grab the latest pre-built release from GitHub and run it immediately -- no .NET SDK or runtime needed:

1. Go to the [**Releases**](https://github.com/dubeyprateek/LocalPhotoAI/releases/latest) page
2. Download the zip for your platform:
   - **Windows:** `LocalPhotoAI-win-x64.zip`
   - **Linux:** `LocalPhotoAI-linux-x64.zip`
   - **macOS:** `LocalPhotoAI-osx-x64.zip`
3. Extract and run:

```bash
# Windows
LocalPhotoAI.Host.exe

# Linux / macOS
chmod +x LocalPhotoAI.Host
./LocalPhotoAI.Host
```

The app starts on `http://localhost:5100`, auto-opens your browser, and shows a QR code for mobile access.

---

## What This Project Does

- **Real image processing** -- SkiaSharp-powered pipeline with 15+ transformations: grayscale, sepia, blur, sharpen, warm, cool, saturate, enhance, invert, resize, rotate, flip, and more.
- **Natural-language prompts** -- Type *"make it vintage"* or *"enhance and sharpen"* -- the system maps your words to the right operations.
- **AI prompt refinement** -- Optionally connect any OpenAI-compatible API (OpenAI, Groq, Gemini, GitHub Models, Ollama) to intelligently rewrite prompts.
- **Upload photos** -- Drag-and-drop or tap-to-select images from any device on your LAN (supports `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.webp`, `.heic`, `.heif`, `.tiff`, `.tif`).
- **Automatic job queuing** -- Every upload is automatically queued for background processing.
- **Track processing status** -- Monitor job progress in real time (Queued -> Processing -> Succeeded / Failed).
- **Browse your gallery** -- View all uploaded and processed photos in the web UI, with auto-refresh.
- **Input gallery persists** -- Uploaded thumbnails remain visible throughout processing.
- **Output preview** -- Processed images show as thumbnails; unfinished images show a placeholder.
- **Download results** -- Select and download individual or all processed photos.
- **Connect from mobile** -- Scan a QR code or use the LAN URL to access the app from your phone/tablet.
- **Run fully offline** -- Everything runs locally. No internet, no cloud accounts, no data leaving your machine.
- **Create editing sessions** -- Describe your desired edit with a natural-language prompt, and the app organizes uploads and outputs under a session.
- **Session management** -- Delete sessions and their files, auto-cleanup downloaded sessions on new session start.

---

## Technology Stack

| Component | Technology |
|---|---|
| Runtime | .NET 9 |
| Language | C# 13 |
| Web framework | ASP.NET Core Minimal APIs |
| Image processing | SkiaSharp (SkiaImagePipeline) |
| AI prompt refinement | OpenAI-compatible API (OpenAI, Groq, Gemini, GitHub Models, Ollama) |
| API Gateway | YARP (Yet Another Reverse Proxy) |
| Background processing | `BackgroundService` |
| Job queue | `System.Threading.Channels` |
| Data persistence | JSON files + `ConcurrentDictionary` |
| QR code generation | QRCoder |
| Testing | xUnit + `WebApplicationFactory` |
| Deployment | Self-contained single-file publish |

---

## Architecture

LocalPhotoAI ships in **two deployment modes**: a monolithic single-process host (ideal for personal use) and a microservices split (for development and scaling).

### High-Level Architecture

```
+------------------------------------------------------------------+
|                        Web Browser / Mobile                      |
|                     (index.html -- static SPA)                   |
+------------------------------------------------------------------+
             |  HTTP                                 |  HTTP
             v                                       v
+----------------------------+              +------------------------+
|   Monolith Mode            |              |   Microservices Mode   |
|  (LocalPhotoAI.Host)       |              |     (Gateway + 4       |
|   Single process,          |              |      services)         |
|   port 5100                |              |   Gateway -> YARP      |
+----------------------------+              +------------------------+
```

### Monolith Mode -- LocalPhotoAI.Host

A single ASP.NET Core process that bundles all functionality. Best for personal desktop use and single-file deployment.

```
LocalPhotoAI.Host (port 5100)
|-- Static files (wwwroot/index.html)
|-- POST /api/uploads                   -- Upload photos
|-- POST /api/sessions                  -- Create editing session
|-- POST /api/sessions/{id}/upload      -- Upload into session
|-- POST /api/sessions/{id}/refine      -- Re-refine session prompt
|-- POST /api/prompt/refine             -- Refine a prompt
|-- GET  /api/sessions                  -- List sessions
|-- GET  /api/sessions/{id}             -- Get session
|-- GET  /api/sessions/{id}/results     -- Session output photos
|-- GET  /api/sessions/{id}/qr          -- Session QR code
|-- DELETE /api/sessions/{id}           -- Delete session & files
|-- GET  /api/jobs                      -- List all jobs
|-- GET  /api/jobs/{id}                 -- Get job status
|-- GET  /api/photos                    -- List all photos
|-- GET  /api/photos/{id}               -- Get photo metadata
|-- GET  /api/photos/{id}/download      -- Download photo
|-- GET  /api/connection-info           -- LAN IP & hostname
|-- GET  /api/qr                        -- QR code (SVG)
|-- GET  /health                        -- Health check
|-- BackgroundService                   -- PhotoProcessingWorker
|-- BackgroundService                   -- BrowserLauncherService
```

### Microservices Mode -- Gateway + Services

The same functionality split across independently deployable services, connected via a YARP reverse proxy gateway.

```
Gateway (port 5100)                    --- YARP Reverse Proxy ---
|-- Static files (wwwroot/index.html)
|-- GET  /api/gateway/status
|-- GET  /api/gateway/connection-info
|-- GET  /api/gateway/qr
|-- GET  /health
|
|--- UploadService (port 5101)
|    |-- POST /api/uploads
|    |-- GET  /health
|
|--- JobService (port 5102)
|    |-- GET  /api/jobs
|    |-- GET  /api/jobs/{id}
|    |-- GET  /health
|
|--- GalleryService (port 5103)
|    |-- GET  /api/photos
|    |-- GET  /api/photos/{id}
|    |-- GET  /api/photos/{id}/download
|    |-- GET  /health
|
|--- WorkerService (headless)
     |-- BackgroundService (Worker)
```

### Project Structure

```
LocalPhotoAI/
|-- LocalPhotoAI.sln
|-- publish.ps1                         # Build self-contained executable
|-- README.md
|-- .gitignore
|
|-- docs/
|   |-- lan-discovery.md                # LAN/mDNS/QR connectivity guide
|
|-- src/
|   |-- LocalPhotoAI.Shared/            # Shared library (all projects reference this)
|   |   |-- Models/
|   |   |   |-- PhotoMetadata.cs        # Photo record with version history
|   |   |   |-- JobRecord.cs            # Job tracking (status, progress, errors)
|   |   |   |-- SessionRecord.cs        # Session record (prompt, folders, status)
|   |   |   |-- UploadResponse.cs       # Upload API response DTO
|   |   |-- Storage/
|   |   |   |-- IPhotoStore.cs          # Photo metadata store interface
|   |   |   |-- IJobStore.cs            # Job metadata store interface
|   |   |   |-- ISessionStore.cs        # Session metadata store interface
|   |   |   |-- JsonPhotoStore.cs       # JSON file-backed photo store
|   |   |   |-- JsonJobStore.cs         # JSON file-backed job store
|   |   |   |-- JsonSessionStore.cs     # JSON file-backed session store
|   |   |-- Queue/
|   |   |   |-- IJobQueue.cs            # Job queue interface
|   |   |   |-- InMemoryJobQueue.cs     # Channel-based in-memory queue
|   |   |   |-- QueueMessage.cs         # Queue message DTO
|   |   |-- Pipelines/
|   |   |   |-- IImagePipeline.cs       # Image processing pipeline interface
|   |   |   |-- SkiaImagePipeline.cs    # SkiaSharp image pipeline (15+ transforms)
|   |   |   |-- StubPipeline.cs         # Stub pipeline (copies file as-is)
|   |   |   |-- IPromptRefiner.cs       # Prompt refinement interface
|   |   |   |-- OpenAIPromptRefiner.cs  # OpenAI-compatible prompt refiner
|   |   |   |-- StubPromptRefiner.cs    # Stub refiner (fallback)
|   |   |-- Security/
|   |   |   |-- FileValidation.cs       # Extension allowlist & filename sanitization
|   |   |-- Middleware/
|   |       |-- CorrelationIdMiddleware.cs  # X-Correlation-Id request tracing
|   |
|   |-- LocalPhotoAI.Host/              # Monolith (all-in-one)
|   |   |-- Program.cs
|   |   |-- PhotoProcessingWorker.cs     # BackgroundService for job processing
|   |   |-- BrowserLauncherService.cs    # Auto-opens browser on startup
|   |   |-- NetworkHelper.cs             # LAN IP detection utility
|   |   |-- wwwroot/index.html           # Web UI
|   |
|   |-- Gateway/                         # API Gateway (YARP reverse proxy)
|   |   |-- Program.cs
|   |   |-- appsettings.json             # YARP route/cluster config
|   |   |-- wwwroot/index.html           # Web UI
|   |
|   |-- UploadService/                   # Photo upload microservice
|   |   |-- Program.cs
|   |
|   |-- JobService/                      # Job tracking microservice
|   |   |-- Program.cs
|   |
|   |-- GalleryService/                  # Photo gallery microservice
|   |   |-- Program.cs
|   |
|   |-- WorkerService/                   # Background processing worker
|       |-- Program.cs
|       |-- Worker.cs                    # BackgroundService implementation
|
|-- tests/
|-- LocalPhotoAI.Tests/
    |-- SkiaImagePipelineTests.cs    # 59 tests: transforms, parsing, keywords
    |-- OpenAIPromptRefinerTests.cs  # API fallback, parsing, code fence handling
    |-- FileValidationTests.cs       # Extension & sanitization tests
    |-- StubPipelineTests.cs         # Pipeline copy tests
    |-- StubPromptRefinerTests.cs    # Prompt refiner tests
    |-- InMemoryJobQueueTests.cs     # Queue ordering & concurrency tests
    |-- JsonStoreTests.cs            # Persistence round-trip tests
    |-- SessionStoreTests.cs         # Session store tests
    |-- UploadServiceIntegrationTests.cs  # HTTP integration tests
    |-- HostIntegrationTests.cs      # Full monolith endpoint tests
```

### Shared Library -- `LocalPhotoAI.Shared`

All projects reference this shared library. It contains:

| Component | Description |
|---|---|
| `IPhotoStore` / `JsonPhotoStore` | Photo metadata persistence (JSON file-backed, `ConcurrentDictionary` + `SemaphoreSlim` for thread safety) |
| `IJobStore` / `JsonJobStore` | Job record persistence (same pattern) |
| `ISessionStore` / `JsonSessionStore` | Session record persistence (same pattern) |
| `IJobQueue` / `InMemoryJobQueue` | Producer/consumer job queue using `System.Threading.Channels` |
| `IImagePipeline` / `SkiaImagePipeline` | SkiaSharp image pipeline with 15+ prompt-driven transforms (grayscale, sepia, blur, sharpen, warm, cool, saturate, enhance, invert, resize, rotate, flip) |
| `StubPipeline` | Stub pipeline (copies file as-is, for testing) |
| `IPromptRefiner` / `OpenAIPromptRefiner` | AI prompt refiner using any OpenAI-compatible API (configurable base URL) |
| `StubPromptRefiner` | Stub refiner fallback (template wrapping, no AI required) |
| `FileValidation` | Filename sanitization (strips path traversal) and extension allowlist |
| `CorrelationIdMiddleware` | Injects `X-Correlation-Id` header for end-to-end request tracing |

### Code Flow

```
1. User creates a session via web UI with a natural-language prompt
       |
       v
2. Server refines the prompt (IPromptRefiner), creates session folders,
   and returns a SessionRecord
       |
       v
3. User uploads photo(s) into the session
       |
       v
4. Server validates extension, sanitizes filename,
   saves original to session InputImages/ folder,
   creates PhotoMetadata & JobRecord, enqueues a QueueMessage
       |
       v
5. PhotoProcessingWorker dequeues message, runs IImagePipeline
   with the session prompt, writes output to OutputImages/ folder,
   updates JobRecord status & PhotoMetadata versions
       |
       v
6. When all jobs finish, session status moves to Completed or Failed
       |
       v
7. Gallery / session results endpoints serve photo list & downloads
       |
       v
8. Web UI auto-refreshes to show processing progress
```

### Storage Layout

All data is stored under the configured `StoragePath` (default: `./storage`):

```
storage/
|-- photos.json          # PhotoMetadata records
|-- jobs.json            # JobRecord records
|-- sessions.json        # SessionRecord records
|-- originals/
|   |-- {photoId}/
|       |-- original.jpg
|-- edited/
|   |-- {photoId}/
|       |-- edited.jpg
|-- sessions/
    |-- {title}_{timestamp}/
        |-- InputImages/
        |   |-- {photoId}.jpg
        |-- OutputImages/
            |-- {photoId}.jpg
```

---

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Quick Start -- Monolith Mode

```bash
# Clone and run
git clone https://github.com/dubeyprateek/LocalPhotoAI.git
cd LocalPhotoAI
dotnet run --project src/LocalPhotoAI.Host
```

The app starts on `http://localhost:5100`, auto-opens your browser, and displays a QR code for mobile access.

### Quick Start -- Microservices Mode

Start all services (each in a separate terminal):

```bash
# Terminal 1 -- Gateway (port 5100)
dotnet run --project src/Gateway

# Terminal 2 -- Upload Service (port 5101)
dotnet run --project src/UploadService

# Terminal 3 -- Job Service (port 5102)
dotnet run --project src/JobService

# Terminal 4 -- Gallery Service (port 5103)
dotnet run --project src/GalleryService

# Terminal 5 -- Worker Service (headless)
dotnet run --project src/WorkerService
```

Open `http://localhost:5100` in your browser. The Gateway proxies all API calls to the backend services via YARP.

---

## Configuration

Settings can be overridden via `appsettings.json`, environment variables, or command-line arguments.

| Setting | Default | Description |
|---|---|---|
| `StoragePath` | `./storage` | Directory for photos, metadata JSON files |
| `MaxUploadSizeMB` | `50` | Maximum upload file size in MB |
| `Urls` | `http://0.0.0.0:5100` | Kestrel listening address and port |
| `Pipeline` | `skia` | Image pipeline (`skia` or `stub`) |
| `AI:ApiKey` | *(none)* | API key for AI prompt refinement (falls back to stub if unset) |
| `AI:Model` | `gpt-4o-mini` | Model name for the AI provider |
| `AI:BaseUrl` | `https://api.openai.com/v1/chat/completions` | Chat completions endpoint URL |
| `OpenAI:ApiKey` | *(none)* | Legacy alias for `AI:ApiKey` |
| `OpenAI:Model` | *(none)* | Legacy alias for `AI:Model` |

### AI Provider Examples

The prompt refiner works with any OpenAI-compatible chat completions API:

```jsonc
// appsettings.json -- OpenAI
{ "AI": { "ApiKey": "sk-...", "Model": "gpt-4o-mini" } }

// Groq (free tier: 30 RPM / 14,400 req/day)
{ "AI": { "ApiKey": "gsk_...", "Model": "llama-3.3-70b-versatile", "BaseUrl": "https://api.groq.com/openai/v1/chat/completions" } }

// Google Gemini (free tier: 15 RPM / 1M tokens/day)
{ "AI": { "ApiKey": "AIza...", "Model": "gemini-2.0-flash", "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions" } }

// GitHub Models (free with GitHub account)
{ "AI": { "ApiKey": "ghp_...", "Model": "gpt-4o-mini", "BaseUrl": "https://models.inference.ai.azure.com/chat/completions" } }

// Ollama (local, free, no API key needed -- use any non-empty string)
{ "AI": { "ApiKey": "ollama", "Model": "llama3.2", "BaseUrl": "http://localhost:11434/v1/chat/completions" } }
```

No AI key? No problem -- the app works fully offline with the stub refiner and the built-in keyword parser.

**Examples:**

```bash
# Environment variable
export StoragePath="/data/photos"
dotnet run --project src/LocalPhotoAI.Host

# Command-line
dotnet run --project src/LocalPhotoAI.Host -- --StoragePath "/data/photos"
```

### Port Assignments (Microservices Mode)

| Service | Port |
|---|---|
| Gateway | `5100` |
| UploadService | `5101` |
| JobService | `5102` |
| GalleryService | `5103` |
| WorkerService | N/A (headless) |

---

## API Reference

### Upload

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/uploads` | Upload one or more photos (multipart/form-data) |

**Request:** `multipart/form-data` with one or more `files` fields.

**Response (200):**
```json
{
  "files": [
    { "photoId": "abc123", "originalFileName": "photo.jpg", "jobId": "def456" }
  ]
}
```

**Errors:** `400` for missing files, disallowed extensions, or non-multipart requests.

### Sessions

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/sessions` | Create a new editing session with a prompt |
| `POST` | `/api/sessions/{sessionId}/upload` | Upload photos into a session (multipart/form-data) |
| `POST` | `/api/sessions/{sessionId}/refine` | Re-refine the prompt for a session |
| `GET` | `/api/sessions` | List all sessions |
| `GET` | `/api/sessions/{sessionId}` | Get a specific session |
| `GET` | `/api/sessions/{sessionId}/results` | Get output photos for a session |
| `GET` | `/api/sessions/{sessionId}/qr` | QR code (SVG) linking to session results |
| `DELETE` | `/api/sessions/{sessionId}` | Delete session, photos, jobs, and files |

**Session statuses:** `Draft` -> `Processing` -> `Completed` / `Failed`

### Prompt Refinement

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/prompt/refine` | Refine a user prompt (returns refined text and title) |

### Jobs

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/jobs` | List all jobs |
| `GET` | `/api/jobs/{jobId}` | Get a specific job's status |

**Job statuses:** `Queued` -> `Processing` -> `Succeeded` / `Failed`

### Gallery

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/photos` | List all photo metadata |
| `GET` | `/api/photos/{photoId}` | Get a specific photo's metadata |
| `GET` | `/api/photos/{photoId}/download` | Download the latest version (or original) |

### Connectivity

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/connection-info` | LAN IP, hostname, mDNS URL |
| `GET` | `/api/qr` | QR code as SVG for mobile access |
| `GET` | `/health` | Health check endpoint |

---

## Publishing a Self-Contained Executable

Use the included `publish.ps1` script to create a single-file, self-contained executable with no .NET runtime dependency on the target machine:

```powershell
# Default: Windows x64
./publish.ps1

# Linux
./publish.ps1 -Runtime linux-x64

# macOS (Apple Silicon)
./publish.ps1 -Runtime osx-arm64

# Custom output directory
./publish.ps1 -OutputDir "dist"
```

The output goes to the `publish/` folder. Copy this folder to any machine and run:

```bash
./publish/LocalPhotoAI.Host.exe       # Windows
./publish/LocalPhotoAI.Host            # Linux / macOS
```

No .NET SDK or runtime is required on the target machine.

---

## Running Tests

```bash
dotnet test
```

### Test Coverage

| Test Class | What It Covers |
|---|---|
| `SkiaImagePipelineTests` | 59 tests: all transforms, keyword parsing, variant recognition, numeric args, multi-op combos |
| `OpenAIPromptRefinerTests` | API key fallback, HTTP failure fallback, JSON parsing, code fence stripping |
| `FileValidationTests` | Extension allowlist, filename sanitization, path traversal prevention |
| `StubPipelineTests` | File copy pipeline, output directory creation |
| `StubPromptRefinerTests` | Prompt refinement output, title generation |
| `InMemoryJobQueueTests` | FIFO ordering, competing consumer correctness |
| `JsonStoreTests` | Save/retrieve/update round-trips, cross-instance persistence |
| `SessionStoreTests` | Session save, retrieve, update, persistence |
| `UploadServiceIntegrationTests` | Full HTTP upload flow, error responses, file-on-disk verification |
| `HostIntegrationTests` | End-to-end monolith endpoint tests (sessions, jobs, photos, QR, prompt) |

---

## LAN and Mobile Access

See [`docs/lan-discovery.md`](docs/lan-discovery.md) for detailed connectivity instructions including:

- **Automatic browser launch** on the host machine
- **IP address** fallback for reliable LAN access
- **QR code** scanning from mobile devices
- **mDNS / `.local` hostname** (Windows 10+ / macOS / Linux with Avahi)

---

## Extending the Image Pipeline

The `IImagePipeline` interface is the extension point for image processing:

```csharp
public interface IImagePipeline
{
    string Name { get; }
    Task<PipelineResult> RunAsync(string inputPath, string outputDir, string? prompt = null, CancellationToken cancellationToken = default);
}
```

The default `SkiaImagePipeline` handles 15+ transformations via SkiaSharp. To add a custom pipeline:

1. Create a new class implementing `IImagePipeline`
2. Register it in DI: `builder.Services.AddSingleton<IImagePipeline, YourPipeline>()`
3. Set `Pipeline` config to your pipeline name
4. The worker will automatically use it for all new jobs

Set `Pipeline` to `stub` in config to use the no-op `StubPipeline` (copies files as-is, useful for testing).

---

## Security

- **File extension allowlist** -- Only image formats are accepted (`.jpg`, `.png`, `.gif`, `.bmp`, `.webp`, `.heic`, `.heif`, `.tiff`, `.tif`)
- **Filename sanitization** -- Path traversal characters and invalid filename characters are stripped
- **Correlation IDs** -- Every request is tagged with `X-Correlation-Id` for tracing
- **Local-only by default** -- No internet access required; data never leaves your machine

---

## License

This project is for personal/educational use. See the repository for license details.
