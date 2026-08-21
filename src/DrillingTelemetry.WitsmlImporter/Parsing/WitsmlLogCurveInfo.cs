namespace DrillingTelemetry.WitsmlImporter.Parsing;

/// <summary>
/// Describes a single WITSML log curve declared in a
/// <c>logCurveInfo</c> element.
/// </summary>
/// <param name="Mnemonic">Curve mnemonic, for example <c>DEPT</c>.</param>
/// <param name="Unit">Curve unit, for example <c>m</c> or <c>gAPI</c>.</param>
/// <param name="NullValue">
/// Value that represents a missing measurement for this curve.
/// </param>
internal sealed record WitsmlLogCurveInfo(
    string Mnemonic,
    string Unit,
    string NullValue);
