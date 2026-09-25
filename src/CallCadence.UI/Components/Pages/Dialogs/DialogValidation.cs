namespace CallCadence.UI.Components.Pages.Dialogs;

/// <summary>
/// Shared validation helpers used by the Settings dialogs.
/// </summary>
public static class DialogValidation
{
    public static bool ValidatePassword(string password)
    {
        return !string.IsNullOrWhiteSpace(password)
            && password.Length >= 12
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(ch => !char.IsLetterOrDigit(ch));
    }
}
