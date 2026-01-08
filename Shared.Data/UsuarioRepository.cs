using Microsoft.Data.SqlClient;
using Shared.DTOs;
using System.Data;

namespace Shared.Data;

public class UsuarioRepository
{
    private readonly string _connectionString;

    public UsuarioRepository(string? connectionString = null)
    {
        _connectionString = connectionString ?? DatabaseConfig.ConnectionString;
    }

    public async Task<List<UsuarioInternoDto>> ObtenerTodosAsync()
    {
        List<UsuarioInternoDto> lista = new();

        await using var cn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(@"
            SELECT ID_UNICO_USUARIO, ID_ROL, NOMBRE_USUARIO, APELLIDO_USUARIO,
                   EMAIL_USUARIO, CLAVE_USUARIO,
                   FECHA_NACIMIENTO_USUARIO, TIPO_DOCUMENTO_USUARIO,
                   DOCUMENTO_USUARIO, FECHA_MODIFICACION_USUARIO, ESTADO_USUARIO
            FROM USUARIO", cn);

        await cn.OpenAsync();
        await using var dr = await cmd.ExecuteReaderAsync();

        while (await dr.ReadAsync())
            lista.Add(Map(dr));

        return lista;
    }

    public async Task<UsuarioInternoDto?> ObtenerPorIdAsync(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(@"
            SELECT ID_UNICO_USUARIO, ID_ROL, NOMBRE_USUARIO, APELLIDO_USUARIO,
                   EMAIL_USUARIO, CLAVE_USUARIO,
                   FECHA_NACIMIENTO_USUARIO, TIPO_DOCUMENTO_USUARIO,
                   DOCUMENTO_USUARIO, FECHA_MODIFICACION_USUARIO, ESTADO_USUARIO
            FROM USUARIO
            WHERE ID_UNICO_USUARIO = @ID", cn);

        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;

        await cn.OpenAsync();
        await using var dr = await cmd.ExecuteReaderAsync();

        return await dr.ReadAsync() ? Map(dr) : null;
    }

    public async Task<UsuarioInternoDto?> ObtenerPorCorreoAsync(string correo)
    {
        await using var cn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(@"
            SELECT ID_UNICO_USUARIO, ID_ROL, NOMBRE_USUARIO, APELLIDO_USUARIO,
                   EMAIL_USUARIO, CLAVE_USUARIO,
                   FECHA_NACIMIENTO_USUARIO, TIPO_DOCUMENTO_USUARIO,
                   DOCUMENTO_USUARIO, FECHA_MODIFICACION_USUARIO, ESTADO_USUARIO
            FROM USUARIO
            WHERE EMAIL_USUARIO = @CORREO", cn);

        cmd.Parameters.Add("@CORREO", SqlDbType.VarChar, 200).Value = correo;

        await cn.OpenAsync();
        await using var dr = await cmd.ExecuteReaderAsync();

        return await dr.ReadAsync() ? Map(dr) : null;
    }

    public async Task<UsuarioInternoDto> CrearAsync(UsuarioInternoDto dto)
    {
        var now = DateTime.Now;

        await using var cn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(@"
            INSERT INTO USUARIO
            (
                ID_UNICO_USUARIO, ID_ROL, NOMBRE_USUARIO, APELLIDO_USUARIO,
                EMAIL_USUARIO, CLAVE_USUARIO, FECHA_NACIMIENTO_USUARIO,
                TIPO_DOCUMENTO_USUARIO, DOCUMENTO_USUARIO,
                FECHA_MODIFICACION_USUARIO, ESTADO_USUARIO
            )
            VALUES
            (
                @ID, @ROL, @NOMBRE, @APELLIDO,
                @CORREO, @CLAVE, @FECHA_NAC,
                @TIPO_DOC, @DOC,
                @FECHA_MOD, @ESTADO
            )", cn);

        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = dto.Id;
        cmd.Parameters.Add("@ROL", SqlDbType.Int).Value = dto.IdRol;
        cmd.Parameters.Add("@NOMBRE", SqlDbType.VarChar, 200).Value = (object?)dto.Nombre ?? DBNull.Value;
        cmd.Parameters.Add("@APELLIDO", SqlDbType.VarChar, 200).Value = (object?)dto.Apellido ?? DBNull.Value;
        cmd.Parameters.Add("@CORREO", SqlDbType.VarChar, 200).Value = (object?)dto.Correo ?? DBNull.Value;
        cmd.Parameters.Add("@CLAVE", SqlDbType.VarChar, 500).Value = (object?)dto.Clave ?? DBNull.Value;
        cmd.Parameters.Add("@FECHA_NAC", SqlDbType.Date).Value = (object?)dto.FechaNacimiento ?? DBNull.Value;
        cmd.Parameters.Add("@TIPO_DOC", SqlDbType.VarChar, 150).Value = (object?)dto.TipoDocumento ?? DBNull.Value;
        cmd.Parameters.Add("@DOC", SqlDbType.VarChar, 20).Value = (object?)dto.Documento ?? DBNull.Value;
        cmd.Parameters.Add("@FECHA_MOD", SqlDbType.DateTime).Value = now;
        cmd.Parameters.Add("@ESTADO", SqlDbType.Bit).Value = dto.Estado;

        await cn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        dto.FechaModificacion = now;
        return dto;
    }

    public async Task<UsuarioInternoDto?> ActualizarAsync(int id, UsuarioInternoDto dto)
    {
        var now = DateTime.Now;

        await using var cn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(@"
            UPDATE USUARIO SET
                ID_ROL = @ROL,
                NOMBRE_USUARIO = @NOMBRE,
                APELLIDO_USUARIO = @APELLIDO,
                EMAIL_USUARIO = @CORREO,
                CLAVE_USUARIO = @CLAVE,
                FECHA_NACIMIENTO_USUARIO = @FECHA_NAC,
                TIPO_DOCUMENTO_USUARIO = @TIPO_DOC,
                DOCUMENTO_USUARIO = @DOC,
                FECHA_MODIFICACION_USUARIO = @FECHA_MOD,
                ESTADO_USUARIO = @ESTADO
            WHERE ID_UNICO_USUARIO = @ID", cn);

        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@ROL", SqlDbType.Int).Value = dto.IdRol;
        cmd.Parameters.Add("@NOMBRE", SqlDbType.VarChar, 200).Value = (object?)dto.Nombre ?? DBNull.Value;
        cmd.Parameters.Add("@APELLIDO", SqlDbType.VarChar, 200).Value = (object?)dto.Apellido ?? DBNull.Value;
        cmd.Parameters.Add("@CORREO", SqlDbType.VarChar, 200).Value = (object?)dto.Correo ?? DBNull.Value;
        cmd.Parameters.Add("@CLAVE", SqlDbType.VarChar, 500).Value = (object?)dto.Clave ?? DBNull.Value;
        cmd.Parameters.Add("@FECHA_NAC", SqlDbType.Date).Value = (object?)dto.FechaNacimiento ?? DBNull.Value;
        cmd.Parameters.Add("@TIPO_DOC", SqlDbType.VarChar, 150).Value = (object?)dto.TipoDocumento ?? DBNull.Value;
        cmd.Parameters.Add("@DOC", SqlDbType.VarChar, 20).Value = (object?)dto.Documento ?? DBNull.Value;
        cmd.Parameters.Add("@FECHA_MOD", SqlDbType.DateTime).Value = now;
        cmd.Parameters.Add("@ESTADO", SqlDbType.Bit).Value = dto.Estado;

        await cn.OpenAsync();
        var rows = await cmd.ExecuteNonQueryAsync();

        if (rows == 0) return null;

        dto.Id = id;
        dto.FechaModificacion = now;
        return dto;
    }

    // ✅ Eliminación lógica (recomendada)
    public async Task<bool> DesactivarAsync(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(@"
            UPDATE USUARIO
            SET ESTADO_USUARIO = 0,
                FECHA_MODIFICACION_USUARIO = @FECHA
            WHERE ID_UNICO_USUARIO = @ID", cn);

        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@FECHA", SqlDbType.DateTime).Value = DateTime.Now;

        await cn.OpenAsync();
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private static UsuarioInternoDto Map(SqlDataReader dr) => new()
    {
        Id = dr.GetInt32(dr.GetOrdinal("ID_UNICO_USUARIO")),
        IdRol = dr.GetInt32(dr.GetOrdinal("ID_ROL")),
        Nombre = dr.IsDBNull(dr.GetOrdinal("NOMBRE_USUARIO")) ? null : dr.GetString(dr.GetOrdinal("NOMBRE_USUARIO")),
        Apellido = dr.IsDBNull(dr.GetOrdinal("APELLIDO_USUARIO")) ? null : dr.GetString(dr.GetOrdinal("APELLIDO_USUARIO")),
        Correo = dr.IsDBNull(dr.GetOrdinal("EMAIL_USUARIO")) ? null : dr.GetString(dr.GetOrdinal("EMAIL_USUARIO")),
        Clave = dr.IsDBNull(dr.GetOrdinal("CLAVE_USUARIO")) ? null : dr.GetString(dr.GetOrdinal("CLAVE_USUARIO")),
        FechaNacimiento = dr.IsDBNull(dr.GetOrdinal("FECHA_NACIMIENTO_USUARIO")) ? null : dr.GetDateTime(dr.GetOrdinal("FECHA_NACIMIENTO_USUARIO")),
        TipoDocumento = dr.IsDBNull(dr.GetOrdinal("TIPO_DOCUMENTO_USUARIO")) ? null : dr.GetString(dr.GetOrdinal("TIPO_DOCUMENTO_USUARIO")),
        Documento = dr.IsDBNull(dr.GetOrdinal("DOCUMENTO_USUARIO")) ? null : dr.GetString(dr.GetOrdinal("DOCUMENTO_USUARIO")),
        FechaModificacion = dr.IsDBNull(dr.GetOrdinal("FECHA_MODIFICACION_USUARIO")) ? null : dr.GetDateTime(dr.GetOrdinal("FECHA_MODIFICACION_USUARIO")),
        Estado = dr.GetBoolean(dr.GetOrdinal("ESTADO_USUARIO"))
    };
}
