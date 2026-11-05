namespace BinPicking.Core.Grasping;

/// <summary>Outcome of evaluating one location: a candidate, or the reason it was rejected.</summary>
public readonly record struct GraspEvaluation(GraspRejection Rejection, GraspCandidate? Candidate)
{
    public bool IsAccepted => Candidate != null;

    public static GraspEvaluation Rejected(GraspRejection rejection) => new(rejection, null);

    public static GraspEvaluation Accepted(GraspCandidate candidate) => new(GraspRejection.None, candidate);
}
