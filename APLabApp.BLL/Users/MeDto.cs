namespace APLabApp.BLL.Users
{
    public class MeDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string Role { get; set; } = "guest";
        public string? InternSeasonName { get; set; }
        public string? MentorSeasonName { get; set; }
    }
}
