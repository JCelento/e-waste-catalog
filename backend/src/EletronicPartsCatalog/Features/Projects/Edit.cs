using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EletronicPartsCatalog.Infrastructure;
using EletronicPartsCatalog.Infrastructure.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EletronicPartsCatalog.Features.Projects
{
    public class Edit
    {
        public class ProjectData
        {
            public string Title { get; set; }

            public string Description { get; set; }

            public string Body { get; set; }

            public string ProjectImage { get; set; }
        }

        public class Command : IRequest<ProjectEnvelope>
        {
            public ProjectData Project { get; set; }
            public string Slug { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(x => x.Project).NotNull();
            }
        }

        public class Handler : IRequestHandler<Command, ProjectEnvelope>
        {
            private readonly EletronicPartsCatalogContext _context;
            private readonly ICurrentUserAccessor _currentUserAccessor;

            public Handler(EletronicPartsCatalogContext context, ICurrentUserAccessor currentUserAccessor)
            {
                _context = context;
                _currentUserAccessor = currentUserAccessor;
            }

            public async Task<ProjectEnvelope> Handle(Command message, CancellationToken cancellationToken)
            {
                var Project = await _context.Projects
                    .Include(x => x.Author)
                    .Where(x => x.Slug == message.Slug)
                    .FirstOrDefaultAsync(cancellationToken);

                if (Project == null)
                {
                    throw new RestException(HttpStatusCode.NotFound, new { Project = "Project not found." });
                }

                if (Project.Author.Username != _currentUserAccessor.GetCurrentUsername())
                {
                    throw new RestException(HttpStatusCode.Unauthorized, new { Project = "Projects can only be altered by its owner." });
                }

                Project.Description = message.Project.Description ?? Project.Description;
                Project.Body = message.Project.Body ?? Project.Body;
                Project.Title = message.Project.Title ?? Project.Title;
                Project.ProjectImage = message.Project.ProjectImage ?? Project.ProjectImage;
                Project.Slug = Project.Title.GenerateSlug();

                if (_context.ChangeTracker.Entries().First(x => x.Entity == Project).State == EntityState.Modified)
                {
                    Project.UpdatedAt = DateTime.UtcNow;
                }
                
                await _context.SaveChangesAsync(cancellationToken);

                return new ProjectEnvelope(await _context.Projects.GetAllData()
                    .Where(x => x.Slug == Project.Slug)
                    .FirstOrDefaultAsync(cancellationToken));            }
        }
    }
}
