namespace IhsanAI.Api.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Poliçe yetkileri
            options.AddPolicy("CanViewAllPolicies", policy =>
                policy.RequireClaim("gorebilecegiPoliceler", "1"));

            options.AddPolicy("CanEditPolicy", policy =>
                policy.RequireClaim("policeDuzenleyebilsin", "1"));

            options.AddPolicy("CanViewPool", policy =>
                policy.RequireClaim("policeHavuzunuGorebilsin", "1"));

            options.AddPolicy("CanTransferPolicy", policy =>
                policy.RequireClaim("policeAktarabilsin", "1"));

            // Yönetim yetkileri
            options.AddPolicy("CanManagePermissions", policy =>
                policy.RequireClaim("yetkilerSayfasindaIslemYapabilsin", "1"));

            options.AddPolicy("CanManageAgencies", policy =>
                policy.RequireClaim("acenteliklerSayfasindaIslemYapabilsin", "1"));

            options.AddPolicy("CanEditCommission", policy =>
                policy.RequireClaim("komisyonOranlariniDuzenleyebilsin", "1"));

            options.AddPolicy("CanViewProducers", policy =>
                policy.RequireClaim("produktorleriGorebilsin", "1"));
        });

        return services;
    }
}
