﻿namespace SchulPlanerBot.Business.Models;

public class HomeworkSubscription
{
    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }

    public bool AnySubject { get; set; }

    public bool NoSubject { get; set; }

    public string[] Include { get; set; } = [];
}
