# 🔄 SOLUCIÓN COMPLETA: Inserción de Pagos en Reservas de Integración

---

## 🎯 **PROBLEMA IDENTIFICADO**

Las reservas confirmadas a través de las **APIs de integración** (externas) **NO insertaban registro en la tabla PAGO**, por lo que al intentar cancelarlas con `sp_cancelarReservaHotel`, el stored procedure no encontraba el pago y retornaba `montoPagado = 0`.

### **Flujo anterior (con error):**

```
Cliente externo
   ↓ POST /api/integracion/reservas/confirmar
   ↓
ApiGateway → RECA API → sp_reservarHabitacion
   ↓ Inserta RESERVA, HABXRES, HOLD ✅
   ↓ NO inserta PAGO ❌
   ↓
200 OK { idReserva, costoTotal, ... }

Luego al cancelar:
   ↓ DELETE /api/integracion/reservas/cancelar?idReserva=X
   ↓
RECA API → sp_cancelarReservaHotel
   ↓ Busca PAGO WHERE ID_RESERVA = X
   ↓ NO encuentra ningún registro ❌
   ↓ Retorna montoPagado = 0 ❌
```

---

## ✅ **SOLUCIÓN IMPLEMENTADA**

He creado un **stored procedure nuevo** (`sp_insertarPagoIntegracion`) que se ejecuta automáticamente después de confirmar la reserva, y modifiqu los controladores para que llamen **directamente** a los stored procedures en lugar de usar RECA API.

### **Archivos modificados/creados:**

1. ✅ `SQL/sp_insertarPagoIntegracion.sql` (nuevo)
2. ✅ `ApiGateway/Controllers/integracion/IntegracionConfirmarReservaController.cs` (modificado)
3. ✅ `ApiGateway/Controllers/integracion/IntegracionCancelarReservaController.cs` (modificado)

---

## 📝 **1. STORED PROCEDURE: sp_insertarPagoIntegracion**

### **Ubicación:**
```
SQL/sp_insertarPagoIntegracion.sql
```

### **Funcionalidad:**

```sql
CREATE OR ALTER PROCEDURE [dbo].[sp_insertarPagoIntegracion]
    @ID_RESERVA              INT,
    @ID_USUARIO_EXTERNO      INT = NULL,
    @ID_USUARIO              INT = NULL,
    @ID_METODO_PAGO          INT = 2,  -- Default: Tarjeta
    @CUENTA_ORIGEN           BIGINT = 0,
    @CUENTA_DESTINO          BIGINT = 0707001310
AS
```

### **Validaciones:**

1. ✅ Valida que la reserva exista
2. ✅ Valida que esté en estado `CONFIRMADO`
3. ✅ Valida que no exista ya un pago activo
4. ✅ Genera cuenta origen simulada si es 0
5. ✅ Inserta el pago con el monto total de la reserva

### **Retorna:**

```json
{
  "OK": true,
  "MENSAJE": "Pago insertado correctamente.",
  "ID_PAGO": 144,
  "MONTO_TOTAL": 170.77,
  "CUENTA_ORIGEN": 707001320,
  "CUENTA_DESTINO": 707001310
}
```

---

## 📝 **2. CONTROLADOR: IntegracionConfirmarReservaController**

### **Cambios:**

**ANTES:**
```csharp
// Solo llamaba a RECA API y retornaba la respuesta
var response = await _http.PostAsJsonAsync("/api/v1/hoteles/book", req);
return StatusCode((int)response.StatusCode, content);
```

**AHORA:**
```csharp
// 1) Confirma la reserva en RECA API
var response = await _http.PostAsJsonAsync("/api/v1/hoteles/book", req);

// 2) Deserializa la respuesta para obtener ID_RESERVA
var result = JsonSerializer.Deserialize<ConfirmarReservaResponse>(content);

// 3) Inserta el pago automáticamente
await InsertarPagoIntegracion(result.IdReserva, req);

// 4) Retorna la respuesta de RECA
return StatusCode((int)response.StatusCode, content);
```

### **Método nuevo:**

```csharp
private async Task InsertarPagoIntegracion(int idReserva, ConfirmarReservaRequest req)
{
    var connectionString = DatabaseConfig.ConnectionString;

    await using var cn = new SqlConnection(connectionString);
    await using var cmd = new SqlCommand("dbo.sp_insertarPagoIntegracion", cn)
    {
        CommandType = CommandType.StoredProcedure
    };

    cmd.Parameters.Add("@ID_RESERVA", SqlDbType.Int).Value = idReserva;
    cmd.Parameters.Add("@ID_USUARIO_EXTERNO", SqlDbType.Int).Value = DBNull.Value;
    cmd.Parameters.Add("@ID_USUARIO", SqlDbType.Int).Value = DBNull.Value;
    cmd.Parameters.Add("@ID_METODO_PAGO", SqlDbType.Int).Value = 2;
    cmd.Parameters.Add("@CUENTA_ORIGEN", SqlDbType.BigInt).Value = 0;
    cmd.Parameters.Add("@CUENTA_DESTINO", SqlDbType.BigInt).Value = 0707001310;

    await cn.OpenAsync();
    await using var rd = await cmd.ExecuteReaderAsync();
    
    if (await rd.ReadAsync())
    {
        bool ok = rd["OK"] != DBNull.Value && Convert.ToBoolean(rd["OK"]);
        if (!ok)
        {
            throw new Exception($"Error en sp_insertarPagoIntegracion");
        }
    }
}
```

---

## 📝 **3. CONTROLADOR: IntegracionCancelarReservaController**

### **Cambios:**

**ANTES:**
```csharp
// Llamaba a RECA API externa
var response = await _http.DeleteAsync($"/api/v1/hoteles/cancel?idReserva={idReserva}");
var result = JsonSerializer.Deserialize<CancelarReservaResponse>(content);
return Ok(result);
```

**AHORA:**
```csharp
// Llama directamente al stored procedure local
var result = await CancelarReservaInterno(idReserva.Value);
return Ok(result);
```

### **Método nuevo:**

```csharp
private async Task<CancelarReservaResponse> CancelarReservaInterno(int idReserva)
{
    var connectionString = DatabaseConfig.ConnectionString;

    await using var cn = new SqlConnection(connectionString);
    await using var cmd = new SqlCommand("dbo.sp_cancelarReservaHotel", cn)
    {
        CommandType = CommandType.StoredProcedure
    };

    cmd.Parameters.Add("@ID_RESERVA", SqlDbType.Int).Value = idReserva;

    await cn.OpenAsync();
    await using var rd = await cmd.ExecuteReaderAsync();

    if (!await rd.ReadAsync())
    {
        return new CancelarReservaResponse
        {
            Success = false,
            MontoPagado = 0,
            Mensaje = "No se pudo procesar la cancelacion."
        };
    }

    bool ok = rd["OK"] != DBNull.Value && Convert.ToBoolean(rd["OK"]);
    decimal? montoPagado = rd["MONTO_PAGADO"] == DBNull.Value 
        ? null 
        : Convert.ToDecimal(rd["MONTO_PAGADO"]);
    string? mensaje = rd["MENSAJE"] as string;

    return new CancelarReservaResponse
    {
        Success = ok,
        MontoPagado = montoPagado ?? 0,
        Mensaje = mensaje ?? string.Empty
    };
}
```

---

## 🔄 **FLUJO COMPLETO AHORA**

### **1. CONFIRMAR RESERVA (Integración)**

```
Cliente externo
   ↓ POST /api/integracion/reservas/confirmar
   ↓ Body: { idHabitacion, idHold, nombre, apellido, correo, ... }
   ↓
ApiGateway (IntegracionConfirmarReservaController)
   ↓
   ↓ [1] Llamar a RECA API
   ↓     POST http://aureacuenrest.runasp.net/api/v1/hoteles/book
   ↓     → sp_reservarHabitacion
   ↓     → Inserta RESERVA, HABXRES, HOLD ✅
   ↓     ← 201 Created { idReserva: 265, costoTotal: 170.77, ... }
   ↓
   ↓ [2] Deserializar respuesta
   ↓     idReserva = 265
   ↓
   ↓ [3] Insertar pago
   ↓     sp_insertarPagoIntegracion(@ID_RESERVA = 265)
   ↓     → Inserta en tabla PAGO ✅
   ↓       ID_PAGO: 144
   ↓       ID_RESERVA: 265
   ↓       MONTO_TOTAL_PAGO: 170.77
   ↓       ID_METODO_PAGO: 2
   ↓       CUENTA_ORIGEN_PAGO: 707001320
   ↓       CUENTA_DESTINO_PAGO: 707001310
   ↓       ESTADO_PAGO: 1
   ↓
   ↓ [4] Retornar respuesta de RECA
   ↓     201 Created { idReserva: 265, ... }
   ↓
Cliente recibe confirmación ✅
```

---

### **2. CANCELAR RESERVA (Integración)**

```
Cliente externo
   ↓ DELETE /api/integracion/reservas/cancelar?idReserva=265
   ↓
ApiGateway (IntegracionCancelarReservaController)
   ↓
   ↓ [1] Llamar directamente al SP
   ↓     sp_cancelarReservaHotel(@ID_RESERVA = 265)
   ↓     
   ↓     → [2.1] Validar reserva existe ✅
   ↓     → [2.2] Validar reserva activa ✅
   ↓     → [2.3] Validar estado CONFIRMADO ✅
   ↓     → [2.4] Validar fecha de inicio no pasada ✅
   ↓     
   ↓     → [3.1] Buscar PAGO activo
   ↓           SELECT TOP 1 ID_PAGO, MONTO_TOTAL_PAGO
   ↓           FROM PAGO
   ↓           WHERE ID_RESERVA = 265 AND ESTADO_PAGO = 1
   ↓           ✅ ENCUENTRA ID_PAGO = 144, MONTO = 170.77
   ↓     
   ↓     → [3.2] Actualizar HABXRES (ESTADO = 0)
   ↓     → [3.3] Actualizar HOLD (ESTADO = 0)
   ↓     → [3.4] Actualizar RESERVA (ESTADO_GENERAL = 'CANCELADO')
   ↓     → [3.5] Actualizar PAGO (ESTADO = 0)
   ↓     → [3.6] Actualizar FACTURA (ESTADO = 0) si existe
   ↓     → [3.7] Actualizar PDF (ESTADO = 0) si existe
   ↓     
   ↓     ← Retorna:
   ↓       OK: true
   ↓       MONTO_PAGADO: 170.77 ✅
   ↓       MENSAJE: null
   ↓       ID_PAGO: 144
   ↓       ID_FACTURA: 343
   ↓
   ↓ [2] Retornar respuesta
   ↓     200 OK {
   ↓       "success": true,
   ↓       "montoPagado": 170.77,  ← ✅ AHORA RETORNA EL MONTO!
   ↓       "mensaje": ""
   ↓     }
   ↓
Cliente recibe el monto a reembolsar ✅
```

---

## 📊 **COMPARACIÓN: ANTES vs AHORA**

| Aspecto | ANTES ❌ | AHORA ✅ |
|---------|---------|---------|
| **Confirmar reserva** | Solo insertaba RESERVA, HABXRES, HOLD | Inserta también PAGO automáticamente |
| **Pago en tabla PAGO** | No se insertaba | Se inserta con sp_insertarPagoIntegracion |
| **Cancelar reserva** | Llamaba a RECA API externa | Llama directamente a sp_cancelarReservaHotel |
| **montoPagado al cancelar** | Siempre 0 ❌ | Retorna el monto real del PAGO ✅ |
| **Reembolso** | No se sabía cuánto reembolsar | Se sabe exactamente cuánto reembolsar |

---

## 🧪 **PRUEBAS**

### **Caso 1: Confirmar reserva de integración**

```bash
POST http://apigateway.com/api/integracion/reservas/confirmar
Content-Type: application/json

{
  "idHabitacion": "HAB001",
  "idHold": "HOJO000123",
  "nombre": "Juan",
  "apellido": "Pérez",
  "correo": "juan@example.com",
  "tipoDocumento": "DNI",
  "documento": "12345678",
  "fechaInicio": "2026-02-01T00:00:00",
  "fechaFin": "2026-02-05T00:00:00",
  "numeroHuespedes": 2
}
```

**Respuesta esperada:**
```json
{
  "idReserva": 265,
  "costoTotalReserva": 170.77,
  "fechaRegistro": "2026-01-13T15:30:00",
  "estadoGeneral": "CONFIRMADO",
  ...
}
```

**Verificar en BD:**
```sql
-- Debe existir el registro en PAGO
SELECT *
FROM PAGO
WHERE ID_RESERVA = 265;

-- Resultado esperado:
-- ID_PAGO: 144
-- ID_RESERVA: 265
-- MONTO_TOTAL_PAGO: 170.77
-- ESTADO_PAGO: 1
```

---

### **Caso 2: Cancelar reserva de integración**

```bash
DELETE http://apigateway.com/api/integracion/reservas/cancelar?idReserva=265
```

**Respuesta esperada:**
```json
{
  "success": true,
  "montoPagado": 170.77,  ← ✅ RETORNA EL MONTO REAL!
  "mensaje": ""
}
```

**Verificar en BD:**
```sql
-- La reserva debe estar cancelada
SELECT ESTADO_GENERAL_RESERVA, ESTADO_RESERVA
FROM RESERVA
WHERE ID_RESERVA = 265;
-- ESTADO_GENERAL_RESERVA: 'CANCELADO'
-- ESTADO_RESERVA: 0

-- El pago debe estar desactivado
SELECT ESTADO_PAGO
FROM PAGO
WHERE ID_RESERVA = 265;
-- ESTADO_PAGO: 0
```

---

### **Caso 3: Intentar cancelar sin pago (no debería pasar)**

Si por algún motivo no se insertó el pago, el SP retorna:

```json
{
  "success": true,
  "montoPagado": 0,
  "mensaje": ""
}
```

Pero con la solución implementada, esto **no debería ocurrir** porque el pago se inserta automáticamente al confirmar.

---

## 🚀 **DESPLEGAR**

### **1. Ejecutar el stored procedure en SQL Server:**

```sql
-- Copiar y ejecutar SQL/sp_insertarPagoIntegracion.sql
-- en SQL Server Management Studio o Azure Data Studio
```

### **2. Desplegar el código:**

```powershell
cd "D:\Jossue\Desktop\RETO 3\FRONT\V1\PROYECTO_HOTELES_DJANGO\frontend-angular\Microservicios"
.\update-render.ps1
```

**Tiempo:** 5-7 minutos

---

## 📋 **CHECKLIST**

### **Stored Procedure:**
- [x] Creado `sp_insertarPagoIntegracion.sql`
- [x] Valida que la reserva exista
- [x] Valida que esté en estado CONFIRMADO
- [x] Valida que no exista pago duplicado
- [x] Genera cuenta origen simulada
- [x] Inserta registro en tabla PAGO
- [x] Retorna OK, ID_PAGO, MONTO_TOTAL
- [ ] Ejecutado en SQL Server

### **Controlador de Confirmación:**
- [x] Llama a RECA API para confirmar
- [x] Deserializa respuesta para obtener ID_RESERVA
- [x] Llama a `sp_insertarPagoIntegracion`
- [x] Logs agregados
- [x] Manejo de errores
- [x] Compilación exitosa ✅

### **Controlador de Cancelación:**
- [x] Ya NO llama a RECA API
- [x] Llama directamente a `sp_cancelarReservaHotel`
- [x] Deserializa resultado del SP
- [x] Retorna `montoPagado` correctamente
- [x] Logs agregados
- [x] Manejo de errores
- [x] Compilación exitosa ✅

### **Despliegue:**
- [ ] SP ejecutado en SQL Server
- [ ] Código subido a GitHub
- [ ] Redespliegue en Render
- [ ] Prueba de confirmación
- [ ] Prueba de cancelación
- [ ] Verificación de `montoPagado`

---

## 🔍 **VERIFICACIÓN POST-DESPLIEGUE**

### **1. Verificar que el SP existe:**

```sql
SELECT OBJECT_ID('dbo.sp_insertarPagoIntegracion');
-- Debe retornar un número (el ID del objeto)
-- Si es NULL, el SP no existe
```

### **2. Probar confirmación:**

```bash
curl -X POST "https://apigateway-hyaw.onrender.com/api/integracion/reservas/confirmar" \
  -H "Content-Type: application/json" \
  -d '{
    "idHabitacion": "HAB001",
    "idHold": "HOJO000123",
    "nombre": "Test",
    "apellido": "Usuario",
    "correo": "test@example.com",
    "tipoDocumento": "DNI",
    "documento": "12345678",
    "fechaInicio": "2026-02-01T00:00:00",
    "fechaFin": "2026-02-05T00:00:00",
    "numeroHuespedes": 2
  }'
```

### **3. Verificar pago en BD:**

```sql
-- Reemplazar 265 con el ID_RESERVA que retornó la API
SELECT *
FROM PAGO
WHERE ID_RESERVA = 265;
```

**Debe retornar:**
```
ID_PAGO | ID_RESERVA | MONTO_TOTAL_PAGO | ESTADO_PAGO
--------|------------|------------------|-------------
144     | 265        | 170.77           | 1
```

### **4. Probar cancelación:**

```bash
curl -X DELETE "https://apigateway-hyaw.onrender.com/api/integracion/reservas/cancelar?idReserva=265"
```

**Debe retornar:**
```json
{
  "success": true,
  "montoPagado": 170.77,
  "mensaje": ""
}
```

### **5. Verificar logs en Render:**

Busca en ApiGateway > Logs:

```
[Information] Confirmando reserva HOLD: HOJO000123, Habitación: HAB001
[Information] RECA API response: {"idReserva":265,...}
[Information] Pago insertado. ID_PAGO: 144, Monto: 170.77
[Information] Pago insertado correctamente para reserva 265

[Information] Cancelando reserva 265 directamente en BD
[Information] Reserva 265 procesada. Success: True, Monto: 170.77, Mensaje:
```

---

## 💡 **ARQUITECTURA FINAL**

```
┌─────────────────────────────────────────────────────┐
│         Cliente (Integración Externa)               │
└──────────────────┬──────────────────────────────────┘
                   │
                   ↓ [1] POST /api/integracion/reservas/confirmar
                   ↓
┌─────────────────────────────────────────────────────┐
│          ApiGateway (.NET 8 - Render)               │
│  ┌───────────────────────────────────────────────┐  │
│  │ IntegracionConfirmarReservaController         │  │
│  │ [A] Llama a RECA API                          │  │
│  │ [B] Obtiene ID_RESERVA                        │  │
│  │ [C] Llama sp_insertarPagoIntegracion ✅       │  │
│  └───────────────────────────────────────────────┘  │
└──────────────────┬──────────────────────────────────┘
                   │
                   ├─────[A]─────→ RECA API
                   │               sp_reservarHabitacion
                   │               → RESERVA, HABXRES, HOLD
                   │
                   └─────[C]─────→ SQL Server
                                   sp_insertarPagoIntegracion
                                   → PAGO ✅

---

┌─────────────────────────────────────────────────────┐
│         Cliente (Integración Externa)               │
└──────────────────┬──────────────────────────────────┘
                   │
                   ↓ [2] DELETE /api/integracion/reservas/cancelar?idReserva=X
                   ↓
┌─────────────────────────────────────────────────────┐
│          ApiGateway (.NET 8 - Render)               │
│  ┌───────────────────────────────────────────────┐  │
│  │ IntegracionCancelarReservaController          │  │
│  │ [A] Llama sp_cancelarReservaHotel             │  │
│  │ [B] Encuentra PAGO ✅                         │  │
│  │ [C] Retorna montoPagado ✅                    │  │
│  └───────────────────────────────────────────────┘  │
└──────────────────┬──────────────────────────────────┘
                   │
                   └─────[A]─────→ SQL Server
                                   sp_cancelarReservaHotel
                                   → Busca PAGO ✅
                                   → Retorna MONTO_PAGADO ✅
```

---

<div align="center">

# ✅ **SOLUCIÓN COMPLETA** ✅

**Problema:** Reservas de integración no insertaban PAGO  
**Solución:** Inserción automática con `sp_insertarPagoIntegracion`  

**Problema:** Cancelación retornaba `montoPagado = 0`  
**Solución:** Ahora retorna el monto real del PAGO ✅  

**Archivos creados:**  
- ✅ `SQL/sp_insertarPagoIntegracion.sql`  

**Archivos modificados:**  
- ✅ `IntegracionConfirmarReservaController.cs`  
- ✅ `IntegracionCancelarReservaController.cs`  

**Compilación:** ✅ Exitosa  
**Listo para:** Ejecutar SP en BD y desplegar  

</div>
