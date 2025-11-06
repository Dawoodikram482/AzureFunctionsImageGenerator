# Weather Image Generator

Azure Functions application that generates weather-overlaid images for Dutch weather stations.

## Live Deployment

**Function App:** https://weatherimg693430-func.azurewebsites.net

**Note:** APIs are publicly accessible - no authentication required.

## Testing the Application

### Option 1: Using VS Code REST Client

1. Open `Http/api.http` in VS Code
2. Click "Send Request" on the POST request to start a job
3. Copy the `jobId` from the response
4. Click "Send Request" on the GET request (replace `{jobId}` with actual ID)

### Option 2: Using cURL or Postman

**Start a job:**
```bash
curl -X POST "https://weatherimg693430-func.azurewebsites.net/api/jobs"
```

**Check status (replace `{jobId}`):**
```bash
curl "https://weatherimg693430-func.azurewebsites.net/api/jobs/{jobId}"
```

**Expected Response:**
- Initial: `"status": "queued"` or `"status": "processing"`
- After ~30-60 seconds: `"status": "completed"`
- Response includes 40 image URLs (one per weather station)

## Local Development Setup

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Azurite](https://www.npmjs.com/package/azurite)
- [Pixabay API key](https://pixabay.com/api/docs/)

### Steps

1. **Install Azurite**
   ```powershell
   npm install -g azurite
   azurite --silent
   ```

2. **Configure local settings**
   
   Copy `local.settings.json.template` to `local.settings.json` and add your Pixabay API key:
   ```powershell
   Copy-Item local.settings.json.template local.settings.json
   ```
   
   Then edit `local.settings.json` and replace `YOUR_PIXABAY_API_KEY_HERE` with your actual key.

3. **Run the app**
   ```powershell
   func start
   ```

4. **Test locally**
   - Use `Http/api-local.http` with VS Code REST Client
   - Or POST to `http://localhost:7071/api/jobs`

## Deployment

```powershell
.\deploy.ps1 -ResourceGroupName "rg-weatherimg" -Location "polandcentral" -PixabayApiKey "YOUR_KEY" -BaseName "yourname"
```

## API Endpoints

### POST /api/jobs
Starts a new weather image generation job.

**Response:**
```json
{
  "jobId": "95902193-87f2-424d-8c70-036a4a5d4411",
  "message": "Job created successfully",
  "statusUrl": "https://weatherimg693430-func.azurewebsites.net/api/jobs/95902193-87f2-424d-8c70-036a4a5d4411"
}
```

### GET /api/jobs/{jobId}
Returns job status and image URLs.

**Response:**
```json
{
  "jobId": "95902193-87f2-424d-8c70-036a4a5d4411",
  "status": "completed",
  "totalStations": 40,
  "processedStations": 40,
  "progressPercentage": 100,
  "imageUrls": [
    "http://127.0.0.1:10000/devstoreaccount1/weather-images/95902193.../6240.jpg",
    "..."
  ]
}
```

## Architecture

1. **POST /api/jobs** → Creates job → Queue message to `process-job-queue`
2. **ProcessJobQueue** → Fetches 40 Buienradar stations → 40 messages to `process-station-queue`
3. **ProcessStationQueue** (parallel) → Fetch Pixabay image → Overlay weather → Upload blob → Update status
4. **GET /api/jobs/{jobId}** → Returns progress and image URLs
