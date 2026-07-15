/// <summary>
/// Shared layout constants for the info panel and its views.
/// The panel spans the full screen minus the right column, split into 4 columns.
/// </summary>
internal static class InfoPanelLayout
{
    public const float W      = HUDHelpers.INFO_W;
    public const float H      = HUDHelpers.INFO_H;
    public const float HDR_H  = 36f;
    public const float PAD    = 14f;
    public const float BODY_H = H - HDR_H;

    // 4 content columns
    public const float COL_GAP = 12f;
    public const float COL_W   = (W - PAD * 2f - COL_GAP * 3f) / 4f;
    public const float C0      = PAD;
    public const float C1      = C0 + COL_W + COL_GAP;
    public const float C2      = C1 + COL_W + COL_GAP;
    public const float C3      = C2 + COL_W + COL_GAP;
}
