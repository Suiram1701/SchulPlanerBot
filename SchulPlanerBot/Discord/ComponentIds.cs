namespace SchulPlanerBot.Discord;

public static class ComponentIds
{
    #region Modals
    private const string _modalPrefix = "Modal_";

    public const string CreateHomeworkModal = $"{_modalPrefix}CreateHomework";

    public const string ModifyHomeworkModal = $"{_modalPrefix}ModifyHomework:*,*";

    public static string CreateModifyHomeworkModal(string homeworkId, ulong? triggerMessage = null) =>
        $"{_modalPrefix}ModifyHomework:{homeworkId},{triggerMessage ?? 0}";

    public static class HomeworkModal
    {
        private const string _prefix = $"{_modalPrefix}Homework_";

        public const string DueDate = $"{_prefix}Due";

        public const string Subject = $"{_prefix}Subject";

        public const string Title = $"{_prefix}Title";

        public const string Details = $"{_prefix}Details";
    }
    #endregion

    #region Components
    private const string _componentPrefix = "Component_";

    public static string CreateGetHomeworkPageComponent(int pageIndex, string cacheId) =>
        $"{_componentPrefix}GetHomeworkPage:{pageIndex},{cacheId}";
    
    public const string GetHomeworkPageComponent = $"{_componentPrefix}GetHomeworkPage:*,*";
    
    public const string GetHomeworksSelectComponent = $"{_componentPrefix}GetHomeworksSelect:*";

    public static string CreateGetHomeworksSelectComponent(string cacheId) =>
        $"{_componentPrefix}GetHomeworksSelect:{cacheId}";
    
    public const string ModifyHomeworkSelectComponent = $"{_componentPrefix}ModifyHomeworkSelect";
    
    public const string DeleteHomeworkSelectComponent = $"{_componentPrefix}DeleteHomeworkSelect";
    #endregion
}
