namespace AvaluxAuth.Models;

/// <summary>
/// Bounds and normalisation for the <c>page</c>/<c>limit</c> query parameters of the
/// user listings (L-04 in SECURITY-AUDIT.md). Controllers reject out-of-range values
/// with 400; the repositories clamp them again so that a caller which reaches a
/// repository through another path can neither ask for an unbounded result set nor
/// produce a negative OFFSET.
/// </summary>
public static class Pagination
{
    /// <summary>Largest page size a single request may ask for.</summary>
    public const int MaxLimit = 100;

    public static int NormalizePage(int page) => page < 0 ? 0 : page;

    /// <summary>
    /// Clamps <paramref name="limit"/> into <c>[1, MaxLimit]</c>. A missing limit means
    /// "as many as allowed", not "everything": the listing stays bounded.
    /// </summary>
    public static int NormalizeLimit(int? limit) =>
        limit is null ? MaxLimit : NormalizeLimit(limit.Value);

    public static int NormalizeLimit(int limit) => Math.Clamp(limit, 1, MaxLimit);

    /// <summary>
    /// Rows to skip for a page. The multiplication is done in 64 bits and saturated:
    /// <c>page * limit</c> for <c>int.MaxValue</c> would wrap around and a negative
    /// OFFSET makes both PostgreSQL and EF throw.
    /// </summary>
    public static int Skip(int page, int limit) =>
        (int)Math.Min((long)NormalizePage(page) * NormalizeLimit(limit), int.MaxValue);
}
