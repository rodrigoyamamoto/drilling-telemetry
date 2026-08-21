namespace DrillingTelemetry.WitsmlImporter.Parsing;

/// <summary>
/// Represents the intermediate result of parsing a single WITSML 1.4.1.1
/// log element, before mapping its rows to telemetry readings.
/// </summary>
/// <param name="UidWell">Well identifier from the log attributes.</param>
/// <param name="NameWell">Well name from the log element.</param>
/// <param name="UidWellbore">Wellbore identifier from the log attributes.</param>
/// <param name="NameWellbore">Wellbore name from the log element.</param>
/// <param name="Uid">Log identifier from the log attributes.</param>
/// <param name="Name">Log name from the log element.</param>
/// <param name="IndexType">
/// Index type declared by the log, for example <c>measured depth</c>.
/// </param>
/// <param name="IndexCurve">
/// Mnemonic of the curve used as the log index, for example <c>DEPT</c>.
/// </param>
/// <param name="Curves">
/// Curve information entries parsed from <c>logCurveInfo</c> elements.
/// The order follows the XML document and may differ from the
/// <see cref="Mnemonics"/> order.
/// </param>
/// <param name="Mnemonics">
/// Mnemonics parsed from the <c>mnemonicList</c> element, defining the
/// column order of each data row.
/// </param>
/// <param name="Units">
/// Units parsed from the <c>unitList</c> element, positionally aligned
/// with <see cref="Mnemonics"/>. An empty string indicates a unitless
/// curve.
/// </param>
/// <param name="DataRows">
/// Data rows parsed from <c>data</c> elements. Each row contains exactly
/// <see cref="Mnemonics"/>.Count fields.
/// </param>
internal sealed record WitsmlLog(
    string UidWell,
    string NameWell,
    string UidWellbore,
    string NameWellbore,
    string Uid,
    string Name,
    string IndexType,
    string IndexCurve,
    IReadOnlyList<WitsmlLogCurveInfo> Curves,
    IReadOnlyList<string> Mnemonics,
    IReadOnlyList<string> Units,
    IReadOnlyList<string[]> DataRows);
