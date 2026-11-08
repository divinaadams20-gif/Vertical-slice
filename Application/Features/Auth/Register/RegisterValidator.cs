namespace Application.Features.Auth.Register;

using Domain.Enums;
using FluentValidation;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.Password).NotEmpty().MinimumLength(12);
        RuleFor(command => command.FirstName).NotEmpty();
        RuleFor(command => command.LastName).NotEmpty();
        RuleFor(command => command.Role)
            .Must(role => role is null ||
                (Enum.TryParse<UserRole>(role, true, out var parsed) && Enum.IsDefined(parsed)))
            .WithMessage("Role must be Student, Instructor, Registrar, or Admin.");
    }
}