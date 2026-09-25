using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Models;

namespace CreditosApp.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Aplicar migraciones pendientes
        await context.Database.MigrateAsync();

        // 1. Crear Rol Analista si no existe
        const string analistaRole = "Analista";
        if (!await roleManager.RoleExistsAsync(analistaRole))
        {
            await roleManager.CreateAsync(new IdentityRole(analistaRole));
        }

        // 2. Crear Usuario con rol Analista
        const string analistaEmail = "analista@creditos.com";
        var analistaUser = await userManager.FindByEmailAsync(analistaEmail);
        if (analistaUser == null)
        {
            analistaUser = new IdentityUser
            {
                UserName = analistaEmail,
                Email = analistaEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(analistaUser, "Analista123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(analistaUser, analistaRole);
            }
        }

        // 3. Crear Clientes de prueba
        // Cliente 1
        const string cliente1Email = "cliente1@creditos.com";
        var user1 = await userManager.FindByEmailAsync(cliente1Email);
        if (user1 == null)
        {
            user1 = new IdentityUser
            {
                UserName = cliente1Email,
                Email = cliente1Email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user1, "Cliente123!");
        }

        var cliente1 = await context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == user1.Id);
        if (cliente1 == null)
        {
            cliente1 = new Cliente
            {
                UsuarioId = user1.Id,
                IngresosMensuales = 3000.00m,
                Activo = true
            };
            context.Clientes.Add(cliente1);
            await context.SaveChangesAsync();
        }

        // Cliente 2
        const string cliente2Email = "cliente2@creditos.com";
        var user2 = await userManager.FindByEmailAsync(cliente2Email);
        if (user2 == null)
        {
            user2 = new IdentityUser
            {
                UserName = cliente2Email,
                Email = cliente2Email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user2, "Cliente123!");
        }

        var cliente2 = await context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == user2.Id);
        if (cliente2 == null)
        {
            cliente2 = new Cliente
            {
                UsuarioId = user2.Id,
                IngresosMensuales = 4500.00m,
                Activo = true
            };
            context.Clientes.Add(cliente2);
            await context.SaveChangesAsync();
        }

        // 4. Crear Solicitudes iniciales: 1 Pendiente y 1 Aprobada
        if (!await context.SolicitudesCredito.AnyAsync())
        {
            context.SolicitudesCredito.AddRange(
                new SolicitudCredito
                {
                    ClienteId = cliente1.Id,
                    MontoSolicitado = 5000.00m,
                    FechaSolicitud = DateTime.UtcNow.AddDays(-1),
                    Estado = EstadoSolicitud.Pendiente
                },
                new SolicitudCredito
                {
                    ClienteId = cliente2.Id,
                    MontoSolicitado = 10000.00m,
                    FechaSolicitud = DateTime.UtcNow.AddDays(-3),
                    Estado = EstadoSolicitud.Aprobado
                }
            );
            await context.SaveChangesAsync();
        }
    }
}
