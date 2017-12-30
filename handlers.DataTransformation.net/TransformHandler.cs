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

            if( !_parms.HasData )
                throw new ArgumentNullException( "Parameters.Data" );

            if( _config.InputTypeIsXml )
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

        bool isDict = _parms.Data is Dictionary<object, object>;
        string data = _parms.Data is string ? (string)_parms.Data : scutils.YamlHelpers.Serialize( _parms.Data, serializeAsJson: _config.InputTypeIsJson );

        if( _parms.HasXslTransformations )
            foreach( string xslt in _parms.XslTransformations )
            {
                OnProgress( "TransformAndConvert", $"Beginning XslTransformation: {xslt}" );
                string xform = Transform( xslt, data );
                Dictionary<object, object> patch = scutils.YamlHelpers.Deserialize( xform );

                scutils.YamlHelpers.Merge( ref result, patch );
            }
        else
        {
            if( isDict )
                result = (Dictionary<object, object>)_parms.Data;
            else
                result = scutils.YamlHelpers.Deserialize( data );
        }

        if( _config.HasConvert )
        {
            OnProgress( "TransformAndConvert", $"Beginning ConvertToFormat: {_config.ToString()}" );
            string buf = scutils.YamlHelpers.Serialize( result, serializeAsJson: _config.InputTypeIsJson );
            buf = WrapperUtility.ConvertToFormat( _config.InputType, buf, _config.OutputType );
            OnProgress( "TransformAndConvert", $"Completed ConvertToFormat: {_config.ToString()}" );

            return buf;
        }
        else
        {
            if( isDict )
                return result;
            else
                return scutils.YamlHelpers.Serialize( result, serializeAsJson: _config.InputTypeIsJson );
        }
    }

    object TransformXml()
    {
        XmlDocument result = new XmlDocument();

        bool isDoc = _parms.Data is XmlDocument;
        string data = _parms.Data is string ? (string)_parms.Data : scutils.XmlHelpers.Serialize<object>( _parms.Data );

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
            
        };
    }

    public override object GetParametersInstance()
    {
        return new TransformParameters()
        {
            Data = "{ \"Valid\": Data }",
            XslTransformations = new List<string>()
            {
                "<xsl:stylesheet ... >",
                "<xsl:stylesheet ... >"
            }
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
    internal bool InputTypeIsJson { get { return InputType == FormatType.Json; } }
    [YamlIgnore]
    internal bool InputTypeIsXml { get { return InputType == FormatType.Xml; } }

    public override string ToString()
    {
        return $"InputType: {InputType}, OutputType: {OutputType}, HasConvert: {HasConvert}";
    }
}

public class TransformParameters
{
    public object Data { get; set; }
    [YamlIgnore]
    internal bool HasData { get { return Data != null; } }

    public List<string> XslTransformations { get; set; }
    [YamlIgnore]
    internal bool HasXslTransformations { get { return XslTransformations?.Count > 0; } }
}