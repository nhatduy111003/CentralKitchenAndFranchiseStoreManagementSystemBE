// CentralKitchenAndFranchise.BLL/Exceptions/ForbiddenAccessException.cs
namespace CentralKitchenAndFranchise.BLL.Exceptions;

/// <summary>
/// Thrown when the user is authenticated but does not have permission/scope to access a resource.
/// This should map to HTTP 403 and ApiResponse.ErrorCode = "FORBIDDEN".
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }

    public ForbiddenAccessException(string message, Exception? innerException) : base(message, innerException) { }
}
