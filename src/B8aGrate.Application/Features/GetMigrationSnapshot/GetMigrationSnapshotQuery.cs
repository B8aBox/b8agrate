using B8aGrate.Application.Features.Abstract;
using B8aGrate.Domain.Projections;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.GetMigrationSnapshot;

public sealed class GetMigrationSnapshotQuery : MigrationRequestBase, IRequest<Result<MigrationSnapshotProjection>>;