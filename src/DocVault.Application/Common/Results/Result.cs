namespace DocVault.Application.Common.Results;

public class Result
{
  public bool IsSuccess { get; }

  /// <summary>Human-readable error message. <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.</summary>
  public string? Error { get; }

  /// <summary>
  /// Short, machine-readable error key (e.g. <c>"NOT_FOUND"</c>, <c>"DUPLICATE"</c>).
  /// Distinct from <see cref="Error"/> so callers can branch on codes without parsing messages.
  /// <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.
  /// </summary>
  public string? ErrorCode { get; }

  protected Result(bool success, string? error, string? errorCode = null)
  {
    IsSuccess = success;
    Error     = error;
    ErrorCode = errorCode;
  }

  public static Result Success() => new(true, null);

  /// <param name="error">Human-readable message shown to callers.</param>
  /// <param name="errorCode">Optional short machine-readable key. Defaults to <paramref name="error"/> when omitted.</param>
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

  /// <param name="error">Human-readable message shown to callers.</param>
  /// <param name="errorCode">Optional short machine-readable key. Defaults to <paramref name="error"/> when omitted.</param>
  public static new Result<T> Failure(string error, string? errorCode = null) => new(false, default, error, errorCode ?? error);
}
