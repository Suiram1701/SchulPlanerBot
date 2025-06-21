using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Modules.Models;

public record HomeworkOverview(
    Homework[] Homeworks,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    string SelectCustomId,
    int PageIndex = 0,
    Guid? DisplayedHomeworkId = null);