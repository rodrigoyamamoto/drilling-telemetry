using System.Xml;
using System.Xml.Linq;

namespace DrillingTelemetry.WitsmlImporter.Parsing;

/// <summary>
/// Parses a WITSML 1.4.1.1 document containing a single measured-depth
/// log into an intermediate <see cref="WitsmlLog"/>.
/// </summary>
/// <remarks>
/// This parser supports only the subset required by the importer:
/// WITSML version 1.4.1.1, a single <c>log</c> element,
/// <c>indexType</c> equal to <c>measured depth</c>, one <c>logData</c>
/// block and simple comma-separated data lines without escaped fields.
/// </remarks>
internal sealed class WitsmlLogParser
{
    private static readonly XNamespace WitsmlNamespace =
        XNamespace.Get("http://www.witsml.org/schemas/1series");

    private const string SupportedVersion = "1.4.1.1";

    private const string SupportedIndexType = "measured depth";

    private const string LogElementName = "log";

    private const string LogCurveInfoElementName = "logCurveInfo";

    private const string LogDataElementName = "logData";

    private const string MnemonicListElementName = "mnemonicList";

    private const string UnitListElementName = "unitList";

    private const string DataElementName = "data";

    private const string MnemonicElementName = "mnemonic";

    private const string UnitElementName = "unit";

    private const string NullValueElementName = "nullValue";

    private const string VersionAttributeName = "version";

    private const string UidWellAttributeName = "uidWell";

    private const string UidWellboreAttributeName = "uidWellbore";

    private const string UidAttributeName = "uid";

    private const string NameWellElementName = "nameWell";

    private const string NameWellboreElementName = "nameWellbore";

    private const string NameElementName = "name";

    private const string IndexTypeElementName = "indexType";

    private const string IndexCurveElementName = "indexCurve";

    /// <summary>
    /// Parses a WITSML 1.4.1.1 log from the supplied stream.
    /// </summary>
    /// <param name="stream">
    /// Stream containing a WITSML XML document.
    /// </param>
    /// <returns>
    /// The intermediate log representation ready for mapping.
    /// </returns>
    /// <exception cref="WitsmlParseException">
    /// Thrown when the document is not valid WITSML 1.4.1.1, contains no
    /// log element, or has inconsistent column and data counts.
    /// </exception>
    public WitsmlLog Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument document = LoadDocument(stream);
        XElement root = GetValidatedRoot(document);
        XElement logElement = GetLogElement(root);

        string uidWell = GetRequiredAttribute(
            logElement, UidWellAttributeName);
        string nameWell = GetRequiredElementValue(
            logElement, NameWellElementName);
        string uidWellbore = GetRequiredAttribute(
            logElement, UidWellboreAttributeName);
        string nameWellbore = GetRequiredElementValue(
            logElement, NameWellboreElementName);
        string uid = GetRequiredAttribute(
            logElement, UidAttributeName);
        string name = GetOptionalElementValue(
            logElement, NameElementName);
        string indexType = GetRequiredElementValue(
            logElement, IndexTypeElementName);

        if (indexType != SupportedIndexType)
        {
            throw new WitsmlParseException(
                $"Unsupported index type '{indexType}'. Only " +
                $"'{SupportedIndexType}' is supported.");
        }

        string indexCurve = GetRequiredElementValue(
            logElement, IndexCurveElementName);

        IReadOnlyList<WitsmlLogCurveInfo> curves =
            logElement
                .Elements(WitsmlNamespace + LogCurveInfoElementName)
                .Select(ParseCurveInfo)
                .ToList();

        XElement logData = GetRequiredElement(
            logElement, LogDataElementName);

        string mnemonicListValue = GetRequiredElementValue(
            logData, MnemonicListElementName);

        string[] mnemonics = SplitCsv(mnemonicListValue);

        if (mnemonics.Length == 0)
        {
            throw new WitsmlParseException(
                "The mnemonic list is empty.");
        }

        string unitListValue = GetRequiredElementValue(
            logData, UnitListElementName);

        string[] units = SplitCsv(unitListValue);

        if (units.Length != mnemonics.Length)
        {
            throw new WitsmlParseException(
                $"The mnemonic list has {mnemonics.Length} columns " +
                $"but the unit list has {units.Length} columns.");
        }

        IReadOnlyList<string[]> dataRows = ParseDataRows(
            logData, mnemonics.Length);

        return new WitsmlLog(
            uidWell,
            nameWell,
            uidWellbore,
            nameWellbore,
            uid,
            name,
            indexType,
            indexCurve,
            curves,
            mnemonics,
            units,
            dataRows);
    }

    private static XDocument LoadDocument(Stream stream)
    {
        try
        {
            return XDocument.Load(
                stream,
                LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            throw new WitsmlParseException(
                $"Invalid XML: {exception.Message}",
                exception);
        }
    }

    private static XElement GetValidatedRoot(XDocument document)
    {
        XElement? root = document.Root;

        if (root is null)
        {
            throw new WitsmlParseException(
                "The XML document is empty.");
        }

        if (root.Name.Namespace != WitsmlNamespace)
        {
            throw new WitsmlParseException(
                $"The root element '{root.Name.LocalName}' is not in " +
                $"the WITSML namespace " +
                $"'{WitsmlNamespace.NamespaceName}'.");
        }

        XAttribute? versionAttribute =
            root.Attribute(VersionAttributeName);

        if (versionAttribute is null ||
            versionAttribute.Value != SupportedVersion)
        {
            string foundVersion =
                versionAttribute?.Value ?? "missing";

            throw new WitsmlParseException(
                $"Unsupported WITSML version. Expected " +
                $"'{SupportedVersion}', got '{foundVersion}'.");
        }

        return root;
    }

    private static XElement GetLogElement(XElement root)
    {
        List<XElement> logElements = root
            .Descendants(WitsmlNamespace + LogElementName)
            .ToList();

        if (logElements.Count == 0)
        {
            throw new WitsmlParseException(
                "No log element found in the WITSML document.");
        }

        if (logElements.Count > 1)
        {
            throw new WitsmlParseException(
                $"The document contains {logElements.Count} log " +
                "elements but the importer supports only one log per " +
                "document.");
        }

        return logElements[0];
    }

    private static WitsmlLogCurveInfo ParseCurveInfo(XElement element)
    {
        string mnemonic = GetRequiredElementValue(
            element, MnemonicElementName);
        string unit = GetOptionalElementValue(
            element, UnitElementName);
        string nullValue = GetOptionalElementValue(
            element, NullValueElementName);

        return new WitsmlLogCurveInfo(mnemonic, unit, nullValue);
    }

    private static IReadOnlyList<string[]> ParseDataRows(
        XElement logData,
        int expectedColumnCount)
    {
        List<string[]> rows = new();

        int index = 0;

        foreach (XElement dataElement in logData
                     .Elements(WitsmlNamespace + DataElementName))
        {
            int lineNumber = index + 1;
            string[] fields = SplitCsv(dataElement.Value);

            if (fields.Length != expectedColumnCount)
            {
                throw new WitsmlParseException(
                    $"Data line {lineNumber} has {fields.Length} " +
                    $"fields but the mnemonic list has " +
                    $"{expectedColumnCount} columns.");
            }

            rows.Add(fields);
            index++;
        }

        return rows;
    }

    /// <summary>
    /// Splits a WITSML comma-separated value string, trimming each entry
    /// but preserving empty fields so that unitless curves and missing
    /// values keep their positional alignment.
    /// </summary>
    /// <param name="value">The raw CSV string from a WITSML element.</param>
    /// <returns>An array of trimmed field values.</returns>
    private static string[] SplitCsv(string value)
    {
        return value.Split(
            ',',
            StringSplitOptions.TrimEntries);
    }

    private static XElement GetRequiredElement(
        XElement parent, string localName)
    {
        XElement? element = parent.Element(
            WitsmlNamespace + localName);

        if (element is null)
        {
            throw new WitsmlParseException(
                $"Required element '{localName}' is missing from " +
                $"'{parent.Name.LocalName}'.");
        }

        return element;
    }

    private static string GetRequiredElementValue(
        XElement parent, string localName)
    {
        return GetRequiredElement(parent, localName).Value.Trim();
    }

    private static string GetOptionalElementValue(
        XElement parent, string localName)
    {
        return parent
            .Element(WitsmlNamespace + localName)?
            .Value.Trim() ?? string.Empty;
    }

    private static string GetRequiredAttribute(
        XElement element, string attributeName)
    {
        XAttribute? attribute = element.Attribute(attributeName);

        if (attribute is null || string.IsNullOrWhiteSpace(attribute.Value))
        {
            throw new WitsmlParseException(
                $"Required attribute '{attributeName}' is missing " +
                $"from '{element.Name.LocalName}'.");
        }

        return attribute.Value.Trim();
    }
}
