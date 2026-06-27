using B8aGrate.Application.Infrastructure;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Data.Services;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Data.Sql.Repositories;
using B8aGrate.Rendering;
using B8aGrate.Rendering.ProjectionRenderers;
using B8aGrate.TypeAdapters.Infrastructure;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using YuckQi.Extensions.Mapping.Mapster;

namespace B8aGrate.Infrastructure.Configuration.Extensions;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDataRepositories()
        {
            services.AddSingleton<IMigrationRepositoryFactory, MigrationRepositoryFactory>();

            return services;
        }

        public IServiceCollection AddDataServices()
        {
            services.AddSingleton<IMigrationScriptDiscoverer, MigrationScriptDiscoverer>();
            services.AddSingleton<IMigrationVersionProvider, MigrationVersionProvider>();

            return services;
        }

        public IServiceCollection AddMapster()
        {
            var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;

            typeAdapterConfig.Scan(B8aGrateTypeAdapters.Assembly);

            services.AddSingleton<IMapper>(new Mapper(typeAdapterConfig));
            services.AddScoped<YuckQi.Extensions.Mapping.Abstractions.Abstract.Interfaces.IMapper, DefaultMapper>();

            return services;
        }

        public IServiceCollection AddMediatR()
        {
            services.AddMediatR(options =>
            {
                options.RegisterServicesFromAssembly(B8aGrateApplication.Assembly);
            });

            return services;
        }

        public IServiceCollection AddResultRendering()
        {
            services.AddSingleton<ResultRenderer>();
            services.AddSingleton<IProjectionRenderer, AdoptExistingMigrationsProjectionRenderer>();
            services.AddSingleton<IProjectionRenderer, ApplyMigrationsProjectionRenderer>();
            services.AddSingleton<IProjectionRenderer, MigrationInformationProjectionRenderer>();
            services.AddSingleton<IProjectionRenderer, MigrationSnapshotProjectionRenderer>();
            services.AddSingleton<IProjectionRenderer, RepairMigrationsProjectionRenderer>();
            services.AddSingleton<IProjectionRenderer, UndoMigrationsProjectionRenderer>();
            services.AddSingleton<IProjectionRenderer, UndoProvisioningProjectionRenderer>();

            return services;
        }
    }
}