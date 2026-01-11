using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using ReservasService.Protos;
using Shared.Data;
using Shared.DTOs;
using Shared.EventBus;
using System.Globalization;

namespace ReservasService.Services;

[Authorize]
public class ReservasGrpcService : ReservasGrpc.ReservasGrpcBase
{
    private readonly ReservaRepository _reservaRepo;
    private readonly HabxResRepository _habxResRepo;
    private readonly DesxHabxResRepository _desxHabxResRepo;
    private readonly HoldRepository _holdRepo;
    private readonly IEventBus _eventBus;

    public ReservasGrpcService(
        ReservaRepository reservaRepo,
        HabxResRepository habxResRepo,
        DesxHabxResRepository desxHabxResRepo,
        HoldRepository holdRepo,
        IEventBus eventBus)
    {
        _reservaRepo = reservaRepo;
        _habxResRepo = habxResRepo;
        _desxHabxResRepo = desxHabxResRepo;
        _holdRepo = holdRepo;
        _eventBus = eventBus;
    }

    // ========== RESERVAS ==========
    [AllowAnonymous] // Permitir acceso anónimo para el calendario
    public override async Task<ReservasResponse> ObtenerReservas(
    Empty request, ServerCallContext context)
    {
        var reservas = await _reservaRepo.ObtenerTodasAsync();
        var response = new ReservasResponse();
        response.Reservas.AddRange(reservas.Select(MapReserva));
        return response;
    }

    public override async Task<ReservaResponse> ObtenerReservaPorId(
        ReservaIdRequest request, ServerCallContext context)
    {
        var reserva = await _reservaRepo.ObtenerPorIdAsync(request.IdReserva);
        if (reserva == null)
            return new ReservaResponse { Success = false, Message = "No encontrada" };

        return new ReservaResponse
        {
            Success = true,
            Message = "OK",
            Reserva = MapReserva(reserva)
        };
    }

    public override async Task<ReservaResponse> CrearReserva(
        ReservaCreateRequest request, ServerCallContext context)
    {
        var dto = new ReservaDto
        {
            IdUnicoUsuario = request.IdUsuario == 0 ? null : request.IdUsuario,
            IdUnicoUsuarioExterno = request.IdUsuarioExterno == 0 ? null : request.IdUsuarioExterno,
            FechaInicioReserva = ParseDate(request.FechaInicio),
            FechaFinalReserva = ParseDate(request.FechaFinal),
            EstadoGeneralReserva = request.EstadoGeneral,
            EstadoReserva = request.Estado
        };

        var result = await _reservaRepo.CrearAsync(dto);

        return new ReservaResponse
        {
            Success = true,
            Message = "Creada",
            Reserva = MapReserva(result)
        };
    }

    public override async Task<ReservaResponse> ActualizarReserva(
        ReservaUpdateRequest request, ServerCallContext context)
    {
        var dto = new ReservaDto
        {
            EstadoGeneralReserva = request.EstadoGeneral,
            EstadoReserva = request.Estado
        };

        var result = await _reservaRepo.ActualizarAsync(request.IdReserva, dto);
        if (result == null)
            return new ReservaResponse { Success = false, Message = "No encontrada" };

        return new ReservaResponse
        {
            Success = true,
            Message = "Actualizada",
            Reserva = MapReserva(result)
        };
    }

    public override async Task<BoolResponse> EliminarReserva(
        ReservaIdRequest request,
        ServerCallContext context)
    {
        var ok = await _reservaRepo.CancelarAsync(request.IdReserva);

        return new BoolResponse
        {
            Success = ok,
            Message = ok ? "Reserva cancelada" : "Reserva no encontrada"
        };
    }



    // ========== HABXRES ==========

    [AllowAnonymous] // Permitir acceso anónimo para el calendario
    public override async Task<HabxResListResponse> ObtenerHabxRes(
        Empty request, ServerCallContext context)
    {
        var items = await _habxResRepo.ObtenerTodosAsync();

        var response = new HabxResListResponse();
        response.Items.AddRange(items.Select(MapHabxRes));

        return response;
    }


    public override async Task<HabxResListResponse> ObtenerHabxResPorReserva(
        ReservaIdRequest request, ServerCallContext context)
    {
        var items = await _habxResRepo.ObtenerPorReservaAsync(request.IdReserva);

        var response = new HabxResListResponse();
        response.Items.AddRange(items.Select(MapHabxRes));

        return response;
    }


    public override async Task<HabxResResponse> CrearHabxRes(
        HabxResCreateRequest request, ServerCallContext context)
    {
        var dto = new HabxResDto
        {
            IdHabitacion = request.IdHabitacion,
            IdReserva = request.IdReserva,
            CapacidadReservaHabxRes = request.Capacidad,
            CostoCalculadoHabxRes = (decimal)request.CostoCalculado,
            DescuentoHabxRes = (decimal)request.Descuento,
            ImpuestosHabxRes = (decimal)request.Impuestos,
            EstadoHabxRes = request.Estado
        };

        var result = await _habxResRepo.CrearAsync(dto);

        return new HabxResResponse
        {
            Success = true,
            Message = "Creado",
            Item = MapHabxRes(result)
        };
    }

    public override async Task<BoolResponse> EliminarHabxRes(
        HabxResIdRequest request, ServerCallContext context)
    {
        var ok = await _habxResRepo.EliminarAsync(request.IdHabxres);
        return new BoolResponse { Success = ok };
    }

    // ========== DESXHABXRES ==========



    public override async Task<DesxHabxResListResponse>
    ObtenerDesxHabxResPorHabxRes(
        HabxResIdRequest request,
        ServerCallContext context)
    {
        var items = await _desxHabxResRepo.ObtenerPorHabxResAsync(request.IdHabxres);

        var response = new DesxHabxResListResponse();
        response.Items.AddRange(items.Select(MapDesxHabxRes));

        return response;
    }



    public override async Task<DesxHabxResResponse> CrearDesxHabxRes(
        DesxHabxResCreateRequest request, ServerCallContext context)
    {
        var dto = new DesxHabxResDto
        {
            IdHabxRes = request.IdHabxres,
            IdDescuento = request.IdDescuento,
            MontoDesxHabxRes = (decimal)request.Monto,
            EstadoDesxHabxRes = request.Estado
        };

        var result = await _desxHabxResRepo.CrearAsync(dto);

        return new DesxHabxResResponse
        {
            Success = true,
            Message = "Creado",
            Item = MapDesxHabxRes(result)
        };
    }

    public override async Task<BoolResponse> EliminarDesxHabxRes(
        DesxHabxResIdRequest request, ServerCallContext context)
    {
        var ok = await _desxHabxResRepo.EliminarAsync(
            request.IdDescuento,
            request.IdHabxres);

        return new BoolResponse
        {
            Success = ok,
            Message = ok ? "Eliminado" : "No encontrado"
        };
    }


    // ========== HOLD ==========

    public override async Task<HoldsResponse> ObtenerHolds(Empty request, ServerCallContext context)
    {
        var holds = await _holdRepo.ObtenerTodosAsync();
        var response = new HoldsResponse();
        response.Holds.AddRange(holds.Select(MapHoldToMessage));
        return response;
    }

    public override async Task<HoldResponse> ObtenerHoldPorId(
        HoldIdRequest request, ServerCallContext context)
    {
        var hold = await _holdRepo.ObtenerPorIdAsync(request.IdHold);

        if (hold == null)
            return new HoldResponse
            {
                Success = false,
                Message = "Hold no encontrado"
            };

        return new HoldResponse
        {
            Success = true,
            Message = "OK",
            Hold = MapHoldToMessage(hold)
        };
    }


    public override async Task<HoldsResponse> ObtenerHoldsPorHabitacion(
        HabitacionIdRequest request, ServerCallContext context)
    {
        var holds = await _holdRepo.ObtenerPorHabitacionAsync(request.IdHabitacion);

        var response = new HoldsResponse();
        response.Holds.AddRange(holds.Select(MapHoldToMessage));

        return response;
    }

    public override async Task<HoldResponse> CrearHold(
        HoldCreateRequest request, ServerCallContext context)
    {
        var dto = new HoldDto
        {
            IdHold = request.IdHold,
            IdHabitacion = request.IdHabitacion,
            IdReserva = request.IdReserva,
            TiempoHold = request.TiempoHold,
            FechaInicioHold = ParseDate(request.FechaInicio),
            FechaFinalHold = ParseDate(request.FechaFinal),
            EstadoHold = request.Estado
        };

        var result = await _holdRepo.CrearAsync(dto);

        return new HoldResponse
        {
            Success = true,
            Message = "Hold creado",
            Hold = MapHoldToMessage(result)
        };
    }


    public override async Task<HoldResponse> ActualizarHold(
        HoldUpdateRequest request, ServerCallContext context)
    {
        var dto = new HoldDto
        {
            EstadoHold = request.Estado
        };

        var result = await _holdRepo.ActualizarAsync(request.IdHold, dto);

        if (result == null)
            return new HoldResponse
            {
                Success = false,
                Message = "Hold no encontrado"
            };

        return new HoldResponse
        {
            Success = true,
            Message = "Hold actualizado",
            Hold = MapHoldToMessage(result)
        };
    }


    public override async Task<BoolResponse> EliminarHold(
        HoldIdRequest request, ServerCallContext context)
    {
        var deleted = await _holdRepo.EliminarAsync(request.IdHold);

        return new BoolResponse
        {
            Success = deleted,
            Message = deleted ? "Eliminado" : "No encontrado"
        };
    }


    // ================== HELPERS ==================

    private static ReservaMessage MapReserva(ReservaDto dto) => new()
    {
        IdReserva = dto.IdReserva,
        IdUsuario = dto.IdUnicoUsuario ?? 0,
        IdUsuarioExterno = dto.IdUnicoUsuarioExterno ?? 0,
        CostoTotal = (double)(dto.CostoTotalReserva ?? 0),
        FechaRegistro = dto.FechaRegistroReserva?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
        FechaInicio = dto.FechaInicioReserva?.ToString("yyyy-MM-dd") ?? "",
        FechaFinal = dto.FechaFinalReserva?.ToString("yyyy-MM-dd") ?? "",
        EstadoGeneral = dto.EstadoGeneralReserva ?? "",
        Estado = dto.EstadoReserva ?? false,
        FechaModificacion = dto.FechaModificacionReserva?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
    };

    private static HabxResMessage MapHabxRes(HabxResDto dto) => new()
    {
        IdHabxres = dto.IdHabxRes,
        IdHabitacion = dto.IdHabitacion,
        IdReserva = dto.IdReserva,
        Capacidad = dto.CapacidadReservaHabxRes ?? 0,
        CostoCalculado = (double)(dto.CostoCalculadoHabxRes ?? 0),
        Descuento = (double)(dto.DescuentoHabxRes ?? 0),
        Impuestos = (double)(dto.ImpuestosHabxRes ?? 0),
        Estado = dto.EstadoHabxRes ?? false,
        FechaModificacion = dto.FechaModificacionHabxRes?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
    };

    private static DesxHabxResMessage MapDesxHabxRes(DesxHabxResDto dto) => new()
    {
        IdHabxres = dto.IdHabxRes,
        IdDescuento = dto.IdDescuento,
        Monto = (double)(dto.MontoDesxHabxRes ?? 0),
        Estado = dto.EstadoDesxHabxRes ?? false,
        FechaModificacion =
            dto.FechaModificacionDesxHabxRes?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
    };


    private static HoldMessage MapHold(HoldDto dto) => new()
    {
        IdHold = dto.IdHold,
        IdHabitacion = dto.IdHabitacion,
        IdReserva = dto.IdReserva,
        TiempoHold = dto.TiempoHold ?? 0,
        FechaInicio = dto.FechaInicioHold?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
        FechaFinal = dto.FechaFinalHold?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
        Estado = dto.EstadoHold ?? false
    };

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    private static HoldMessage MapHoldToMessage(HoldDto dto) => new()
    {
        IdHold = dto.IdHold,
        IdHabitacion = dto.IdHabitacion,
        IdReserva = dto.IdReserva,
        TiempoHold = dto.TiempoHold ?? 0,
        FechaInicio = dto.FechaInicioHold?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
        FechaFinal = dto.FechaFinalHold?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
        Estado = dto.EstadoHold ?? false
    };


}
