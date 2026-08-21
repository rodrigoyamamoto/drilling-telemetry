using System.Globalization;
using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.WitsmlImporter.Parsing;

/// <summary>
/// Maps a parsed <see cref="WitsmlLog"/> to a list of
/// <see cref="TelemetryReading"/> values ready for publication.
/// </summary>
/// <remarks>
/// <para>
/// The mapper locates columns by the <c>mnemonicList</c> order, never
/// assumes the <c>logCurveInfo</c> order matches, and uses the
/// positionally-aligned <c>unitList</c> for unit validation.
/// </para>
/// <para>
/// The depth column is identified by the log's <c>indexCurve</c>
/// element, not by a hardcoded mnemonic. This aligns with WITSML
/// semantics where the index curve defines the depth column, regardless
/// of its mnemonic name (for example <c>DEPT</c> or <c>Mdepth</c>).
/// </para>
/// <para>
/// The remaining required curves (<c>DTIM</c>, <c>GR</c>, <c>SPP</c>,
/// <c>TEMP</c>) are located by mnemonic. Rows where a required curve is
/// empty or uses its declared <c>nullValue</c> are rejected.
/// </para>
/// </remarks>
internal sealed class TelemetryReadingMapper
{
    private const string DtimMnemonic = "DTIM";

    private const string GrMnemonic = "GR";

    private const string SppMnemonic = "SPP";

    private const string TempMnemonic = "TEMP";

    private const string MetreUnit = "m";

    private const string FeetUnit = "ft";

    private const string GApiUnit = "gAPI";

    private const string PsiUnit = "psi";

    private const string DegCUnit = "degC";

    private const double FeetToMetres = 0.3048;

    private static readonly string[] RequiredMnemonics =
    {
        DtimMnemonic, GrMnemonic, SppMnemonic, TempMnemonic
    };

    /// <summary>
    /// Converts a parsed WITSML log into telemetry readings.
    /// </summary>
    /// <param name="log">
    /// The intermediate log produced by <see cref="WitsmlLogParser"/>.
    /// </param>
    /// <param name="deviceId">
    /// Device identifier assigned to every reading.
    /// </param>
    /// <param name="acquisitionSessionId">
    /// Acquisition session identifier shared by every reading in this
    /// import run.
    /// </param>
    /// <returns>
    /// A list of telemetry readings in data-line order, with sequence
    /// numbers starting at 1.
    /// </returns>
    /// <exception cref="WitsmlParseException">
    /// Thrown when a required mnemonic is missing, a unit is unsupported,
    /// a value is empty or null, or timestamps are not strictly
    /// increasing.
    /// </exception>
    public IReadOnlyList<TelemetryReading> Map(
        WitsmlLog log,
        string deviceId,
        Guid acquisitionSessionId)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        Dictionary<string, int> columnIndex =
            BuildColumnIndex(log.Mnemonics);

        int depthColumnIndex = ResolveDepthColumnIndex(
            log.IndexCurve, columnIndex);

        EnsureRequiredMnemonicsPresent(columnIndex);

        Dictionary<string, string> unitByMnemonic =
            BuildUnitMap(log.Mnemonics, log.Units);

        Dictionary<string, WitsmlLogCurveInfo> curveInfo =
            BuildCurveInfoMap(log.Curves);

        WitsmlLogCurveInfo depthCurveInfo =
            EnsureIndexCurveInfoPresent(log.IndexCurve, curveInfo);

        EnsureRequiredCurveInfoPresent(curveInfo);
        ValidateUnits(log, unitByMnemonic);

        List<TelemetryReading> readings = new(log.DataRows.Count);

        DateTimeOffset? previousTimestamp = null;
        double? previousDepthMetres = null;

        for (int i = 0; i < log.DataRows.Count; i++)
        {
            int lineNumber = i + 1;
            string[] row = log.DataRows[i];

            double depthMetres = ParseDepth(
                row[depthColumnIndex],
                unitByMnemonic[log.IndexCurve],
                depthCurveInfo,
                lineNumber);

            DateTimeOffset timestampUtc = ParseTimestamp(
                row[columnIndex[DtimMnemonic]],
                curveInfo[DtimMnemonic],
                lineNumber);

            double gammaRayApi = ParseMeasurement(
                row[columnIndex[GrMnemonic]],
                curveInfo[GrMnemonic],
                GrMnemonic,
                lineNumber);

            double pressurePsi = ParseMeasurement(
                row[columnIndex[SppMnemonic]],
                curveInfo[SppMnemonic],
                SppMnemonic,
                lineNumber);

            double temperatureCelsius = ParseMeasurement(
                row[columnIndex[TempMnemonic]],
                curveInfo[TempMnemonic],
                TempMnemonic,
                lineNumber);

            EnsureTimestampAdvances(
                timestampUtc,
                previousTimestamp,
                lineNumber);

            (DrillingOperation operation, double depthChangeRate) =
                ComputeOperationAndRate(
                    depthMetres,
                    previousDepthMetres,
                    timestampUtc,
                    previousTimestamp);

            readings.Add(new TelemetryReading
            {
                DeviceId = deviceId,
                AcquisitionSessionId = acquisitionSessionId,
                SequenceNumber = readings.Count + 1,
                WellId = log.UidWell,
                WellName = log.NameWell,
                WellboreId = log.UidWellbore,
                WellboreName = log.NameWellbore,
                MeasuredDepthMetres = depthMetres,
                DrillingOperation = operation,
                DepthChangeRateMetresPerHour = depthChangeRate,
                PressurePsi = pressurePsi,
                TemperatureCelsius = temperatureCelsius,
                GammaRayApi = gammaRayApi,
                TimestampUtc = timestampUtc,
                AcquisitionMode = TelemetryAcquisitionMode.HistoricalImport
            });

            previousTimestamp = timestampUtc;
            previousDepthMetres = depthMetres;
        }

        return readings;
    }

    private static Dictionary<string, int> BuildColumnIndex(
        IReadOnlyList<string> mnemonics)
    {
        Dictionary<string, int> index = new(StringComparer.Ordinal);

        for (int i = 0; i < mnemonics.Count; i++)
        {
            index[mnemonics[i]] = i;
        }

        return index;
    }

    private static int ResolveDepthColumnIndex(
        string indexCurve,
        Dictionary<string, int> columnIndex)
    {
        if (string.IsNullOrWhiteSpace(indexCurve))
        {
            throw new WitsmlParseException(
                "The log indexCurve is empty.");
        }

        if (!columnIndex.TryGetValue(indexCurve, out int index))
        {
            throw new WitsmlParseException(
                $"The index curve '{indexCurve}' is not present in " +
                "the mnemonic list.");
        }

        return index;
    }

    private static void EnsureRequiredMnemonicsPresent(
        Dictionary<string, int> columnIndex)
    {
        foreach (string mnemonic in RequiredMnemonics)
        {
            if (!columnIndex.ContainsKey(mnemonic))
            {
                throw new WitsmlParseException(
                    $"Required mnemonic '{mnemonic}' is not present " +
                    "in the mnemonic list.");
            }
        }
    }

    private static Dictionary<string, string> BuildUnitMap(
        IReadOnlyList<string> mnemonics,
        IReadOnlyList<string> units)
    {
        if (mnemonics.Count != units.Count)
        {
            throw new WitsmlParseException(
                $"The mnemonic list has {mnemonics.Count} columns " +
                $"but the unit list has {units.Count} columns.");
        }

        Dictionary<string, string> map = new(StringComparer.Ordinal);

        for (int i = 0; i < mnemonics.Count; i++)
        {
            map[mnemonics[i]] = units[i];
        }

        return map;
    }

    private static Dictionary<string, WitsmlLogCurveInfo> BuildCurveInfoMap(
        IReadOnlyList<WitsmlLogCurveInfo> curves)
    {
        Dictionary<string, WitsmlLogCurveInfo> map =
            new(StringComparer.Ordinal);

        foreach (WitsmlLogCurveInfo curve in curves)
        {
            map[curve.Mnemonic] = curve;
        }

        return map;
    }

    private static WitsmlLogCurveInfo EnsureIndexCurveInfoPresent(
        string indexCurve,
        Dictionary<string, WitsmlLogCurveInfo> curveInfo)
    {
        if (!curveInfo.TryGetValue(indexCurve, out WitsmlLogCurveInfo? info))
        {
            throw new WitsmlParseException(
                $"The index curve '{indexCurve}' has no logCurveInfo " +
                "entry.");
        }

        return info;
    }

    private static void EnsureRequiredCurveInfoPresent(
        Dictionary<string, WitsmlLogCurveInfo> curveInfo)
    {
        foreach (string mnemonic in RequiredMnemonics)
        {
            if (!curveInfo.ContainsKey(mnemonic))
            {
                throw new WitsmlParseException(
                    $"Required mnemonic '{mnemonic}' has no " +
                    "logCurveInfo entry.");
            }
        }
    }

    private static void ValidateUnits(
        WitsmlLog log,
        Dictionary<string, string> unitByMnemonic)
    {
        ValidateDepthUnit(log.IndexCurve, unitByMnemonic[log.IndexCurve]);
        ValidateUnit(GrMnemonic, unitByMnemonic[GrMnemonic]);
        ValidateUnit(SppMnemonic, unitByMnemonic[SppMnemonic]);
        ValidateUnit(TempMnemonic, unitByMnemonic[TempMnemonic]);
    }

    private static void ValidateDepthUnit(string indexCurve, string unit)
    {
        if (unit is not (MetreUnit or FeetUnit))
        {
            throw new WitsmlParseException(
                $"Index curve '{indexCurve}' has unsupported unit " +
                $"'{unit}'. Supported depth units: m, ft.");
        }
    }

    private static void ValidateUnit(string mnemonic, string unit)
    {
        bool valid = mnemonic switch
        {
            GrMnemonic => unit == GApiUnit,
            SppMnemonic => unit == PsiUnit,
            TempMnemonic => unit == DegCUnit,
            _ => true
        };

        if (!valid)
        {
            throw new WitsmlParseException(
                $"Mnemonic '{mnemonic}' has unsupported unit " +
                $"'{unit}'. Supported units: GR=gAPI, SPP=psi, " +
                "TEMP=degC.");
        }
    }

    private static double ParseDepth(
        string value,
        string unit,
        WitsmlLogCurveInfo curveInfo,
        int lineNumber)
    {
        string normalized = NormalizeMandatoryValue(
            value, curveInfo, curveInfo.Mnemonic, lineNumber);

        double depth = ParseDouble(normalized, curveInfo.Mnemonic, lineNumber);

        if (depth < 0)
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: depth value {depth} is " +
                "negative.");
        }

        return unit == FeetUnit
            ? depth * FeetToMetres
            : depth;
    }

    private static DateTimeOffset ParseTimestamp(
        string value,
        WitsmlLogCurveInfo curveInfo,
        int lineNumber)
    {
        string normalized = NormalizeMandatoryValue(
            value, curveInfo, DtimMnemonic, lineNumber);

        if (!DateTimeOffset.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset timestamp))
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: DTIM value '{normalized}' " +
                "is not a valid ISO 8601 timestamp.");
        }

        return timestamp.ToUniversalTime();
    }

    private static double ParseMeasurement(
        string value,
        WitsmlLogCurveInfo curveInfo,
        string mnemonic,
        int lineNumber)
    {
        string normalized = NormalizeMandatoryValue(
            value, curveInfo, mnemonic, lineNumber);

        double measurement = ParseDouble(normalized, mnemonic, lineNumber);

        if (mnemonic == GrMnemonic && measurement < 0)
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: GR value {measurement} " +
                "is negative.");
        }

        return measurement;
    }

    private static string NormalizeMandatoryValue(
        string value,
        WitsmlLogCurveInfo curveInfo,
        string mnemonic,
        int lineNumber)
    {
        string trimmed = value.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: required mnemonic " +
                $"'{mnemonic}' has an empty value.");
        }

        if (!string.IsNullOrEmpty(curveInfo.NullValue) &&
            trimmed == curveInfo.NullValue.Trim())
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: required mnemonic " +
                $"'{mnemonic}' uses the null value " +
                $"'{curveInfo.NullValue}'.");
        }

        return trimmed;
    }

    private static double ParseDouble(
        string value,
        string mnemonic,
        int lineNumber)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out double result))
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: {mnemonic} value " +
                $"'{value}' is not a valid number.");
        }

        if (!double.IsFinite(result))
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: {mnemonic} value " +
                $"{result} is not finite.");
        }

        return result;
    }

    private static void EnsureTimestampAdvances(
        DateTimeOffset timestamp,
        DateTimeOffset? previousTimestamp,
        int lineNumber)
    {
        if (previousTimestamp is null)
        {
            return;
        }

        if (timestamp <= previousTimestamp.Value)
        {
            throw new WitsmlParseException(
                $"Data line {lineNumber}: timestamp " +
                $"{timestamp:O} is not later than the previous " +
                $"timestamp {previousTimestamp.Value:O}.");
        }
    }

    private static (DrillingOperation, double) ComputeOperationAndRate(
        double depthMetres,
        double? previousDepthMetres,
        DateTimeOffset timestamp,
        DateTimeOffset? previousTimestamp)
    {
        if (previousDepthMetres is null || previousTimestamp is null)
        {
            return (DrillingOperation.Stationary, 0);
        }

        double depthDelta = depthMetres - previousDepthMetres.Value;
        TimeSpan timeDelta = timestamp - previousTimestamp.Value;
        double hoursElapsed = timeDelta.TotalHours;
        double rate = depthDelta / hoursElapsed;

        DrillingOperation operation = rate > 0
            ? DrillingOperation.DrillingAhead
            : rate < 0
                ? DrillingOperation.TrippingOut
                : DrillingOperation.Stationary;

        return (operation, rate);
    }
}
