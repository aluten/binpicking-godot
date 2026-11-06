namespace BinPicking.Core.Grasping;

/// <summary>Outcome of evaluating one location: a candidate, or the reason it was rejected.</summary>
public readonly record struct GraspEvaluation(GraspRejection Rejection, GraspCandidate? Candidate)
{
    /// <summary>True when the location passed every requirement.</summary>
    public bool IsAccepted => Candidate != null;

    /// <summary>The location failed this requirement.</summary>
    public static GraspEvaluation Rejected(GraspRejection rejection) => new(rejection, null);

    /// <summary>The location passed every requirement.</summary>
    public static GraspEvaluation Accepted(GraspCandidate candidate) => new(GraspRejection.None, candidate);
}
