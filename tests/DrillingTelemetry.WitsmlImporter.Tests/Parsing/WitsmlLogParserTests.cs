using System.Text;
using DrillingTelemetry.WitsmlImporter.Parsing;

namespace DrillingTelemetry.WitsmlImporter.Tests.Parsing;

/// <summary>
/// Tests the WITSML log parser's structural and metadata extraction
/// behaviour, including interoperability with the official Energistics
/// 1.4.1.1 example.
/// </summary>
public sealed class WitsmlLogParserTests
{
    private const string OfficialExampleFileName =
        "Fixtures/log_no_xsl.xml";

    /// <summary>
    /// Verifies that the parser correctly reads the official Energistics
    /// WITSML 1.4.1.1 log example, extracting metadata, mnemonics, units
    /// and data rows.
    /// </summary>
    [Fact]
    public void Parse_OfficialExample_ExtractsStructure()
    {
        // Arrange
        string xml = ReadOfficialExample();
        var parser = new WitsmlLogParser();

        // Act
        WitsmlLog log = parser.Parse(CreateStream(xml));

        // Assert
        Assert.Equal("W-12", log.UidWell);
        Assert.Equal("6507/7-A-42", log.NameWell);
        Assert.Equal("B-01", log.UidWellbore);
        Assert.Equal("A-42", log.NameWellbore);
        Assert.Equal("f34a", log.Uid);
        Assert.Equal("L001", log.Name);
        Assert.Equal("measured depth", log.IndexType);
        Assert.Equal("Mdepth", log.IndexCurve);

        Assert.Equal(21, log.Mnemonics.Count);
        Assert.Equal(21, log.Units.Count);
        Assert.Equal(11, log.DataRows.Count);

        Assert.Equal("Mdepth", log.Mnemonics[0]);
        Assert.Equal("m", log.Units[0]);
        Assert.Equal("ECD", log.Mnemonics[20]);
        Assert.Equal("g/cm3", log.Units[20]);

        // The official example has an empty unit for DXC (position 19).
        Assert.Equal("DXC", log.Mnemonics[19]);
        Assert.Equal(string.Empty, log.Units[19]);
    }

    /// <summary>
    /// Verifies that the parser reads the mnemonic list in the declared
    /// order, independent of the logCurveInfo element order.
    /// </summary>
    [Fact]
    public void Parse_MnemonicListOrder_DiffersFromCurveInfoOrder()
    {
        // Arrange
        string xml = BuildLogXml(
            mnemonicList: "GR,DEPT,TEMP,SPP,DTIM",
            unitList: "gAPI,m,degC,psi,datetime",
            curveInfoOrder: ["DEPT", "DTIM", "GR", "SPP", "TEMP"],
            dataLines:
            [
                "72.5,1000.0,105.0,8200,2024-06-15T08:00:00Z"
            ]);

        var parser = new WitsmlLogParser();

        // Act
        WitsmlLog log = parser.Parse(CreateStream(xml));

        // Assert
        Assert.Equal(
            ["GR", "DEPT", "TEMP", "SPP", "DTIM"],
            log.Mnemonics);
        Assert.Equal(
            ["gAPI", "m", "degC", "psi", "datetime"],
            log.Units);
        Assert.Equal(5, log.Curves.Count);
        Assert.Equal("DEPT", log.Curves[0].Mnemonic);
        Assert.Equal("GR", log.Curves[2].Mnemonic);
    }

    /// <summary>
    /// Verifies that an unsupported WITSML version is rejected.
    /// </summary>
    [Fact]
    public void Parse_UnsupportedVersion_Throws()
    {
        // Arrange
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <logs xmlns="http://www.witsml.org/schemas/1series" version="2.0">
              <log uidWell="W" uidWellbore="WB" uid="L">
                <nameWell>Well</nameWell>
                <nameWellbore>Wellbore</nameWellbore>
                <indexType>measured depth</indexType>
                <indexCurve>DEPT</indexCurve>
              </log>
            </logs>
            """;

        var parser = new WitsmlLogParser();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => parser.Parse(CreateStream(xml)));

        Assert.Contains("1.4.1.1", exception.Message);
    }

    /// <summary>
    /// Verifies that an unsupported index type is rejected.
    /// </summary>
    [Fact]
    public void Parse_UnsupportedIndexType_Throws()
    {
        // Arrange
        string xml = BuildLogXml(
            indexType: "date time",
            mnemonicList: "DEPT,DTIM,GR,SPP,TEMP",
            unitList: "m,datetime,gAPI,psi,degC",
            dataLines:
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0"
            ]);

        var parser = new WitsmlLogParser();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => parser.Parse(CreateStream(xml)));

        Assert.Contains("measured depth", exception.Message);
    }

    /// <summary>
    /// Verifies that a data line whose field count does not match the
    /// mnemonic list is rejected with the line number.
    /// </summary>
    [Fact]
    public void Parse_FieldCountMismatch_ThrowsWithLineNumber()
    {
        // Arrange
        string xml = BuildLogXml(
            mnemonicList: "DEPT,DTIM,GR,SPP,TEMP",
            unitList: "m,datetime,gAPI,psi,degC",
            dataLines:
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200",
                "1000.5,2024-06-15T08:01:00Z,74.1,8210,105.4"
            ]);

        var parser = new WitsmlLogParser();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => parser.Parse(CreateStream(xml)));

        Assert.Contains("Data line 1", exception.Message);
    }

    /// <summary>
    /// Verifies that a unit list whose column count does not match the
    /// mnemonic list is rejected with a clear message.
    /// </summary>
    [Fact]
    public void Parse_UnitListCountMismatch_Throws()
    {
        // Arrange
        string xml = BuildLogXml(
            mnemonicList: "DEPT,DTIM,GR,SPP,TEMP",
            unitList: "m,datetime,gAPI,psi",
            dataLines:
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0"
            ]);

        var parser = new WitsmlLogParser();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => parser.Parse(CreateStream(xml)));

        Assert.Contains("unit list", exception.Message);
        Assert.Contains("4", exception.Message);
        Assert.Contains("5", exception.Message);
    }

    /// <summary>
    /// Verifies that invalid XML is surfaced as a WITSML parse exception
    /// without being masked.
    /// </summary>
    [Fact]
    public void Parse_InvalidXml_Throws()
    {
        // Arrange
        string xml = "<?xml version=\"1.0\"?><logs><log>";

        var parser = new WitsmlLogParser();

        // Act & Assert
        Assert.Throws<WitsmlParseException>(
            () => parser.Parse(CreateStream(xml)));
    }

    /// <summary>
    /// Verifies that a document without a log element is rejected.
    /// </summary>
    [Fact]
    public void Parse_NoLogElement_Throws()
    {
        // Arrange
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <logs xmlns="http://www.witsml.org/schemas/1series" version="1.4.1.1">
            </logs>
            """;

        var parser = new WitsmlLogParser();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => parser.Parse(CreateStream(xml)));

        Assert.Contains("No log element", exception.Message);
    }

    /// <summary>
    /// Verifies that a document containing more than one log element is
    /// rejected, since the importer supports only one log per document.
    /// </summary>
    [Fact]
    public void Parse_MultipleLogElements_Throws()
    {
        // Arrange
        string singleLog = BuildLogXml(
            mnemonicList: "DEPT,DTIM,GR,SPP,TEMP",
            unitList: "m,datetime,gAPI,psi,degC",
            dataLines:
            [
                "1000.0,2024-06-15T08:00:00Z,72.5,8200,105.0"
            ]);

        // Duplicate the inner log element to produce two logs.
        string xml = singleLog.Replace(
            "  </log>\n</logs>",
            "  </log>\n" +
            "  <log uidWell=\"W2\" uidWellbore=\"WB2\" uid=\"L2\">\n" +
            "    <nameWell>Well 2</nameWell>\n" +
            "    <nameWellbore>Wellbore 2</nameWellbore>\n" +
            "    <indexType>measured depth</indexType>\n" +
            "    <indexCurve>DEPT</indexCurve>\n" +
            "  </log>\n</logs>");

        var parser = new WitsmlLogParser();

        // Act & Assert
        WitsmlParseException exception = Assert.Throws<WitsmlParseException>(
            () => parser.Parse(CreateStream(xml)));

        Assert.Contains("one log", exception.Message);
    }

    private static string ReadOfficialExample()
    {
        string basePath = AppContext.BaseDirectory;
        string fullPath = Path.Combine(basePath, OfficialExampleFileName);

        return File.ReadAllText(fullPath);
    }

    private static Stream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static string BuildLogXml(
        string mnemonicList,
        string unitList,
        string[] dataLines,
        string? indexType = null,
        string? indexCurve = null,
        string[]? curveInfoOrder = null)
    {
        string[] mnemonics = curveInfoOrder ??
            ["DEPT", "DTIM", "GR", "SPP", "TEMP"];

        string curveInfo = string.Join(
            Environment.NewLine,
            mnemonics.Select(BuildCurveInfo));

        string dataElements = string.Join(
            Environment.NewLine,
            dataLines.Select(d => $"      <data>{d}</data>"));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <logs xmlns="http://www.witsml.org/schemas/1series" version="1.4.1.1">
              <log uidWell="ARCHER-A-07" uidWellbore="ARCHER-A-07-MAIN" uid="RT-DRILLING-LOG-001">
                <nameWell>Archer A-07</nameWell>
                <nameWellbore>A-07 Main</nameWellbore>
                <name>Real-time drilling log</name>
                <indexType>{indexType ?? "measured depth"}</indexType>
                <indexCurve>{indexCurve ?? "DEPT"}</indexCurve>
            {curveInfo}
                <logData>
                  <mnemonicList>{mnemonicList}</mnemonicList>
                  <unitList>{unitList}</unitList>
            {dataElements}
                </logData>
              </log>
            </logs>
            """;
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
