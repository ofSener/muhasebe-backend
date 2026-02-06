using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Infrastructure.Persistence;
using IhsanAI.Infrastructure.Services;

namespace IhsanAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(5, 7, 0)),
                mySqlOptions =>
                {
                    mySqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IGoogleDriveService, GoogleDriveService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddScoped<IAcentelikService, AcentelikService>();

        // Memory Cache (Excel import session için)
        services.AddMemoryCache();

        return services;
    }
}
