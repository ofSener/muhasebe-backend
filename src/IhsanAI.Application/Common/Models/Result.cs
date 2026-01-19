namespace IhsanAI.Application.Common.Models;

public class Result
{
    internal Result(bool succeeded, IEnumerable<Error> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; }
    public Error[] Errors { get; }

    public static Result Success()
    {
        return new Result(true, Array.Empty<Error>());
    }

    public static Result Failure(IEnumerable<Error> errors)
    {
        return new Result(false, errors);
    }

    public static Result Failure(Error error)
    {
        return new Result(false, new[] { error });
    }

    public static Result Failure(string code, string message)
    {
        return new Result(false, new[] { new Error(code, message) });
    }
}

public class Result<T> : Result
{
    internal Result(T? data, bool succeeded, IEnumerable<Error> errors)
        : base(succeeded, errors)
    {
        Data = data;
    }

    public T? Data { get; }

    public static Result<T> Success(T data)
    {
        return new Result<T>(data, true, Array.Empty<Error>());
    }

    public new static Result<T> Failure(IEnumerable<Error> errors)
    {
        return new Result<T>(default, false, errors);
    }

    public new static Result<T> Failure(Error error)
    {
        return new Result<T>(default, false, new[] { error });
    }

    public new static Result<T> Failure(string code, string message)
    {
        return new Result<T>(default, false, new[] { new Error(code, message) });
    }
}
