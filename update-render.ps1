# Script para actualizar el código en GitHub y triggear redespliegue en Render

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " ACTUALIZANDO CÓDIGO EN GITHUB" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Agregar todos los cambios
Write-Host "[1/4] Agregando cambios..." -ForegroundColor Yellow
git add .

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al agregar archivos" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Cambios agregados" -ForegroundColor Green
Write-Host ""

# 2. Commit
Write-Host "[2/4] Creando commit..." -ForegroundColor Yellow
$mensaje = "feat: Inserción automática de pagos en reservas de integración"
git commit -m $mensaje

if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  No hay cambios para commit o error" -ForegroundColor Yellow
} else {
    Write-Host "✅ Commit creado: $mensaje" -ForegroundColor Green
}

Write-Host ""

# 3. Push
Write-Host "[3/4] Subiendo a GitHub..." -ForegroundColor Yellow
git push

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al subir a GitHub" -ForegroundColor Red
    Write-Host "Verifica tu conexión y autenticación" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Código subido a GitHub" -ForegroundColor Green
Write-Host ""

# 4. Información
Write-Host "[4/4] Siguiente paso" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  IMPORTANTE: Ejecutar el stored procedure en SQL Server" -ForegroundColor Yellow
Write-Host ""
Write-Host "Opción 1: Usar el script PowerShell" -ForegroundColor White
Write-Host "  .\ejecutar-sp.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "Opción 2: Ejecutar manualmente" -ForegroundColor White
Write-Host "  1. Abrir SQL Server Management Studio" -ForegroundColor Gray
Write-Host "  2. Conectar a: db31651.public.databaseasp.net" -ForegroundColor Gray
Write-Host "  3. Abrir: SQL/sp_insertarPagoIntegracion.sql" -ForegroundColor Gray
Write-Host "  4. Ejecutar (F5)" -ForegroundColor Gray
Write-Host ""
Write-Host "✨ Render detectará el cambio automáticamente" -ForegroundColor Cyan
Write-Host "⏳ Espera 5-7 minutos mientras redesplega" -ForegroundColor Cyan
Write-Host ""
Write-Host "📊 Monitorea el progreso en:" -ForegroundColor White
Write-Host "   https://dashboard.render.com" -ForegroundColor Blue
Write-Host ""
Write-Host "🔍 Servicios que se redespliegan:" -ForegroundColor White
Write-Host "   - ApiGateway" -ForegroundColor Yellow
Write-Host ""
Write-Host "📝 Cambios aplicados:" -ForegroundColor White
Write-Host "   ✅ Creado sp_insertarPagoIntegracion" -ForegroundColor Green
Write-Host "   ✅ Inserción automática de pagos al confirmar reserva" -ForegroundColor Green
Write-Host "   ✅ Cancelación retorna montoPagado correctamente" -ForegroundColor Green
Write-Host "   ✅ Llamadas directas a stored procedures (no RECA API)" -ForegroundColor Green
Write-Host ""
Write-Host "🧪 Después del redespliegue prueba:" -ForegroundColor White
Write-Host ""
Write-Host "   # Confirmar reserva" -ForegroundColor Gray
Write-Host "   POST /api/integracion/reservas/confirmar" -ForegroundColor Cyan
Write-Host "   → Verifica que se inserta el PAGO en la BD" -ForegroundColor Gray
Write-Host ""
Write-Host "   # Cancelar reserva" -ForegroundColor Gray
Write-Host "   DELETE /api/integracion/reservas/cancelar?idReserva=265" -ForegroundColor Cyan
Write-Host "   → 200 OK { success: true, montoPagado: 170.77, mensaje: '' }" -ForegroundColor Gray
Write-Host ""
Write-Host "📚 Documentación completa:" -ForegroundColor White
Write-Host "   - SOLUCION_PAGO_INTEGRACION.md" -ForegroundColor Cyan
Write-Host "   - SQL/sp_insertarPagoIntegracion.sql" -ForegroundColor Cyan
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " ✅ ACTUALIZACIÓN COMPLETA" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
