param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$Location,
    
    [Parameter(Mandatory=$true)]
    [string]$PixabayApiKey,
    
    [Parameter(Mandatory=$false)]
    [string]$BaseName = "weatherimg"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Weather Image Generator Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Variables
$ProjectPath = $PSScriptRoot
$BicepTemplatePath = Join-Path $ProjectPath "Infrastructure\main.bicep"
$OutputPath = Join-Path $ProjectPath "bin\Release\net8.0"
$PublishPath = Join-Path $ProjectPath "publish"
$ZipPath = Join-Path $ProjectPath "publish.zip"

# Step 1: Build the project
Write-Host "Step 1: Building the project..." -ForegroundColor Yellow
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build completed successfully." -ForegroundColor Green
Write-Host ""

# Step 2: Publish the project
Write-Host "Step 2: Publishing the project..." -ForegroundColor Yellow
dotnet publish --configuration Release --output $PublishPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Publish completed successfully." -ForegroundColor Green
Write-Host ""

# Step 3: Create zip file for deployment
Write-Host "Step 3: Creating deployment package..." -ForegroundColor Yellow
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}
Compress-Archive -Path "$PublishPath\*" -DestinationPath $ZipPath
Write-Host "Deployment package created." -ForegroundColor Green
Write-Host ""

# Step 4: Login to Azure (if not already logged in)
Write-Host "Step 4: Checking Azure login..." -ForegroundColor Yellow
$account = az account show 2>$null
if (-not $account) {
    Write-Host "Not logged in. Logging in to Azure..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Azure login failed!" -ForegroundColor Red
        exit 1
    }
}
Write-Host "Azure login verified." -ForegroundColor Green
Write-Host ""

# Step 5: Create Resource Group
Write-Host "Step 5: Creating resource group..." -ForegroundColor Yellow
az group create --name $ResourceGroupName --location $Location
if ($LASTEXITCODE -ne 0) {
    Write-Host "Resource group creation failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Resource group created/verified." -ForegroundColor Green
Write-Host ""

# Step 6: Deploy Bicep template
Write-Host "Step 6: Deploying Azure resources with Bicep..." -ForegroundColor Yellow
$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $BicepTemplatePath `
    --parameters baseName=$BaseName pixabayApiKey=$PixabayApiKey `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Host "Bicep deployment failed!" -ForegroundColor Red
    exit 1
}

$functionAppName = $deploymentOutput.properties.outputs.functionAppName.value
$functionAppUrl = $deploymentOutput.properties.outputs.functionAppUrl.value

Write-Host "Azure resources deployed successfully." -ForegroundColor Green
Write-Host "Function App Name: $functionAppName" -ForegroundColor Cyan
Write-Host ""

# Step 7: Deploy Function App code
Write-Host "Step 7: Deploying function app code..." -ForegroundColor Yellow
az functionapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $functionAppName `
    --src $ZipPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Function app deployment failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Function app code deployed successfully." -ForegroundColor Green
Write-Host ""

# Step 8: Get function keys
Write-Host "Step 8: Retrieving function keys..." -ForegroundColor Yellow
Start-Sleep -Seconds 10  # Wait for deployment to complete

$functionKey = az functionapp keys list `
    --resource-group $ResourceGroupName `
    --name $functionAppName `
    --query "functionKeys.default" `
    --output tsv

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Deployment Completed Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Function App URL: $functionAppUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "API Endpoints:" -ForegroundColor Yellow
Write-Host "  Start Job: POST $functionAppUrl/api/jobs?code=$functionKey" -ForegroundColor White
Write-Host "  Get Status: GET $functionAppUrl/api/jobs/{jobId}?code=$functionKey" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test the API using the HTTP files in the Http folder" -ForegroundColor White
Write-Host "2. Replace {functionAppUrl} and {functionKey} in the .http files" -ForegroundColor White
Write-Host ""

# Cleanup
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}
if (Test-Path $PublishPath) {
    Remove-Item $PublishPath -Recurse -Force
}
