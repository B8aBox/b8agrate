using B8aGrate.Application.Features.Abstract;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.ValidateMigrationHistory;

public sealed class ValidateMigrationHistoryCommand : MigrationRequestBase, IRequest<Result>;