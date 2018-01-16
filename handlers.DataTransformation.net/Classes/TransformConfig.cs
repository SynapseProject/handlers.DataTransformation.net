using System;

using YamlDotNet.Serialization;

using Zephyr.DataTransformation;


public class TransformConfig
{
    public FormatType InputType { get; set; } = FormatType.Json;
    public FormatType OutputType { get; set; } = FormatType.None;
    [YamlIgnore]
    internal bool HasConvert { get { return OutputType != FormatType.None && InputType != OutputType; } }
    [YamlIgnore]
    internal bool InputTypeIsJson { get { return InputType == FormatType.Json; } }
    [YamlIgnore]
    internal bool InputTypeIsXml { get { return InputType == FormatType.Xml; } }

    public override string ToString()
    {
        return $"InputType: {InputType}, OutputType: {OutputType}, HasConvert: {HasConvert}";
    }
}