namespace IhsanAI.Api.Endpoints;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Auth endpoints (must be first, no auth required for login)
        app.MapAuthEndpoints();

        // Core business endpoints (CQRS)
        app.MapYakalananPolicelerEndpoints();
        app.MapPolicelerEndpoints();
        app.MapPoliceHavuzlariEndpoints();
        app.MapMusterilerEndpoints();
        app.MapKullanicilarEndpoints();
        app.MapAcenteKodlariEndpoints();
        app.MapYetkilerEndpoints();
        app.MapKomisyonGruplariEndpoints();
        app.MapKazanclarEndpoints();
        app.MapDriveEndpoints();
        app.MapDashboardEndpoints();
        app.MapExcelImportEndpoints();

        // Simple lookups (direct DB queries, no CQRS)
        app.MapLookupEndpoints();

        return app;
    }
}
