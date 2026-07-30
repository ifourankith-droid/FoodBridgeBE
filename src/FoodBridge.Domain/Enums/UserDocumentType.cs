namespace FoodBridge.Domain.Enums;

/// <summary>Kind of evidence a user submits for account verification. Stored as tinyint.</summary>
public enum UserDocumentType : byte
{
    /// <summary>A government photo ID — Aadhaar, driving licence, voter ID, passport.</summary>
    IdProof = 1,

    /// <summary>
    /// A live photo of the person, so an admin can check it against the ID rather than only
    /// confirming the ID itself looks plausible.
    /// </summary>
    Selfie = 2,
}
