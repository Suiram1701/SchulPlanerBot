using SchulPlanerBot.Business.Errors;

namespace SchulPlanerBot.Business;

public class UpdateResult
{
    public bool Success { get; }

    public UpdateError[] Errors { get; }

    private UpdateResult(bool success, UpdateError[] errors)
    {
        Success = success;
        Errors = errors;
    }

    public static UpdateResult Succeeded() => new(true, []);

    public static UpdateResult Failed(params UpdateError[] errors) => new(false, errors);
}