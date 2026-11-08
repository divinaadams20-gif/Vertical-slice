namespace Api.Common.Authorization;

using System.Security.Claims;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;

public sealed class AdmissionOwnerHandler
    : AuthorizationHandler<AdmissionOwnerRequirement, Admission>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdmissionOwnerRequirement requirement,
        Admission resource)
    {
        if (context.User.IsInRole(nameof(UserRole.Registrar)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var studentIdClaim = context.User.FindFirstValue("student_id");
        if (Guid.TryParse(studentIdClaim, out var studentId) && resource.StudentId == studentId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}