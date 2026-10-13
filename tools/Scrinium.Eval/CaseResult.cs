namespace Scrinium.Eval;

public sealed record CaseResult(EvaluationCase Case, AnswerObservation Observation, IReadOnlyList<CheckResult> Checks)
{
    public bool Passed => Checks.All(check => check is not CheckResult.Failed);
}
