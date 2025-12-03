using AegisDrive.Api.Shared.MarkerInterface;
namespace AegisDrive.Api.Shared.ResultEndpoint;

public class Result<TValue> : Result , MarkerInterface.IResult
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) =>
        _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result can not be accessed.");

    public static implicit operator Result<TValue>(TValue? value) => Create(value);

    
}
