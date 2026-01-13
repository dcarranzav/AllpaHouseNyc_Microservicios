using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;
using Shared.Data;

namespace ApiGateway.Controllers.Integracion;

[ApiController]
[Route("api/integracion/reservas")]
public class IntegracionConfirmarReservaController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly ILogger<IntegracionConfirmarReservaController> _logger;

    public IntegracionConfirmarReservaController(
        IHttpClientFactory factory,
        ILogger<IntegracionConfirmarReservaController> logger)
    {
        _http = factory.CreateClient("RecaApi");
        _logger = logger;
    }

    /// <summary>
    /// Confirma una reserva definitiva a partir de un hold (pre–reserva).
    /// Inserta automáticamente el pago en la tabla PAGO.
    /// </summary>
    [HttpPost("confirmar")]
    public async Task<IActionResult> ConfirmarReserva(
        [FromBody] ConfirmarReservaRequest req)
    {
        if (req == null)
            return BadRequest("El cuerpo no puede estar vacío.");

        try
        {
            _logger.LogInformation("Confirmando reserva HOLD: {IdHold}, Habitación: {IdHabitacion}", 
                req.idHold, req.idHabitacion);

            // 1) Confirmar la reserva en RECA API
            var response = await _http.PostAsJsonAsync(
                "/api/v1/hoteles/book",
                req
            );

            var content = await response.Content.ReadAsStringAsync();

            // Si RECA devuelve error, lo propagamos
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RECA API error: {StatusCode}, {Content}", 
                    response.StatusCode, content);
                return StatusCode((int)response.StatusCode, content);
            }

            _logger.LogInformation("RECA API response: {Content}", content);

            // 2) Deserializar la respuesta para obtener ID_RESERVA
            ConfirmarReservaResponse? result = null;
            try
            {
                result = JsonSerializer.Deserialize<ConfirmarReservaResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializando respuesta de RECA");
            }

            if (result == null || result.IdReserva <= 0)
            {
                _logger.LogWarning("No se pudo obtener ID_RESERVA de la respuesta: {Content}", content);
                // Retornar la respuesta de RECA tal cual si no se puede deserializar
                return StatusCode((int)response.StatusCode, content);
            }

            // 3) Insertar el pago en la tabla PAGO
            try
            {
                await InsertarPagoIntegracion(result.IdReserva, req);
                _logger.LogInformation("Pago insertado correctamente para reserva {IdReserva}", result.IdReserva);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al insertar pago para reserva {IdReserva}", result.IdReserva);
                // No falla la confirmación, solo logea el error
                // La reserva ya está confirmada en RECA
            }

            // 🔔 Opcional: publicar evento RabbitMQ
            // ReservaConfirmadaEvent

            // RECA devuelve 201 Created
            return StatusCode((int)response.StatusCode, content);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con RECA API");
            return StatusCode(503, new
            {
                error = "Error de conexión con el servicio de reservas",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al confirmar reserva");
            return StatusCode(500, new
            {
                error = "Error interno al confirmar la reserva",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Inserta un registro de pago en la tabla PAGO para reservas de integración
    /// </summary>
    private async Task InsertarPagoIntegracion(int idReserva, ConfirmarReservaRequest req)
    {
        var connectionString = DatabaseConfig.ConnectionString;

        await using var cn = new SqlConnection(connectionString);
        await using var cmd = new SqlCommand("dbo.sp_insertarPagoIntegracion", cn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.Add("@ID_RESERVA", SqlDbType.Int).Value = idReserva;
        
        // Para integraciones externas, siempre es ID_USUARIO_EXTERNO
        // Puedes extraer el ID del correo o documento si es necesario
        cmd.Parameters.Add("@ID_USUARIO_EXTERNO", SqlDbType.Int).Value = DBNull.Value;
        cmd.Parameters.Add("@ID_USUARIO", SqlDbType.Int).Value = DBNull.Value;
        
        // Método de pago por defecto: 2 (Tarjeta de crédito/débito)
        cmd.Parameters.Add("@ID_METODO_PAGO", SqlDbType.Int).Value = 2;
        
        // Cuentas: se generarán en el SP
        cmd.Parameters.Add("@CUENTA_ORIGEN", SqlDbType.BigInt).Value = 0;
        cmd.Parameters.Add("@CUENTA_DESTINO", SqlDbType.BigInt).Value = 0707001310;

        await cn.OpenAsync();
        await using var rd = await cmd.ExecuteReaderAsync();

        if (await rd.ReadAsync())
        {
            bool ok = rd["OK"] != DBNull.Value && Convert.ToBoolean(rd["OK"]);
            string? mensaje = rd["MENSAJE"] as string;

            if (!ok)
            {
                throw new Exception($"Error en sp_insertarPagoIntegracion: {mensaje}");
            }

            _logger.LogInformation("Pago insertado. ID_PAGO: {IdPago}, Monto: {Monto}", 
                rd["ID_PAGO"], rd["MONTO_TOTAL"]);
        }
    }
}

public class ConfirmarReservaRequest
{
    public string idHabitacion { get; set; }
    public string idHold { get; set; }
    public string nombre { get; set; }
    public string apellido { get; set; }
    public string correo { get; set; }
    public string tipoDocumento { get; set; }
    public string documento { get; set; }
    public DateTime fechaInicio { get; set; }
    public DateTime fechaFin { get; set; }
    public int numeroHuespedes { get; set; }
}

/// <summary>
/// Respuesta de RECA API al confirmar reserva
/// </summary>
public class ConfirmarReservaResponse
{
    public int IdReserva { get; set; }
    public decimal? CostoTotalReserva { get; set; }
    public DateTime? FechaRegistro { get; set; }
    public string? EstadoGeneral { get; set; }
    // ... otros campos que retorna RECA
}
