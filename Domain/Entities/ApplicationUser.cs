namespace Domain.Entities;

using Domain.Common;
using Domain.Enums;

public sealed class ApplicationUser : BaseEntity
{
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.Student;
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime? TokensValidFromUtc { get; set; }
}