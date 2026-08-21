using System.Text;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.WitsmlImporter.Parsing;

namespace DrillingTelemetry.WitsmlImporter.Tests.Parsing;

/// <summary>
/// Tests the mapping from a parsed WITSML log to telemetry readings,
/// covering unit conversion, null-value handling, drilling-operation
/// derivation and timestamp validation.
/// </summary>
/// <remarks>
/// These tests use a minimal DEPT/GR fixture that represents the
/// laboratory cut (DEPT as index curve, plus DTIM, GR, SPP, TEMP).
/// The official Energistics example
/// (<c>log_no_xsl.xml</c>) is tested separately in
/// <see cref="WitsmlLogParserTests"/> because it does not contain
/// GR, DTIM, SPP or TEMP curves.
/// </remarks>
public sealed class TelemetryReadingMapperTests
{
    private static readonly Guid AcquisitionSessionId =
        Guid.Parse("8f1c2a3e-4b5d-4c6a-9d7e-1a2b3c4d5e6f");

    private const string DeviceId = "WITSML-DEMO-001";

    /// <summary>
    /// Verifies that well, wellbore and acquisition-session metadata are
    /// preserved on every reading.
    /// </summary>
    [Fact]
    public void Map_ValidLog_PreservesWellAndSessionMetadata()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "1000.5,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act
        IReadOnlyList<TelemetryReading> readings = mapper.Map(
            log,
            DeviceId,
            AcquisitionSessionId);

        // Assert
        Assert.Equal(2, readings.Count);

        foreach (TelemetryReading reading in readings)
        {
            Assert.Equal(DeviceId, reading.DeviceId);
            Assert.Equal(AcquisitionSessionId, reading.AcquisitionSessionId);
            Assert.Equal("ARCHER-A-07", reading.WellId);
            Assert.Equal("Archer A-07", reading.WellName);
            Assert.Equal("ARCHER-A-07-MAIN", reading.WellboreId);
            Assert.Equal("A-07 Main", reading.WellboreName);
            Assert.Equal(
                TelemetryAcquisitionMode.HistoricalImport,
                reading.AcquisitionMode);
        }

        Assert.Equal(1, readings[0].SequenceNumber);
        Assert.Equal(2, readings[1].SequenceNumber);
    }

    /// <summary>
    /// Verifies that every reading produced by the WITSML mapper carries
    /// the historical-import acquisition mode, regardless of the data row.
    /// </summary>
    [Fact]
    public void Map_ProducesHistoricalImportAcquisitionMode()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "1000.5,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act
        IReadOnlyList<TelemetryReading> readings = mapper.Map(
            log,
            DeviceId,
            AcquisitionSessionId);

        // Assert
        Assert.All(
            readings,
            reading => Assert.Equal(
                TelemetryAcquisitionMode.HistoricalImport,
                reading.AcquisitionMode));
    }

    /// <summary>
    /// Verifies that curves in a different mnemonic-list order are
    /// correctly located by mnemonic, not by position.
    /// </summary>
    [Fact]
    public void Map_MnemonicListInDifferentOrder_LocatesColumnsCorrectly()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "GR,SPP,TEMP,DEPT,DTIM",
            "gAPI,psi,degC,m,datetime",
            [
                "72.5,8200,105.0,1000.0,2024-06-15T08:00:00Z",
                "74.1,8210,105.4,1000.5,2024-06-15T08:01:00Z"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act
        IReadOnlyList<TelemetryReading> readings = mapper.Map(
            log,
            DeviceId,
            AcquisitionSessionId);

        // Assert
        Assert.Equal(1000.0, readings[0].MeasuredDepthMetres);
        Assert.Equal(72.5, readings[0].GammaRayApi);
        Assert.Equal(8200, readings[0].PressurePsi);
        Assert.Equal(105.0, readings[0].TemperatureCelsius);
        Assert.Equal(
            DateTimeOffset.Parse("2024-06-15T08:00:00Z"),
            readings[0].TimestampUtc);
    }

    /// <summary>
    /// Verifies that DEPT values in feet are converted to metres using
    /// the international foot factor (0.3048), with the unit read from
    /// the <c>unitList</c> element.
    /// </summary>
    [Fact]
    public void Map_DepthInFeet_ConvertsToMetres()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "ft,datetime,gAPI,psi,degC",
            [
                "3280.84,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "3281.84,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act
        IReadOnlyList<TelemetryReading> readings = mapper.Map(
            log,
            DeviceId,
            AcquisitionSessionId);

        // Assert
        Assert.Equal(
            3280.84 * 0.3048,
            readings[0].MeasuredDepthMetres,
            precision: 4);
        Assert.Equal(
            3281.84 * 0.3048,
            readings[1].MeasuredDepthMetres,
            precision: 4);
    }

    /// <summary>
    /// Verifies that an empty value in a required curve rejects the line
    /// with a message containing the line number.
    /// </summary>
    [Fact]
    public void Map_EmptyRequiredField_ThrowsWithLineNumber()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "1000.5,,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => mapper.Map(log, DeviceId, AcquisitionSessionId));

        Assert.Contains("Data line 2", exception.Message);
        Assert.Contains("DTIM", exception.Message);
    }

    /// <summary>
    /// Verifies that a value matching the declared nullValue rejects the
    /// line.
    /// </summary>
    [Fact]
    public void Map_NullValueInRequiredField_ThrowsWithLineNumber()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "-999.25,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => mapper.Map(log, DeviceId, AcquisitionSessionId));

        Assert.Contains("Data line 2", exception.Message);
        Assert.Contains("null value", exception.Message);
    }

    /// <summary>
    /// Verifies that an unsupported unit for a required curve is rejected
    /// before any data row is processed, using the unit from
    /// <c>unitList</c>.
    /// </summary>
    [Fact]
    public void Map_UnsupportedUnit_Throws()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "km,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => mapper.Map(log, DeviceId, AcquisitionSessionId));

        Assert.Contains("km", exception.Message);
    }

    /// <summary>
    /// Verifies that a missing <c>logCurveInfo</c> entry for the
    /// <c>indexCurve</c> is rejected, so the depth <c>nullValue</c>
    /// cannot be silently ignored.
    /// </summary>
    [Fact]
    public void Map_MissingCurveInfoForIndexCurve_Throws()
    {
        // Arrange — build a log where DEPT is the index curve and appears
        // in mnemonicList/unitList, but has no logCurveInfo entry.
        WitsmlLog log = ParseLogWithoutCurveInfo(
            "DEPT",
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => mapper.Map(log, DeviceId, AcquisitionSessionId));

        Assert.Contains("DEPT", exception.Message);
        Assert.Contains("logCurveInfo", exception.Message);
    }

    /// <summary>
    /// Verifies that a mismatch between the number of mnemonics and the
    /// number of units is rejected by the mapper rather than silently
    /// filling missing units with empty strings.
    /// </summary>
    [Fact]
    public void Map_MnemonicAndUnitCountMismatch_Throws()
    {
        // Arrange — construct a WitsmlLog directly with mismatched counts,
        // bypassing the parser which would already reject this.
        WitsmlLog log = new WitsmlLog(
            UidWell: "ARCHER-A-07",
            NameWell: "Archer A-07",
            UidWellbore: "ARCHER-A-07-MAIN",
            NameWellbore: "A-07 Main",
            Uid: "RT-DRILLING-LOG-001",
            Name: "Real-time drilling log",
            IndexType: "measured depth",
            IndexCurve: "DEPT",
            Curves: [],
            Mnemonics: ["DEPT", "DTIM", "GR", "SPP", "TEMP"],
            Units: ["m", "datetime", "gAPI", "psi"],
            DataRows: []);

        var mapper = new TelemetryReadingMapper();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => mapper.Map(log, DeviceId, AcquisitionSessionId));

        Assert.Contains("mnemonic list", exception.Message);
        Assert.Contains("unit list", exception.Message);
    }

    /// <summary>
    /// Verifies that increasing depth with increasing time produces
    /// DrillingAhead and a positive depth-change rate.
    /// </summary>
    [Fact]
    public void Map_IncreasingDepth_ProducesDrillingAheadAndPositiveRate()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "1001.0,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act
        IReadOnlyList<TelemetryReading> readings = mapper.Map(
            log,
            DeviceId,
            AcquisitionSessionId);

        // Assert
        Assert.Equal(DrillingOperation.Stationary, readings[0].DrillingOperation);
        Assert.Equal(0, readings[0].DepthChangeRateMetresPerHour);

        Assert.Equal(
            DrillingOperation.DrillingAhead,
            readings[1].DrillingOperation);
        Assert.True(readings[1].DepthChangeRateMetresPerHour > 0);
        Assert.Equal(
            60.0,
            readings[1].DepthChangeRateMetresPerHour,
            precision: 6);
    }

    /// <summary>
    /// Verifies that decreasing depth with increasing time produces
    /// TrippingOut and a negative depth-change rate.
    /// </summary>
    [Fact]
    public void Map_DecreasingDepthWithIncreasingTime_ProducesTrippingOut()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "999.0,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act
        IReadOnlyList<TelemetryReading> readings = mapper.Map(
            log,
            DeviceId,
            AcquisitionSessionId);

        // Assert
        Assert.Equal(
            DrillingOperation.TrippingOut,
            readings[1].DrillingOperation);
        Assert.True(readings[1].DepthChangeRateMetresPerHour < 0);
        Assert.Equal(
            -60.0,
            readings[1].DepthChangeRateMetresPerHour,
            precision: 6);
    }

    /// <summary>
    /// Verifies that a repeated timestamp is rejected with the line
    /// number.
    /// </summary>
    [Fact]
    public void Map_RepeatedTimestamp_Throws()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0",
                "1000.5,2024-06-15T08:00:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => mapper.Map(log, DeviceId, AcquisitionSessionId));

        Assert.Contains("Data line 2", exception.Message);
        Assert.Contains("not later than", exception.Message);
    }

    /// <summary>
    /// Verifies that a regressive timestamp (earlier than the previous
    /// one) is rejected with the line number. This is distinct from a
    /// repeated timestamp because it tests backward time travel, not
    /// equality.
    /// </summary>
    [Fact]
    public void Map_RegressiveTimestamp_Throws()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:02:00Z,72.5,8200,105.0",
                "1000.5,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => mapper.Map(log, DeviceId, AcquisitionSessionId));

        Assert.Contains("Data line 2", exception.Message);
        Assert.Contains("not later than", exception.Message);
    }

    /// <summary>
    /// Verifies that the first reading uses Stationary operation and a
    /// zero depth-change rate.
    /// </summary>
    [Fact]
    public void Map_FirstReading_IsStationaryWithZeroRate()
    {
        // Arrange
        WitsmlLog log = ParseLog(
            "DEPT,DTIM,GR,SPP,TEMP",
            "m,datetime,gAPI,psi,degC",
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0"
            ]);

        var mapper = new TelemetryReadingMapper();

        // Act
        IReadOnlyList<TelemetryReading> readings = mapper.Map(
            log,
            DeviceId,
            AcquisitionSessionId);

        // Assert
        Assert.Single(readings);
        Assert.Equal(DrillingOperation.Stationary, readings[0].DrillingOperation);
        Assert.Equal(0, readings[0].DepthChangeRateMetresPerHour);
    }

    private static WitsmlLog ParseLog(
        string mnemonicList,
        string unitList,
        string[] dataLines)
    {
        string[] mnemonics = mnemonicList.Split(',');

        string curveInfo = string.Join(
            Environment.NewLine,
            mnemonics.Select(BuildCurveInfo));

        string dataElements = string.Join(
            Environment.NewLine,
            dataLines.Select(d => $"      <data>{d}</data>"));

        string xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <logs xmlns="http://www.witsml.org/schemas/1series" version="1.4.1.1">
              <log uidWell="ARCHER-A-07" uidWellbore="ARCHER-A-07-MAIN" uid="RT-DRILLING-LOG-001">
                <nameWell>Archer A-07</nameWell>
                <nameWellbore>A-07 Main</nameWellbore>
                <name>Real-time drilling log</name>
                <indexType>measured depth</indexType>
                <indexCurve>DEPT</indexCurve>
            {curveInfo}
                <logData>
                  <mnemonicList>{mnemonicList}</mnemonicList>
                  <unitList>{unitList}</unitList>
            {dataElements}
                </logData>
              </log>
            </logs>
            """;

        var parser = new WitsmlLogParser();

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));

        return parser.Parse(stream);
    }

    /// <summary>
    /// Builds a log where <paramref name="excludedMnemonic"/> is present
    /// in <c>mnemonicList</c> and <c>unitList</c> but has no
    /// <c>logCurveInfo</c> entry, to test explicit validation of the
    /// index curve's curve info.
    /// </summary>
    private static WitsmlLog ParseLogWithoutCurveInfo(
        string excludedMnemonic,
        string mnemonicList,
        string unitList,
        string[] dataLines)
    {
        string[] mnemonics = mnemonicList.Split(',')
            .Where(m => m != excludedMnemonic)
            .ToArray();

        string curveInfo = string.Join(
            Environment.NewLine,
            mnemonics.Select(BuildCurveInfo));

        string dataElements = string.Join(
            Environment.NewLine,
            dataLines.Select(d => $"      <data>{d}</data>"));

        string xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <logs xmlns="http://www.witsml.org/schemas/1series" version="1.4.1.1">
              <log uidWell="ARCHER-A-07" uidWellbore="ARCHER-A-07-MAIN" uid="RT-DRILLING-LOG-001">
                <nameWell>Archer A-07</nameWell>
                <nameWellbore>A-07 Main</nameWellbore>
                <name>Real-time drilling log</name>
                <indexType>measured depth</indexType>
                <indexCurve>DEPT</indexCurve>
            {curveInfo}
                <logData>
                  <mnemonicList>{mnemonicList}</mnemonicList>
                  <unitList>{unitList}</unitList>
            {dataElements}
                </logData>
              </log>
            </logs>
            """;

        var parser = new WitsmlLogParser();

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));

        return parser.Parse(stream);
    }

    private static string BuildCurveInfo(string mnemonic)
    {
        string unit = mnemonic switch
        {
            "DEPT" => "m",
            "DTIM" => "datetime",
            "GR" => "gAPI",
            "SPP" => "psi",
            "TEMP" => "degC",
            _ => string.Empty
        };

        string nullValue = mnemonic == "DTIM"
            ? "1900-01-01T00:00:00Z"
            : "-999.25";

        return $"""
                <logCurveInfo>
                  <mnemonic>{mnemonic}</mnemonic>
                  <unit>{unit}</unit>
                  <nullValue>{nullValue}</nullValue>
                </logCurveInfo>
            """;
    }
}
