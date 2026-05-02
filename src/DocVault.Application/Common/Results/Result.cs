namespace DocVault.Application.Common.Results;

public class Result
{
  public bool IsSuccess { get; }

  /// <summary>Human-readable error message. Null on success.</summary>
  public string? Error { get; }

  /// <summary>Short machine-readable error key (e.g. "NOT_FOUND"). Null on success.</summary>
  public string? ErrorCode { get; }

  protected Result(bool success, string? error, string? errorCode = null)
  {
    IsSuccess = success;
    Error     = error;
    ErrorCode = errorCode;
  }

  public static Result Success() => new(true, null);
  public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode ?? error);
}

public class Result<T> : Result
{
  public T? Value { get; }

  private Result(bool success, T? value, string? error, string? errorCode) : base(success, error, errorCode)
  {
    Value = value;
  }

  public static Result<T> Success(T value) => new(true, value, null, null);
  public static new Result<T> Failure(string error, string? errorCode = null) => new(false, default, error, errorCode ?? error);
}
