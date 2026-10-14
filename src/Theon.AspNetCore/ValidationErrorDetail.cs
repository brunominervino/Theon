namespace Theon.AspNetCore;

/// <summary>One error in a problem document, with its code beside its message.</summary>
/// <remarks>
/// What <c>errors</c> holds when <see cref="TheonValidationOptions.IncludeErrorCodes"/> is on. A
/// message is for a person to read and is localizable; a code is what a client may branch on. A
/// response carrying only the message forces a client to match on text, which this library says
/// everywhere else is not a contract.
/// </remarks>
public sealed class ValidationErrorDetail
{
    internal ValidationErrorDetail(string message, string code)
    {
        Message = message;
        Code = code;
    }

    /// <summary>Gets the message, for a person to read.</summary>
    public string Message { get; }

    /// <summary>Gets the code, for a client to branch on.</summary>
    /// <remarks>
    /// The code the rule carried where it carried one, and the name of the library's own code
    /// otherwise — so <c>"cnpj.digito_verificador"</c> or <c>"TooSmall"</c>.
    /// </remarks>
    public string Code { get; }
}
