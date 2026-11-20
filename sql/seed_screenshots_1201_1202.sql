-- Одноразовий безпечний seed двох НОВИХ тестових рейсів для скриншотів.
-- Запускайте в SSMS один раз перед Docker Compose.
-- Не видаляє і не змінює наявні дані. Якщо ідентифікатори вже зайняті — зупиняється.
USE [DispatcherDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM dbo.Aircraft WHERE id IN (1201,1202) OR registrationNumber IN (N'UR-DEMO1201',N'UR-DEMO1202'))
        THROW 52101, N'Test aircraft IDs/registrations already used; choose new IDs for next run.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Routes WHERE id IN (1201,1202))
        THROW 52102, N'Test route IDs already used; choose new IDs for next run.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Waypoints WHERE id IN (12011,12012,12013,12021,12022,12023))
        THROW 52103, N'Test waypoint IDs already used; choose new IDs for next run.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Flights WHERE id IN (251201,251202) OR callsign IN (N'TEST1201',N'TEST1202'))
        THROW 52104, N'Test flight IDs/callsigns already used; choose new IDs for next run.', 1;

    SET IDENTITY_INSERT dbo.Aircraft ON;
    INSERT INTO dbo.Aircraft (id, registrationNumber, model, aircraftType) VALUES
      (1201, N'UR-DEMO1201', N'Навчальний літак', N'Симулятор'),
      (1202, N'UR-DEMO1202', N'Навчальний літак', N'Симулятор');
    SET IDENTITY_INSERT dbo.Aircraft OFF;

    SET IDENTITY_INSERT dbo.Routes ON;
    INSERT INTO dbo.Routes (id, name, description) VALUES
      (1201, N'Тест 1201 — Київ', N'Короткий маршрут для скриншотів одночасних рейсів'),
      (1202, N'Тест 1202 — Житомир', N'Короткий маршрут для скриншотів одночасних рейсів');
    SET IDENTITY_INSERT dbo.Routes OFF;

    SET IDENTITY_INSERT dbo.Waypoints ON;
    INSERT INTO dbo.Waypoints (id, name, latitude, longitude) VALUES
      (12011, N'T1201-1', 50.500000, 30.553000),
      (12012, N'T1201-2', 50.555000, 30.597000),
      (12013, N'T1201-3', 50.600000, 30.639000),
      (12021, N'T1202-1', 50.305000, 28.700000),
      (12022, N'T1202-2', 50.350000, 28.748000),
      (12023, N'T1202-3', 50.400000, 28.803000);
    SET IDENTITY_INSERT dbo.Waypoints OFF;

    INSERT INTO dbo.RouteWaypoints (routeId, waypointId, sequenceNumber) VALUES
      (1201,12011,1), (1201,12012,2), (1201,12013,3),
      (1202,12021,1), (1202,12022,2), (1202,12023,3);

    SET IDENTITY_INSERT dbo.Flights ON;
    INSERT INTO dbo.Flights (id, aircraftId, routeId, callsign, status, scheduledDeparture, scheduledArrival) VALUES
      (251201,1201,1201,N'TEST1201',N'Scheduled',CAST(SYSUTCDATETIME() AS datetime2(0)),DATEADD(MINUTE,20,CAST(SYSUTCDATETIME() AS datetime2(0)))),
      (251202,1202,1202,N'TEST1202',N'Scheduled',CAST(SYSUTCDATETIME() AS datetime2(0)),DATEADD(MINUTE,20,CAST(SYSUTCDATETIME() AS datetime2(0))));
    SET IDENTITY_INSERT dbo.Flights OFF;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
SELECT id, callsign, aircraftId, routeId, status FROM dbo.Flights WHERE id IN (251201,251202);
SELECT routeId, waypointId, sequenceNumber FROM dbo.RouteWaypoints WHERE routeId IN (1201,1202) ORDER BY routeId,sequenceNumber;
GO
