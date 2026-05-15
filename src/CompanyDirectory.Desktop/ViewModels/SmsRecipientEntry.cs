using CompanyDirectory.Shared.Dtos;

namespace CompanyDirectory_Desktop.ViewModels;

public class SmsRecipientEntry
{
    public UserDirectoryEntryDto User { get; init; } = null!;
    public bool HasMobile => !string.IsNullOrWhiteSpace(User.Mobile);
    public string MobileDisplay => HasMobile ? User.Mobile! : "(brak numeru)";
}
