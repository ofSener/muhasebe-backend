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

            // Finans yetkileri
            options.AddPolicy("CanViewMyEarnings", policy =>
                policy.RequireClaim("kazanclarimGorebilsin", "1"));

            // Drive integration
            options.AddPolicy("CanAccessDriveIntegration", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("driveEntegrasyonuGorebilsin", "1"));

            // Customer management
            options.AddPolicy("CanViewCustomers", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("musterileriGorebilsin", "1"));

            options.AddPolicy("CanViewCustomerDetail", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("musteriDetayGorebilsin", "1"));

            // Finance
            options.AddPolicy("CanViewFinanceDashboard", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("finansDashboardGorebilsin", "1"));

            options.AddPolicy("CanViewFinancePage", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("finansSayfasiniGorebilsin", "1"));

            options.AddPolicy("CanViewPolicyPayments", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("policeOdemeleriGorebilsin", "1"));

            options.AddPolicy("CanViewPaymentTracking", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("tahsilatTakibiGorebilsin", "1"));

            options.AddPolicy("CanViewFinanceReports", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("finansRaporlariGorebilsin", "1"));

            // Captured policies
            // Eski sistemde herkes görüntüleyebiliyordu (login olanlar)
            // PoliceYakalamaSecenekleri SADECE yakalama sırasında kullanılır
            options.AddPolicy("CanViewCapturedPolicies", policy =>
                policy.RequireAuthenticatedUser());

            // Excel import
            options.AddPolicy("CanImportPolicies", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("policeAktarabilsin", "1"));

            // Renewal tracking
            options.AddPolicy("CanViewRenewalTracking", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("yenilemeTakibiGorebilsin", "1"));
        });

        return services;
    }
}
