using DispatcherServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DispatcherServer.Persistence;

public sealed class DispatcherDbContext(DbContextOptions<DispatcherDbContext> options) : DbContext(options)
{
    public DbSet<Aircraft> Aircraft => Set<Aircraft>();
    public DbSet<AircraftPosition> AircraftPositions => Set<AircraftPosition>();
    public DbSet<AirspaceSector> AirspaceSectors => Set<AirspaceSector>();
    public DbSet<AtcCommand> AtcCommands => Set<AtcCommand>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteWaypoint> RouteWaypoints => Set<RouteWaypoint>();
    public DbSet<SystemEvent> SystemEvents => Set<SystemEvent>();
    public DbSet<Waypoint> Waypoints => Set<Waypoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Aircraft>(entity =>
        {
            entity.ToTable("Aircraft", "dbo");
            entity.HasKey(x => x.Id).HasName("PK_Aircraft");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.RegistrationNumber).HasColumnName("registrationNumber").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
            entity.Property(x => x.AircraftType).HasColumnName("aircraftType").HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.RegistrationNumber).IsUnique().HasDatabaseName("UQ_Aircraft_Registration");
        });

        modelBuilder.Entity<AircraftPosition>(entity =>
        {
            entity.ToTable("AircraftPositions", "dbo", table =>
            {
                table.HasCheckConstraint("CK_AircraftPositions_Heading", "[heading] >= 0 AND [heading] < 360");
                table.HasCheckConstraint("CK_AircraftPositions_Latitude", "[latitude] >= -90 AND [latitude] <= 90");
                table.HasCheckConstraint("CK_AircraftPositions_Longitude", "[longitude] >= -180 AND [longitude] <= 180");
                table.HasCheckConstraint("CK_AircraftPositions_Speed", "[speed] >= 0");
            });
            entity.HasKey(x => x.Id).HasName("PK_AircraftPositions");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.AircraftId).HasColumnName("aircraftId");
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
            entity.Property(x => x.Altitude).HasColumnName("altitude").HasPrecision(10, 2);
            entity.Property(x => x.Speed).HasColumnName("speed").HasPrecision(8, 2);
            entity.Property(x => x.Heading).HasColumnName("heading").HasPrecision(5, 2);
            entity.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasColumnType("datetime2(3)").HasDefaultValueSql("sysutcdatetime()");
            entity.HasIndex(x => x.AircraftId).IsUnique().HasDatabaseName("UQ_AircraftPositions_Aircraft");
            entity.HasOne(x => x.Aircraft).WithOne(x => x.AircraftPosition).HasForeignKey<AircraftPosition>(x => x.AircraftId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AircraftPositions_Aircraft");
        });

        modelBuilder.Entity<AirspaceSector>(entity =>
        {
            entity.ToTable("AirspaceSectors", "dbo", table =>
            {
                table.HasCheckConstraint("CK_AirspaceSectors_Altitude", "[minAltitude] >= 0 AND [maxAltitude] > [minAltitude]");
                table.HasCheckConstraint("CK_AirspaceSectors_Boundary", "isjson([boundaryData]) = 1");
            });
            entity.HasKey(x => x.Id).HasName("PK_AirspaceSectors");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.MinAltitude).HasColumnName("minAltitude");
            entity.Property(x => x.MaxAltitude).HasColumnName("maxAltitude");
            entity.Property(x => x.BoundaryData).HasColumnName("boundaryData").HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<AtcCommand>(entity =>
        {
            entity.ToTable("ATCCommands", "dbo", table => table.HasCheckConstraint("CK_ATCCommands_Status", "[status] IN ('Failed', 'Acknowledged', 'Sent', 'Pending')"));
            entity.HasKey(x => x.Id).HasName("PK_ATCCommands");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.CommandId).HasColumnName("commandId");
            entity.Property(x => x.FlightId).HasColumnName("flightId");
            entity.Property(x => x.CommandType).HasColumnName("commandType").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Parameters).HasColumnName("parameters").HasColumnType("nvarchar(max)");
            entity.Property(x => x.IssuedAt).HasColumnName("issuedAt").HasColumnType("datetime2(3)").HasDefaultValueSql("sysutcdatetime()");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).HasDefaultValue("Pending").IsRequired();
            entity.HasIndex(x => new { x.FlightId, x.IssuedAt }).HasDatabaseName("IX_ATCCommands_FlightId_IssuedAt");
            entity.HasIndex(x => x.CommandId).IsUnique().HasFilter("[commandId] IS NOT NULL").HasDatabaseName("UX_ATCCommands_CommandId");
            entity.HasOne(x => x.Flight).WithMany(x => x.AtcCommands).HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ATCCommands_Flights");
        });

        modelBuilder.Entity<Flight>(entity =>
        {
            entity.ToTable("Flights", "dbo", table =>
            {
                table.HasCheckConstraint("CK_Flights_Schedule", "[scheduledArrival] > [scheduledDeparture]");
                table.HasCheckConstraint("CK_Flights_Status", "[status] IN ('Cancelled', 'Completed', 'Active', 'Scheduled')");
            });
            entity.HasKey(x => x.Id).HasName("PK_Flights");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.AircraftId).HasColumnName("aircraftId");
            entity.Property(x => x.RouteId).HasColumnName("routeId");
            entity.Property(x => x.Callsign).HasColumnName("callsign").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).HasDefaultValue("Scheduled").IsRequired();
            entity.Property(x => x.ScheduledDeparture).HasColumnName("scheduledDeparture").HasColumnType("datetime2(0)");
            entity.Property(x => x.ActualDeparture).HasColumnName("actualDeparture").HasColumnType("datetime2(0)");
            entity.Property(x => x.ScheduledArrival).HasColumnName("scheduledArrival").HasColumnType("datetime2(0)");
            entity.Property(x => x.ActualArrival).HasColumnName("actualArrival").HasColumnType("datetime2(0)");
            entity.HasIndex(x => x.AircraftId).HasDatabaseName("IX_Flights_AircraftId");
            entity.HasIndex(x => x.RouteId).HasDatabaseName("IX_Flights_RouteId");
            entity.HasOne(x => x.Aircraft).WithMany(x => x.Flights).HasForeignKey(x => x.AircraftId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Flights_Aircraft");
            entity.HasOne(x => x.Route).WithMany(x => x.Flights).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Flights_Routes");
        });

        modelBuilder.Entity<Route>(entity =>
        {
            entity.ToTable("Routes", "dbo");
            entity.HasKey(x => x.Id).HasName("PK_Routes");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        });

        modelBuilder.Entity<RouteWaypoint>(entity =>
        {
            entity.ToTable("RouteWaypoints", "dbo", table => table.HasCheckConstraint("CK_RouteWaypoints_Sequence", "[sequenceNumber] > 0"));
            entity.HasKey(x => new { x.RouteId, x.SequenceNumber }).HasName("PK_RouteWaypoints");
            entity.Property(x => x.RouteId).HasColumnName("routeId");
            entity.Property(x => x.WaypointId).HasColumnName("waypointId");
            entity.Property(x => x.SequenceNumber).HasColumnName("sequenceNumber");
            entity.HasIndex(x => x.WaypointId).HasDatabaseName("IX_RouteWaypoints_WaypointId");
            entity.HasOne(x => x.Route).WithMany(x => x.RouteWaypoints).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RouteWaypoints_Routes");
            entity.HasOne(x => x.Waypoint).WithMany(x => x.RouteWaypoints).HasForeignKey(x => x.WaypointId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RouteWaypoints_Waypoints");
        });

        modelBuilder.Entity<SystemEvent>(entity =>
        {
            entity.ToTable("SystemEvents", "dbo");
            entity.HasKey(x => x.Id).HasName("PK_SystemEvents");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.FlightId).HasColumnName("flightId");
            entity.Property(x => x.EventType).HasColumnName("eventType").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasColumnType("nvarchar(max)");
            entity.Property(x => x.OccurredAt).HasColumnName("occurredAt").HasColumnType("datetime2(3)").HasDefaultValueSql("sysutcdatetime()");
            entity.HasIndex(x => new { x.FlightId, x.OccurredAt }).HasDatabaseName("IX_SystemEvents_FlightId_OccurredAt");
            entity.HasOne(x => x.Flight).WithMany(x => x.SystemEvents).HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SystemEvents_Flights");
        });

        modelBuilder.Entity<Waypoint>(entity =>
        {
            entity.ToTable("Waypoints", "dbo", table =>
            {
                table.HasCheckConstraint("CK_Waypoints_Latitude", "[latitude] >= -90 AND [latitude] <= 90");
                table.HasCheckConstraint("CK_Waypoints_Longitude", "[longitude] >= -180 AND [longitude] <= 180");
            });
            entity.HasKey(x => x.Id).HasName("PK_Waypoints");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
        });
    }
}
