using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Data;
using Shared.DTOs;
using Shared.EventBus;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;


namespace UsuariosPagosService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;

    public UsuariosController(UsuarioRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UsuarioInternoDto>>> GetAll()
    {
        var usuarios = await _repository.ObtenerTodosAsync();
        return Ok(usuarios);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UsuarioInternoDto>> GetById(int id)
    {
        var usuario = await _repository.ObtenerPorIdAsync(id);
        if (usuario == null) return NotFound();
        return Ok(usuario);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<UsuarioInternoDto>> Create([FromBody] UsuarioInternoDto dto)
    {
        // Hash the password before saving
        if (!string.IsNullOrEmpty(dto.Clave))
        {
            dto.Clave = HashPassword(dto.Clave);
        }

        var result = await _repository.CrearAsync(dto);

        await _eventBus.PublishAsync(new UsuarioCreatedEvent
        {
            IdUsuario = result.Id,
            NombreUsuario = result.Nombre ?? "",
            Email = result.Correo ?? ""
        });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UsuarioInternoDto>> Update(int id, [FromBody] UsuarioInternoDto dto)
    {
        var result = await _repository.ActualizarAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _repository.DesactivarAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLower();
    }



    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UsuarioInternoDto>> Login([FromBody] LoginRequest request)
    {
        var usuario = await _repository.ObtenerPorCorreoAsync(request.Correo);
        if (usuario == null)
            return Unauthorized(new { message = "Credenciales inválidas" });

        var passwordHash = HashPassword(request.Password);

        if (usuario.Clave != passwordHash)
            return Unauthorized(new { message = "Credenciales inválidas" });

        return Ok(usuario);
    }

}

public record LoginRequest(string Correo, string Password);
