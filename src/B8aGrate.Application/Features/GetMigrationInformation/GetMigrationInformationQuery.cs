using B8aGrate.Application.Features.Abstract;
using B8aGrate.Domain.Projections;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.GetMigrationInformation;

public sealed class GetMigrationInformationQuery : MigrationRequestBase, IRequest<Result<MigrationInformationProjection>>;