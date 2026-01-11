namespace Shared.Data;

public static class DatabaseConfig
{
    // Cadena de conexión a la base de datos SQL Server en Somee.com
    // NOTA: En producción (Railway), esto será sobrescrito por variables de entorno
    public const string ConnectionString = "Server=db31701.public.databaseasp.net,1433;Database=db31701;User Id=db31701;Password=T=q7p2?H#tB4;Encrypt=True;TrustServerCertificate=True;";
}
