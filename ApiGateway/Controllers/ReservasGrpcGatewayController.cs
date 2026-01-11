using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc;
using ReservasService.Protos;

namespace ApiGateway.Controllers;

/// <summary>
/// Controlador REST que expone los métodos gRPC del ReservasService.
/// Proporciona acceso HTTP a reservas, habitaciones por reserva, descuentos y holds.
/// </summary>
[ApiController]
[Route("api/reservas-grpc")]
public class ReservasGrpcGatewayController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<ReservasGrpcGatewayController> _logger;

    public ReservasGrpcGatewayController(
        IConfiguration config,
        ILogger<ReservasGrpcGatewayController> logger)
    {
        _config = config;
        _logger = logger;
    }

    private ReservasGrpc.ReservasGrpcClient GetGrpcClient()
    {
        // Primero intentar leer de la configuración
        var grpcUrl = _config["GrpcServices:ReservasService"];
        
        // Si no está configurado o contiene placeholder ${}, leer de variable de entorno directamente
        if (string.IsNullOrEmpty(grpcUrl) || grpcUrl.Contains("${"))
        {
            grpcUrl = Environment.GetEnvironmentVariable("RESERVAS_SERVICE_URL") 
                      ?? "https://reservas-service.onrender.com";
        }
        
        _logger.LogInformation("Connecting to gRPC service at: {Url}", grpcUrl);
        
        // Usar GrpcWebHandler para compatibilidad con HTTP/1.1 (Render, Cloudflare, etc.)
        var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler());
        var channel = GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
        {
            HttpHandler = httpHandler
        });
        
        return new ReservasGrpc.ReservasGrpcClient(channel);
    }

    // ==================== RESERVAS ====================

    /// <summary>
    /// Obtiene todas las reservas
    /// </summary>
    [HttpGet("reservas")]
    public async Task<IActionResult> ObtenerReservas()
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.ObtenerReservasAsync(new Empty());
            
            var result = response.Reservas.Select(r => new
            {
                IdReserva = r.IdReserva,
                IdUsuario = r.IdUsuario,
                IdUsuarioExterno = r.IdUsuarioExterno,
                CostoTotal = r.CostoTotal,
                FechaRegistro = r.FechaRegistro,
                FechaInicio = r.FechaInicio,
                FechaFinal = r.FechaFinal,
                EstadoGeneral = r.EstadoGeneral,
                Estado = r.Estado,
                FechaModificacion = r.FechaModificacion
            }).ToList();
            
            _logger.LogInformation("Returned {Count} reservations", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reservations from gRPC");
            return Problem($"Error obteniendo reservas: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene una reserva por su ID
    /// </summary>
    [HttpGet("reservas/{idReserva}")]
    public async Task<IActionResult> ObtenerReservaPorId(int idReserva)
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.ObtenerReservaPorIdAsync(
                new ReservaIdRequest { IdReserva = idReserva });
            
            if (!response.Success)
                return NotFound(response.Message);
            
            return Ok(new
            {
                Success = response.Success,
                Message = response.Message,
                Reserva = new
                {
                    IdReserva = response.Reserva.IdReserva,
                    IdUsuario = response.Reserva.IdUsuario,
                    IdUsuarioExterno = response.Reserva.IdUsuarioExterno,
                    CostoTotal = response.Reserva.CostoTotal,
                    FechaRegistro = response.Reserva.FechaRegistro,
                    FechaInicio = response.Reserva.FechaInicio,
                    FechaFinal = response.Reserva.FechaFinal,
                    EstadoGeneral = response.Reserva.EstadoGeneral,
                    Estado = response.Reserva.Estado
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reservation {Id}", idReserva);
            return Problem($"Error obteniendo reserva: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancela (elimina lógicamente) una reserva
    /// </summary>
    [HttpDelete("reservas/{idReserva}")]
    public async Task<IActionResult> EliminarReserva(int idReserva)
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.EliminarReservaAsync(
                new ReservaIdRequest { IdReserva = idReserva });
            
            return Ok(new { Success = response.Success, Message = response.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reservation {Id}", idReserva);
            return Problem($"Error eliminando reserva: {ex.Message}");
        }
    }

    // ==================== HABXRES ====================

    /// <summary>
    /// Obtiene todas las relaciones habitación-reserva
    /// </summary>
    [HttpGet("habxres")]
    public async Task<IActionResult> ObtenerHabxRes()
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.ObtenerHabxResAsync(new Empty());
            
            var result = response.Items.Select(h => new
            {
                IdHabxRes = h.IdHabxres,
                IdHabitacion = h.IdHabitacion,
                IdReserva = h.IdReserva,
                Capacidad = h.Capacidad,
                CostoCalculado = h.CostoCalculado,
                Descuento = h.Descuento,
                Impuestos = h.Impuestos,
                Estado = h.Estado,
                FechaModificacion = h.FechaModificacion
            }).ToList();
            
            _logger.LogInformation("Returned {Count} habxres records", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting habxres from gRPC");
            return Problem($"Error obteniendo HabxRes: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene las relaciones habitación-reserva para una reserva específica
    /// </summary>
    [HttpGet("habxres/reserva/{idReserva}")]
    public async Task<IActionResult> ObtenerHabxResPorReserva(int idReserva)
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.ObtenerHabxResPorReservaAsync(
                new ReservaIdRequest { IdReserva = idReserva });
            
            var result = response.Items.Select(h => new
            {
                IdHabxRes = h.IdHabxres,
                IdHabitacion = h.IdHabitacion,
                IdReserva = h.IdReserva,
                Capacidad = h.Capacidad,
                CostoCalculado = h.CostoCalculado,
                Descuento = h.Descuento,
                Impuestos = h.Impuestos,
                Estado = h.Estado
            }).ToList();
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting habxres for reservation {Id}", idReserva);
            return Problem($"Error obteniendo HabxRes: {ex.Message}");
        }
    }

    // ==================== HOLDS ====================

    /// <summary>
    /// Obtiene todos los holds activos
    /// </summary>
    [HttpGet("holds")]
    public async Task<IActionResult> ObtenerHolds()
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.ObtenerHoldsAsync(new Empty());
            
            var result = response.Holds.Select(h => new
            {
                IdHold = h.IdHold,
                IdHabitacion = h.IdHabitacion,
                IdReserva = h.IdReserva,
                TiempoHold = h.TiempoHold,
                FechaInicio = h.FechaInicio,
                FechaFinal = h.FechaFinal,
                Estado = h.Estado
            }).ToList();
            
            _logger.LogInformation("Returned {Count} holds", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting holds from gRPC");
            return Problem($"Error obteniendo Holds: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene un hold por su ID
    /// </summary>
    [HttpGet("holds/{idHold}")]
    public async Task<IActionResult> ObtenerHoldPorId(string idHold)
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.ObtenerHoldPorIdAsync(
                new HoldIdRequest { IdHold = idHold });
            
            if (!response.Success)
                return NotFound(response.Message);
            
            return Ok(new
            {
                Success = response.Success,
                Message = response.Message,
                Hold = new
                {
                    IdHold = response.Hold.IdHold,
                    IdHabitacion = response.Hold.IdHabitacion,
                    IdReserva = response.Hold.IdReserva,
                    TiempoHold = response.Hold.TiempoHold,
                    FechaInicio = response.Hold.FechaInicio,
                    FechaFinal = response.Hold.FechaFinal,
                    Estado = response.Hold.Estado
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hold {Id}", idHold);
            return Problem($"Error obteniendo Hold: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene los holds de una habitación específica
    /// </summary>
    [HttpGet("holds/habitacion/{idHabitacion}")]
    public async Task<IActionResult> ObtenerHoldsPorHabitacion(string idHabitacion)
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.ObtenerHoldsPorHabitacionAsync(
                new HabitacionIdRequest { IdHabitacion = idHabitacion });
            
            var result = response.Holds.Select(h => new
            {
                IdHold = h.IdHold,
                IdHabitacion = h.IdHabitacion,
                IdReserva = h.IdReserva,
                TiempoHold = h.TiempoHold,
                FechaInicio = h.FechaInicio,
                FechaFinal = h.FechaFinal,
                Estado = h.Estado
            }).ToList();
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting holds for room {Id}", idHabitacion);
            return Problem($"Error obteniendo Holds por habitación: {ex.Message}");
        }
    }

    /// <summary>
    /// Elimina un hold
    /// </summary>
    [HttpDelete("holds/{idHold}")]
    public async Task<IActionResult> EliminarHold(string idHold)
    {
        try
        {
            var client = GetGrpcClient();
            var response = await client.EliminarHoldAsync(
                new HoldIdRequest { IdHold = idHold });
            
            return Ok(new { Success = response.Success, Message = response.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hold {Id}", idHold);
            return Problem($"Error eliminando Hold: {ex.Message}");
        }
    }

    // ==================== FECHAS OCUPADAS (Para Calendario) ====================

    /// <summary>
    /// Obtiene las fechas ocupadas de una habitación específica.
    /// Útil para bloquear fechas en el calendario del frontend.
    /// </summary>
    [HttpGet("fechas-ocupadas/{idHabitacion}")]
    public async Task<IActionResult> ObtenerFechasOcupadas(string idHabitacion)
    {
        try
        {
            var client = GetGrpcClient();
            
            // Obtener todas las reservas y habxres
            var reservasTask = client.ObtenerReservasAsync(new Empty());
            var habxresTask = client.ObtenerHabxResAsync(new Empty());
            
            await Task.WhenAll(reservasTask.ResponseAsync, habxresTask.ResponseAsync);
            
            var reservas = reservasTask.ResponseAsync.Result.Reservas.ToList();
            var habxres = habxresTask.ResponseAsync.Result.Items.ToList();
            
            _logger.LogInformation("Processing {ReservasCount} reservas and {HabxResCount} habxres for room {Id}", 
                reservas.Count, habxres.Count, idHabitacion);
            
            // Crear índice de HabxRes por IdReserva
            var habxresDict = habxres.ToDictionary(h => h.IdReserva, h => h);
            
            var fechasOcupadas = new HashSet<string>();
            
            foreach (var reserva in reservas)
            {
                // Verificar que tengamos HabxRes para esta reserva
                if (!habxresDict.TryGetValue(reserva.IdReserva, out var habxresItem))
                    continue;
                
                // Verificar que sea la habitación correcta
                if (habxresItem.IdHabitacion != idHabitacion)
                    continue;
                
                // Excluir reservas canceladas o expiradas
                var estado = (reserva.EstadoGeneral ?? "").Trim().ToUpper();
                if (estado.Contains("CANCELADA") || estado.Contains("EXPIRADO"))
                    continue;
                
                // Generar todas las fechas del rango
                if (!string.IsNullOrEmpty(reserva.FechaInicio) && !string.IsNullOrEmpty(reserva.FechaFinal))
                {
                    if (DateTime.TryParse(reserva.FechaInicio, out var inicio) &&
                        DateTime.TryParse(reserva.FechaFinal, out var fin))
                    {
                        for (var d = inicio.Date; d <= fin.Date; d = d.AddDays(1))
                        {
                            fechasOcupadas.Add(d.ToString("yyyy-MM-dd"));
                        }
                    }
                }
            }
            
            var result = fechasOcupadas.OrderBy(f => f).ToList();
            
            _logger.LogInformation("Room {Id} has {Count} occupied dates", idHabitacion, result.Count);
            
            return Ok(new
            {
                Success = true,
                IdHabitacion = idHabitacion,
                FechasOcupadas = result,
                TotalFechas = result.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting occupied dates for room {Id}", idHabitacion);
            return Problem($"Error obteniendo fechas ocupadas: {ex.Message}");
        }
    }
}
