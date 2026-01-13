using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Data;
using Shared.Data;

namespace ApiGateway.Controllers.Integracion;

[ApiController]
[Route("api/integracion/reservas")]
public class IntegracionCancelarReservaController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly ILogger<IntegracionCancelarReservaController> _logger;

    public IntegracionCancelarReservaController(
        IHttpClientFactory factory,
        ILogger<IntegracionCancelarReservaController> logger)
    {
        _http = factory.CreateClient("RecaApi");
        _logger = logger;
    }

    /// <summary>
    /// Cancela una reserva y retorna el monto pagado a reembolsar.
    /// Siempre retorna 200 OK con un objeto JSON que indica el resultado.
    /// </summary>
    /// <param name="idReserva">ID de la reserva a cancelar</param>
    /// <returns>
    /// {
    ///   "success": true/false,
    ///   "montoPagado": 150.50,
    ///   "mensaje": "Reserva cancelada exitosamente" | "Error message"
    /// }
    /// </returns>
    [HttpDelete("cancelar")]
    public async Task<IActionResult> CancelarReserva([FromQuery] int? idReserva)
    {
        // Validación: idReserva es obligatorio
        if (!idReserva.HasValue)
        {
            _logger.LogWarning("Intento de cancelación sin idReserva");
            return Ok(new CancelarReservaResponse
            {
                Success = false,
                MontoPagado = 0,
                Mensaje = "Debe enviar idReserva."
            });
        }

        try
        {
            _logger.LogInformation("Cancelando reserva {IdReserva} directamente en BD", idReserva.Value);
            
            // Llamar directamente al stored procedure
            var result = await CancelarReservaInterno(idReserva.Value);

            _logger.LogInformation("Reserva {IdReserva} procesada. Success: {Success}, Monto: {Monto}, Mensaje: {Mensaje}", 
                idReserva.Value, result.Success, result.MontoPagado, result.Mensaje);

            // 🔔 Opcional: publicar evento RabbitMQ
            // if (result.Success)
            // {
            //     await _eventBus.PublishAsync(new ReservaCanceladaEvent
            //     {
            //         IdReserva = idReserva.Value,
            //         MontoPagado = result.MontoPagado,
            //         FechaCancelacion = DateTime.UtcNow
            //     });
            // }

            // Siempre retornar 200 OK con el resultado (como la API anterior)
            return Ok(result);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error de BD al cancelar reserva {IdReserva}", idReserva.Value);
            
            // Retornar 200 OK con error (como lo hacía tu API anterior)
            return Ok(new CancelarReservaResponse
            {
                Success = false,
                MontoPagado = 0,
                Mensaje = $"Error de base de datos: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al cancelar reserva {IdReserva}", idReserva.Value);
            
            // Retornar 500 solo en errores graves (como tu API anterior con InternalServerError)
            return StatusCode(500, new
            {
                message = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Llama al stored procedure sp_cancelarReservaHotel
    /// </summary>
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

        // Leer el resultado del SP
        bool ok = rd["OK"] != DBNull.Value && Convert.ToBoolean(rd["OK"]);
        decimal? montoPagado = rd["MONTO_PAGADO"] == DBNull.Value ? null : Convert.ToDecimal(rd["MONTO_PAGADO"]);
        string? mensaje = rd["MENSAJE"] as string;

        return new CancelarReservaResponse
        {
            Success = ok,
            MontoPagado = montoPagado ?? 0,
            Mensaje = mensaje ?? string.Empty
        };
    }
}

/// <summary>
/// Respuesta de la API RECA para cancelación de reserva.
/// Coincide con la estructura de tu API anterior en .NET Framework.
/// </summary>
public class CancelarReservaResponse
{
    /// <summary>
    /// Indica si la cancelación fue exitosa
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Monto que fue pagado y será reembolsado (viene del stored procedure)
    /// </summary>
    public decimal MontoPagado { get; set; }
    
    /// <summary>
    /// Mensaje descriptivo del resultado (del stored procedure)
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;
}
