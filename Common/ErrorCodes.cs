namespace ProjetoLP.API.Common;

public static class ErrorCodes
{
    public const string NotFound         = "NOT_FOUND";
    public const string DuplicatePayment = "DUPLICATE_PAYMENT";
    public const string InactivePatient  = "INACTIVE_PATIENT";
    public const string InvalidFormat    = "INVALID_FORMAT";
    public const string EmptyField       = "EMPTY_FIELD";
    public const string CannotDelete     = "CANNOT_DELETE";
}
