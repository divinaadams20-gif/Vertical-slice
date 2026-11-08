namespace Api.Common.Authorization;

using Domain.Enums;

public static class Policies
{
    public const string CanManageCourses = nameof(CanManageCourses);
    public const string CanManageStudents = nameof(CanManageStudents);
    public const string CanManageAdmissions = nameof(CanManageAdmissions);
}

public static class AuthorizationSetup
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanManageCourses, policy =>
                policy.RequireRole(nameof(UserRole.Registrar)))
            .AddPolicy(Policies.CanManageStudents, policy =>
                policy.RequireRole(nameof(UserRole.Registrar)))
            .AddPolicy(Policies.CanManageAdmissions, policy =>
                policy.RequireRole(nameof(UserRole.Registrar)));

        return services;
    }
}