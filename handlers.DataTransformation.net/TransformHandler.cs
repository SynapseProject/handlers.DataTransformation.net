using System;
using System.Collections.Generic;
using System.Xml;

using Synapse.Core;
using scutils = Synapse.Core.Utilities;

using Zephyr.DataTransformation;

using YamlDotNet.Serialization;

public class TransformHandler : HandlerRuntimeBase
{
    TransformConfig _config = null;
    TransformParameters _parms = null;

    public override IHandlerRuntime Initialize(string config)
    {
        OnProgress( "Initialize", "DeserializeOrNew<TransformConfig>" );
        _config = DeserializeOrNew<TransformConfig>( config );
        OnProgress( "Initialize", _config.ToString() );

        return base.Initialize( config );
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        Exception exception = null;
        ExecuteResult result = new ExecuteResult() { Status = StatusType.Success, Message = "Complete" };

        try
        {
            OnProgress( "Execute", "DeserializeOrNew<TransformParameters>" );
            _parms = DeserializeOrNew<TransformParameters>( startInfo.Parameters );

            if( _config.Data == null )
                throw new ArgumentNullException( "Config.Data" );

            if( _config.InputIsXml )
                result.ExitData = TransformXml();
            else
                result.ExitData = TransformYamlJson();
        }
        catch( Exception ex )
        {
            result.Status = StatusType.Failed;
            result.Message = "An error occurred.";
            result.ExitData = exception = ex;
        }

        OnProgress( "Execute", result.Message, result.Status, sequence: Int32.MaxValue, ex: exception );

        return result;
    }

    object TransformYamlJson()
    {
        Dictionary<object, object> result = new Dictionary<object, object>();

        bool isDict = _config.Data is Dictionary<object, object>;
        string data = _config.Data is string ? (string)_config.Data : scutils.YamlHelpers.Serialize( _config.Data, serializeAsJson: _config.InputIsJson );

        foreach( string xslt in _parms.XslTransformations )
        {
            OnProgress( "TransformAndConvert", $"Beginning XslTransformation: {xslt}" );
            string xform = Transform( xslt, data );
            Dictionary<object, object> patch = scutils.YamlHelpers.Deserialize( xform );

            scutils.YamlHelpers.Merge( ref result, patch );
        }

        if( _config.HasConvert )
        {
            OnProgress( "TransformAndConvert", $"Beginning ConvertToFormat: {_config.ToString()}" );
            string buf = scutils.YamlHelpers.Serialize( result, serializeAsJson: _config.InputIsJson );
            buf = WrapperUtility.ConvertToFormat( _config.InputType, buf, _config.OutputType );
            OnProgress( "TransformAndConvert", $"Completed ConvertToFormat: {_config.ToString()}" );

            return buf;
        }
        else
        {
            if( isDict )
                return result;
            else
                return scutils.YamlHelpers.Serialize( result, serializeAsJson: _config.InputIsJson );
        }
    }

    object TransformXml()
    {
        XmlDocument result = new XmlDocument();

        bool isDoc = _config.Data is XmlDocument;
        string data = _config.Data is string ? (string)_config.Data : scutils.XmlHelpers.Serialize<object>( _config.Data );

        foreach( string xslt in _parms.XslTransformations )
        {
            OnProgress( "TransformAndConvert", $"Beginning XslTransformation: {xslt}" );
            string xform = Transform( xslt, data );
            XmlDocument patch = scutils.XmlHelpers.Deserialize<XmlDocument>( xform );

            scutils.XmlHelpers.Merge( ref result, patch );
        }

        if( _config.HasConvert )
        {
            OnProgress( "TransformAndConvert", $"Beginning ConvertToFormat: {_config.ToString()}" );
            string buf = scutils.XmlHelpers.Serialize<XmlDocument>( result );
            buf = WrapperUtility.ConvertToFormat( _config.InputType, buf, _config.OutputType );
            OnProgress( "TransformAndConvert", $"Completed ConvertToFormat: {_config.ToString()}" );

            return buf;
        }
        else
        {
            if( isDoc )
                return result;
            else
                return scutils.XmlHelpers.Serialize<XmlDocument>( result );
        }
    }

    string Transform(string xslt, string data)
    {
        if( !string.IsNullOrWhiteSpace( xslt ) )
        {
            OnProgress( "TransformAndConvert", "Beginning Transform" );
            data = WrapperUtility.Transform( _config.InputType, data, xslt );
            OnProgress( "TransformAndConvert", "Completed Transform" );
        }

        return data;
    }

    public override object GetConfigInstance()
    {
        return new TransformConfig()
        {
            InputType = FormatType.Json,
            OutputType = FormatType.Yaml,
            Data = "{ \"Valid\": Data }",
        };
    }

    public override object GetParametersInstance()
    {
        return new List<string>()
        {
            "<xsl:stylesheet ... >",
            "<xsl:stylesheet ... >"
        };
    }
}

public class TransformConfig
{
    public FormatType InputType { get; set; } = FormatType.Json;
    public FormatType OutputType { get; set; } = FormatType.None;
    [YamlIgnore]
    internal bool HasConvert { get { return OutputType != FormatType.None && InputType != OutputType; } }
    [YamlIgnore]
    internal bool InputIsJson { get { return InputType == FormatType.Json; } }
    [YamlIgnore]
    internal bool InputIsXml { get { return InputType == FormatType.Xml; } }

    public object Data { get; set; }

    public override string ToString()
    {
        return $"InputType: {InputType}, OutputType: {OutputType}, HasConvert: {HasConvert}";
    }
}

public class TransformParameters
{
    public List<string> XslTransformations { get; set; }
}