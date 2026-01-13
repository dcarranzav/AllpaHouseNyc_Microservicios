# Script para ejecutar el stored procedure en SQL Server
# Usa este script si tienes SQL Server instalado localmente

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EJECUTAR SP EN SQL SERVER" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$sqlFile = "SQL/sp_insertarPagoIntegracion.sql"

if (!(Test-Path $sqlFile)) {
    Write-Host "❌ Error: No se encontró $sqlFile" -ForegroundColor Red
    exit 1
}

Write-Host "📄 Archivo SQL encontrado: $sqlFile" -ForegroundColor Green
Write-Host ""

# Configuración de conexión
$server = "db31651.public.databaseasp.net"
$database = "db31651"
$username = "db31651"
$password = "prueba2020d"

Write-Host "🔐 Conectando a SQL Server..." -ForegroundColor Yellow
Write-Host "   Server: $server" -ForegroundColor Gray
Write-Host "   Database: $database" -ForegroundColor Gray
Write-Host ""

try {
    # Leer el archivo SQL
    $sqlContent = Get-Content $sqlFile -Raw

    # Crear conexión
    $connectionString = "Server=$server;Database=$database;User Id=$username;Password=$password;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    
    # Abrir conexión
    $connection.Open()
    Write-Host "✅ Conectado exitosamente" -ForegroundColor Green
    Write-Host ""

    # Ejecutar el script
    Write-Host "🚀 Ejecutando stored procedure..." -ForegroundColor Yellow
    $command = $connection.CreateCommand()
    $command.CommandText = $sqlContent
    $command.CommandTimeout = 120  # 2 minutos timeout
    
    $command.ExecuteNonQuery() | Out-Null
    
    Write-Host "✅ Stored procedure creado exitosamente" -ForegroundColor Green
    Write-Host ""

    # Verificar que existe
    Write-Host "🔍 Verificando..." -ForegroundColor Yellow
    $verifyCommand = $connection.CreateCommand()
    $verifyCommand.CommandText = "SELECT OBJECT_ID('dbo.sp_insertarPagoIntegracion');"
    $objectId = $verifyCommand.ExecuteScalar()

    if ($objectId -ne [DBNull]::Value -and $objectId -ne $null) {
        Write-Host "✅ Verificación exitosa. Object ID: $objectId" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Advertencia: No se pudo verificar el SP" -ForegroundColor Yellow
    }

    $connection.Close()

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " ✅ STORED PROCEDURE LISTO" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Siguiente paso:" -ForegroundColor White
    Write-Host "  .\update-render.ps1" -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "❌ Error al ejecutar el stored procedure:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternativa:" -ForegroundColor Yellow
    Write-Host "  1. Abrir SQL Server Management Studio" -ForegroundColor Gray
    Write-Host "  2. Conectar a: $server" -ForegroundColor Gray
    Write-Host "  3. Abrir el archivo: $sqlFile" -ForegroundColor Gray
    Write-Host "  4. Ejecutar (F5)" -ForegroundColor Gray
    exit 1
}
