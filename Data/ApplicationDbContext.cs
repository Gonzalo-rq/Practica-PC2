using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Models;

namespace CreditosApp.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Regla: Un cliente solo puede tener una solicitud en estado Pendiente (Estado = 0)
        builder.Entity<SolicitudCredito>()
            .HasIndex(s => s.ClienteId)
            .IsUnique()
            .HasFilter("Estado = 0");

        builder.Entity<Cliente>()
            .HasOne(c => c.Usuario)
            .WithOne()
            .HasForeignKey<Cliente>(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unicidad de MessageId para idempotencia en procesamiento de eventos RabbitMQ
        builder.Entity<Notificacion>()
            .HasIndex(n => n.MessageId)
            .IsUnique();
    }
}
