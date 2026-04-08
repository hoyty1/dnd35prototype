using UnityEngine;

/// <summary>
/// Utility class for hexagonal grid math.
/// Uses "pointy-top" hexagons with axial (q, r) coordinates.
/// </summary>
public static class HexUtils
{
    // Hex size (outer radius = distance from center to vertex)
    public const float HexSize = 0.5f;

    // Derived constants
    public static readonly float HexWidth = Mathf.Sqrt(3f) * HexSize;   // horizontal span
    public static readonly float HexHeight = 2f * HexSize;               // vertical span

    /// <summary>
    /// Convert axial hex coordinates (q, r) to world position.
    /// Pointy-top layout.
    /// </summary>
    public static Vector3 AxialToWorld(int q, int r)
    {
        float x = HexWidth * (q + r * 0.5f);
        float y = HexHeight * 0.75f * r;
        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// Convert world position to the nearest axial hex coordinate.
    /// </summary>
    public static Vector2Int WorldToAxial(Vector3 worldPos)
    {
        float q = (worldPos.x * Mathf.Sqrt(3f) / 3f - worldPos.y / 3f) / HexSize;
        float r = (worldPos.y * 2f / 3f) / HexSize;
        return CubeRound(q, -q - r, r);
    }

    /// <summary>
    /// Round fractional cube coordinates to nearest hex.
    /// </summary>
    private static Vector2Int CubeRound(float fq, float fs, float fr)
    {
        int rq = Mathf.RoundToInt(fq);
        int rs = Mathf.RoundToInt(fs);
        int rr = Mathf.RoundToInt(fr);

        float dq = Mathf.Abs(rq - fq);
        float ds = Mathf.Abs(rs - fs);
        float dr = Mathf.Abs(rr - fr);

        if (dq > ds && dq > dr)
            rq = -rs - rr;
        else if (ds > dr)
            rs = -rq - rr;
        // else rr = -rq - rs; (not needed for axial)

        return new Vector2Int(rq, rr);
    }

    /// <summary>
    /// Hex distance between two axial coordinates.
    /// </summary>
    public static int HexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = b.x - a.x;
        int dr = b.y - a.y;
        return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
    }

    /// <summary>
    /// The 6 axial direction offsets for pointy-top hex neighbors.
    /// </summary>
    public static readonly Vector2Int[] Directions = new Vector2Int[]
    {
        new Vector2Int(+1,  0), // East
        new Vector2Int(+1, -1), // NE
        new Vector2Int( 0, -1), // NW
        new Vector2Int(-1,  0), // West
        new Vector2Int(-1, +1), // SW
        new Vector2Int( 0, +1), // SE
    };
}
