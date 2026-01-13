-- =============================================
-- sp_insertarPagoIntegracion
-- Inserta un registro de pago para reservas de integración
-- =============================================
USE [db31651]
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_insertarPagoIntegracion]
    @ID_RESERVA              INT,
    @ID_USUARIO_EXTERNO      INT = NULL,
    @ID_USUARIO              INT = NULL,
    @ID_METODO_PAGO          INT = 2,  -- Default: Tarjeta de crédito/débito
    @CUENTA_ORIGEN           BIGINT = 0,
    @CUENTA_DESTINO          BIGINT = 0707001310  -- Cuenta destino del hotel
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE 
            @ID_PAGO            INT,
            @MONTO_TOTAL        DECIMAL(10,2),
            @ID_FACTURA         INT = NULL,
            @ESTADO_RESERVA     VARCHAR(50);

        -- Validar que la reserva exista y esté confirmada
        SELECT 
            @MONTO_TOTAL = COSTO_TOTAL_RESERVA,
            @ESTADO_RESERVA = ESTADO_GENERAL_RESERVA
        FROM RESERVA WITH (UPDLOCK, HOLDLOCK)
        WHERE ID_RESERVA = @ID_RESERVA;

        IF @MONTO_TOTAL IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                CAST(0 AS BIT) AS OK,
                'La reserva no existe.' AS MENSAJE,
                NULL AS ID_PAGO;
            RETURN;
        END

        IF @ESTADO_RESERVA <> 'CONFIRMADO'
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                CAST(0 AS BIT) AS OK,
                'Solo se puede insertar pago en reservas confirmadas.' AS MENSAJE,
                NULL AS ID_PAGO;
            RETURN;
        END

        -- Validar que no exista ya un pago activo para esta reserva
        IF EXISTS (
            SELECT 1 
            FROM PAGO 
            WHERE ID_RESERVA = @ID_RESERVA 
              AND ISNULL(ESTADO_PAGO, 1) = 1
        )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                CAST(0 AS BIT) AS OK,
                'Ya existe un pago activo para esta reserva.' AS MENSAJE,
                NULL AS ID_PAGO;
            RETURN;
        END

        -- Generar ID_PAGO
        SELECT @ID_PAGO = ISNULL(MAX(ID_PAGO), 0) + 1 FROM PAGO;

        -- Buscar ID_FACTURA si existe
        SELECT TOP 1 @ID_FACTURA = ID_FACTURA
        FROM FACTURA
        WHERE ID_RESERVA = @ID_RESERVA
          AND ISNULL(ESTADO_FACTURA, 1) = 1
        ORDER BY ID_FACTURA DESC;

        -- Si CUENTA_ORIGEN es 0, generar una simulada
        IF @CUENTA_ORIGEN = 0
        BEGIN
            -- Generar cuenta origen simulada basada en ID_USUARIO_EXTERNO o ID_USUARIO
            IF @ID_USUARIO_EXTERNO IS NOT NULL
                SET @CUENTA_ORIGEN = 0707000000 + (@ID_USUARIO_EXTERNO % 100000);
            ELSE IF @ID_USUARIO IS NOT NULL
                SET @CUENTA_ORIGEN = 0707000000 + (@ID_USUARIO % 100000);
            ELSE
                SET @CUENTA_ORIGEN = 0707001320; -- Cuenta por defecto
        END

        -- Insertar el pago
        INSERT INTO PAGO
        (
            ID_PAGO,
            ID_METODO_PAGO,
            ID_UNICO_USUARIO_EXTERNO,
            ID_UNICO_USUARIO,
            ID_FACTURA,
            ID_RESERVA,
            CUENTA_ORIGEN_PAGO,
            CUENTA_DESTINO_PAGO,
            MONTO_TOTAL_PAGO,
            FECHA_EMISION_PAGO,
            ESTADO_PAGO,
            FECHA_MODIFICACION_PAGO
        )
        VALUES
        (
            @ID_PAGO,
            @ID_METODO_PAGO,
            @ID_USUARIO_EXTERNO,
            @ID_USUARIO,
            @ID_FACTURA,
            @ID_RESERVA,
            @CUENTA_ORIGEN,
            @CUENTA_DESTINO,
            @MONTO_TOTAL,
            GETDATE(),
            1,  -- Estado activo
            GETDATE()
        );

        COMMIT TRANSACTION;

        -- Retornar éxito
        SELECT 
            CAST(1 AS BIT) AS OK,
            'Pago insertado correctamente.' AS MENSAJE,
            @ID_PAGO AS ID_PAGO,
            @MONTO_TOTAL AS MONTO_TOTAL,
            @CUENTA_ORIGEN AS CUENTA_ORIGEN,
            @CUENTA_DESTINO AS CUENTA_DESTINO;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

        DECLARE @ERR NVARCHAR(4000) = ERROR_MESSAGE();

        SELECT 
            CAST(0 AS BIT) AS OK,
            @ERR AS MENSAJE,
            NULL AS ID_PAGO;
    END CATCH
END
GO
