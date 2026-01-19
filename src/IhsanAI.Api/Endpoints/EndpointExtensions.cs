namespace IhsanAI.Api.Endpoints;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapYakalananPolicelerEndpoints();
        app.MapPolicelerEndpoints();
        app.MapPoliceHavuzlariEndpoints();
        app.MapMusterilerEndpoints();
        app.MapKullanicilarEndpoints();
        app.MapSigortaSirketleriEndpoints();
        app.MapSubelerEndpoints();
        app.MapFirmalarEndpoints();
        app.MapAcenteKodlariEndpoints();
        app.MapYetkilerEndpoints();
        app.MapPoliceTurleriEndpoints();
        app.MapKomisyonOranlariEndpoints();
        app.MapPoliceRizikoAdresleriEndpoints();
        app.MapPoliceSigortalilariEndpoints();

        return app;
    }
}
