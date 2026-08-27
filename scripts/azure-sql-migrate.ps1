param(
    [string]$Server = "taha.database.windows.net",
    [string]$Database = "MarketOtomasyon",
    [string]$UserName = "marketadmin"
)

$ErrorActionPreference = "Stop"
$projectDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) "MarketOtomasyon"
$applicationDll = Join-Path $projectDirectory "bin\Release\net8.0\MarketOtomasyon.dll"

$previousEnvironment = [Environment]::GetEnvironmentVariable(
    "ASPNETCORE_ENVIRONMENT",
    "Process"
)
$previousConnection = [Environment]::GetEnvironmentVariable(
    "ConnectionStrings__MarketDb",
    "Process"
)

$securePassword = Read-Host "Azure SQL parolasını girin" -AsSecureString
$credential = [PSCredential]::new($UserName, $securePassword)
$plainPassword = $credential.GetNetworkCredential().Password

if ([string]::IsNullOrWhiteSpace($plainPassword)) {
    throw "Parola boş bırakılamaz."
}

$connectionString = "Server=tcp:$Server,1433;Initial Catalog=$Database;" +
    "User ID=$UserName;Password=$plainPassword;Encrypt=True;" +
    "TrustServerCertificate=False;Connection Timeout=90;"

try {
    [Environment]::SetEnvironmentVariable(
        "ASPNETCORE_ENVIRONMENT",
        "Production",
        "Process"
    )
    [Environment]::SetEnvironmentVariable(
        "ConnectionStrings__MarketDb",
        $connectionString,
        "Process"
    )

    if (-not (Test-Path -LiteralPath $applicationDll)) {
        Write-Host "Release çıktısı bulunamadı; proje derleniyor..."
        dotnet build (Join-Path $projectDirectory "MarketOtomasyon.csproj") `
            -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Proje derlenemedi."
        }
    }

    Push-Location $projectDirectory
    try {
        dotnet $applicationDll migrate
        if ($LASTEXITCODE -ne 0) {
            throw "Migration başarısız oldu."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    [Environment]::SetEnvironmentVariable(
        "ConnectionStrings__MarketDb",
        $previousConnection,
        "Process"
    )
    [Environment]::SetEnvironmentVariable(
        "ASPNETCORE_ENVIRONMENT",
        $previousEnvironment,
        "Process"
    )

    $plainPassword = $null
    $connectionString = $null
    $credential = $null
    $securePassword = $null
}
