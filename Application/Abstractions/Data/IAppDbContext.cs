namespace Application.Abstractions.Data;

using Domain.Entities;
using Microsoft.EntityFrameworkCore;

public interface IAppDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<RevokedToken> RevokedTokens { get; }
    DbSet<Student> Students { get; }
    DbSet<Course> Courses { get; }
    DbSet<Admission> Admissions { get; }
    DbSet<AdmissionCourse> AdmissionCourses { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
