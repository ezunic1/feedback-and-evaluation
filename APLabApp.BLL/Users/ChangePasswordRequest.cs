namespace APLabApp.BLL.Users;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
