namespace SchulPlanerBot.Discord;

public static class ComponentIds
{
    public const string CreateHomeworkModal = "Modal_CreateHomework";

    public const string ModifyHomeworkModal = "Modal_ModifyHomework:*";

    public static string ModifyHomeworkModalId(string homeworkId) => ModifyHomeworkModal.Replace("*", homeworkId);
}
