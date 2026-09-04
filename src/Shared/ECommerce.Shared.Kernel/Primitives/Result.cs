namespace ECommerce.Shared.Kernel.Primitives;

/// <summary>
/// An outcome that is either a value or a stated reason. Returning an empty result in
/// place of a reason is forbidden (FR-029), and this type is what makes that structural
/// rather than a convention.
/// </summary>
public readonly record struct Result<T>
{
    private Result(bool succeeded, T? value, string? reasonCode, string? detail)
    {
        Succeeded = succeeded;
        Value = value;
        ReasonCode = reasonCode;
        Detail = detail;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public string? ReasonCode { get; }
    public string? Detail { get; }

    public static Result<T> Ok(T value) => new(true, value, null, null);

    public static Result<T> Fail(string reasonCode, string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("A failure must carry a reason code (FR-029).", nameof(reasonCode));
        return new Result<T>(false, default, reasonCode, detail);
    }
}
