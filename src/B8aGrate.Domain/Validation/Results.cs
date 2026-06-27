using YuckQi.Domain.Validation;

namespace B8aGrate.Domain.Validation;

public static class Results
{
    public static Result Failure(params IReadOnlyCollection<ResultDetail> details) => new(details);

    public static Result Failure(string message, ResultCode? code = null, string? property = null,
        ResultType type = ResultType.Error) => new([new ResultDetail(message, code, property, type)]);

    public static Result<T> Failure<T>(params IReadOnlyCollection<ResultDetail> details) => new(details);

    public static Result<T> Failure<T>(string message, ResultCode? code = null, string? property = null,
        ResultType type = ResultType.Error) => new(new ResultDetail(message, code, property, type));

    public static Result Success() => new(null);

    public static Result<T> Success<T>() => new(default(T));

    public static Result<T> Success<T>(T value) => new(value);
}