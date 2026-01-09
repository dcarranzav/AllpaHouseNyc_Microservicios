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
public class FacturasController : ControllerBase
{
    private readonly FacturaRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ReservasGrpc.ReservasGrpcClient _reservasClient;

    public FacturasController(
        FacturaRepository repository, 
        IEventBus eventBus,
        ReservasGrpc.ReservasGrpcClient reservasClient)
    {
        _repository = repository;
        _eventBus = eventBus;
        _reservasClient = reservasClient;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<FacturaDto>>> GetAll()
    {
        var facturas = await _repository.ObtenerTodasAsync();
        return Ok(facturas);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<FacturaDto>> GetById(int id)
    {
        var factura = await _repository.ObtenerPorIdAsync(id);
        if (factura == null) return NotFound();
        return Ok(factura);
    }

    [HttpPost]
    public async Task<ActionResult<FacturaDto>> Create([FromBody] FacturaDto dto)
    {
        // Comunicación gRPC: Verificar que la reserva existe
        try
        {
            var reservaResponse = await _reservasClient.ObtenerReservaPorIdAsync(
                new ReservaIdRequest
                {
                    IdReserva = dto.IdReserva
                });

            if (!reservaResponse.Success)
            {
                return BadRequest(new { message = "La reserva especificada no existe" });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: No se pudo verificar la reserva via gRPC: {ex.Message}");
        }

        var result = await _repository.CrearAsync(dto);
        
        // Calcular monto total
        var montoTotal = (result.SubtotalFactura ?? 0) - (result.DescuentoTotalFactura ?? 0) + (result.ImpuestoTotalFactura ?? 0);
        
        await _eventBus.PublishAsync(new FacturaEmitidaEvent
        {
            IdFactura = result.IdFactura,
            IdReserva = result.IdReserva,
            MontoTotal = montoTotal
        });

        return CreatedAtAction(nameof(GetById), new { id = result.IdFactura }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FacturaDto>> Update(int id, [FromBody] FacturaDto dto)
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
