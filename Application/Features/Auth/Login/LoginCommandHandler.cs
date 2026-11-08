namespace Application.Features.Auth.Login;

using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

public sealed class LoginCommandHandler(
    IAppDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider)
    : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);

        if (user is null || !user.IsActive || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return Result<LoginResponse>.Failure(new Error(
                "Auth.InvalidCredentials",
                "Invalid email or password."));
        }

        user.LastLoginAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokenService.GenerateAccessToken(user),
            tokenService.GetAccessTokenExpiry(),
            user.Role.ToString(),
            $"{user.FirstName} {user.LastName}"));
    }
}