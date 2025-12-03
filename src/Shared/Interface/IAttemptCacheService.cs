namespace Examination.Api.Shared.Services.Interface
{

    public record AttemptCache(
        int AttemptId,
        int QuizId,
        string UserId,
        DateTime DeadlineUtc,
        Dictionary<int, HashSet<int>> Answers);

    public interface IAttemptCacheService
    {
        Task CreateAttemptCacheAsync(int attemptId, int quizId, string userId, DateTime deadlineUtc, TimeSpan expiry);
        Task<AttemptCache?> GetAttemptCacheAsync(int attemptId);
        Task SaveAnswerAsync(int attemptId, int questionId, HashSet<int> selectedAnswerIds);
        Task DeleteAttemptCacheAsync(int attemptId);        
    }
}
