namespace IhsanAI.Application.Common.Models;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.");
    public static readonly Error NotFound = new("Error.NotFound", "The requested resource was not found.");
    public static readonly Error Unauthorized = new("Error.Unauthorized", "You are not authorized to perform this action.");
    public static readonly Error Forbidden = new("Error.Forbidden", "Access to this resource is forbidden.");
    public static readonly Error Validation = new("Error.Validation", "One or more validation errors occurred.");
}
