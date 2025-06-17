using System.ComponentModel.DataAnnotations;

namespace SchulPlanerBot.Options;

public class ResponseOptions
{
    [Range(1, 25)]
    public int MaxObjectsPerSelect { get; set; } = 25;     // Max for the select component
}