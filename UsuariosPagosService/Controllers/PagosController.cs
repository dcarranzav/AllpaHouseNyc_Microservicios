using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Data;
using Shared.DTOs;
using Shared.EventBus;
using ReservasService.Protos;

namespace UsuariosPagosService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    private readonly PagoRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ReservasGrpc.ReservasGrpcClient _reservasClient;

    public PagosController(
        PagoRepository repository, 
        IEventBus eventBus,
        ReservasGrpc.ReservasGrpcClient reservasClient)
    {
        _repository = repository;
        _eventBus = eventBus;
        _reservasClient = reservasClient;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<PagoDto>>> GetAll()
    {
        var pagos = await _repository.ObtenerTodosAsync();
        return Ok(pagos);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<PagoDto>> GetById(int id)
    {
        var pago = await _repository.ObtenerPorIdAsync(id);
        if (pago == null) return NotFound();
        return Ok(pago);
    }

    [HttpPost]
    public async Task<ActionResult<PagoDto>> Create([FromBody] PagoDto dto)
    {
        // Comunicación gRPC: Verificar que la reserva existe
        if (dto.IdReserva.HasValue)
        {
            try
            {

                var reservaResponse =
                    await _reservasClient.ObtenerReservaPorIdAsync(
                        new ReservaIdRequest
                        {
                            IdReserva = dto.IdReserva.Value
                        });

                if (!reservaResponse.Success)
                {
                    return BadRequest(new { message = "La reserva especificada no existe" });
                }
            }
            catch (Exception ex)
            {
                // Si el servicio gRPC no está disponible, continuar (en producción manejar diferente)
                Console.WriteLine($"Warning: No se pudo verificar la reserva via gRPC: {ex.Message}");
            }
        }

        var result = await _repository.CrearAsync(dto);
        
        await _eventBus.PublishAsync(new PagoRealizadoEvent
        {
            IdPago = result.IdPago,
            IdReserva = result.IdReserva ?? 0,
            Monto = result.MontoTotalPago ?? 0,
            IdMetodoPago = result.IdMetodoPago
        });

        return CreatedAtAction(nameof(GetById), new { id = result.IdPago }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PagoDto>> Update(int id, [FromBody] PagoDto dto)
    {
        var result = await _repository.ActualizarAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _repository.EliminarAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
