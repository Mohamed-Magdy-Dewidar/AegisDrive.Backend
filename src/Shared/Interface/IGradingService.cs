using Examination.Api.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Interface
{

    // A record to hold the results of the grading process    
    public record GradingResult(double Score,List<UserAnswer> AnswersToPersist,double TotalPossibleMark);
    public interface IGradingService
    {
        /// <summary>
        /// Grades a given attempt based on answers stored in the cache.
        /// </summary>
        /// <param name="attemptId">The ID of the attempt to grade.</param>
        /// <returns>A GradingResult containing the calculated score and the UserAnswer entities to be saved.</returns>
        Task<GradingResult> GradeAttemptAsync(int attemptId);
    }
}