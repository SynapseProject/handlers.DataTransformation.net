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
    bool _isDryRun = false;

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

        _isDryRun = startInfo.IsDryRun;

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

        if( _parms.HasTransformations )
        {
            if( _parms.HasXslTransformations )
                foreach( Transformation t in _parms.XslTransformations )
                {
                    OnProgress( "TransformYamlJson", $"Beginning XslTransformation: {t}" );
                    string xform = Transform( t.Xslt, data, t.PreserveOutputAsIs );
                    Dictionary<object, object> patch = scutils.YamlHelpers.Deserialize( xform );

                    scutils.YamlHelpers.Merge( ref result, patch );
                }
            if( _parms.HasRegexQueries )
                foreach( RegexQuery r in _parms.RegexQueries )
                {
                    OnProgress( "TransformYamlJson", $"Beginning RegexQuery: {r}" );
                    string xform = ""; // --> RegexHelpers.Match(data, r.Pattern, r.Options, r.Timeout);
                    Dictionary<object, object> patch = scutils.YamlHelpers.Deserialize( xform );

                    scutils.YamlHelpers.Merge( ref result, patch );
                }
            //if( _parms.HasJsonQueries )
            //foreach( JsonQuery j in _parms.JsonQueries )
            //{
            //    OnProgress( "TransformYamlJson", $"Beginning RegexQuery: {j}" );
            //    string xform = JsonHelpers.Select( data, j.Expression );
            //    Dictionary<object, object> patch = scutils.YamlHelpers.Deserialize( xform );

            //    scutils.YamlHelpers.Merge( ref result, patch );
            //}
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
            OnProgress( "TransformYamlJson->HasConvert", $"Beginning ConvertToFormat: {_config.ToString()}" );
            string buf = scutils.YamlHelpers.Serialize( result, serializeAsJson: _config.InputTypeIsJson );
            buf = WrapperUtility.ConvertToFormat( _config.InputType, buf, _config.OutputType );
            OnProgress( "TransformYamlJson->HasConvert", $"Completed ConvertToFormat: {_config.ToString()}" );

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

        if( _parms.HasTransformations )
        {
            if( _parms.HasXslTransformations )
                foreach( Transformation t in _parms.XslTransformations )
                {
                    OnProgress( "TransformXml", $"Beginning XslTransformation: {t}" );
                    string xform = Transform( t.Xslt, data, t.PreserveOutputAsIs );
                    XmlDocument patch = scutils.XmlHelpers.Deserialize<XmlDocument>( xform );

                    scutils.XmlHelpers.Merge( ref result, patch );
                }
        }
        else
        {
            if( isDoc )
                result = (XmlDocument)_parms.Data;
            else
                result = scutils.XmlHelpers.Deserialize<XmlDocument>( data );
        }

        if( _config.HasConvert )
        {
            OnProgress( "TransformXml->HasConvert", $"Beginning ConvertToFormat: {_config.ToString()}" );
            string buf = scutils.XmlHelpers.Serialize<XmlDocument>( result );
            buf = WrapperUtility.ConvertToFormat( _config.InputType, buf, _config.OutputType );
            OnProgress( "TransformXml->HasConvert", $"Completed ConvertToFormat: {_config.ToString()}" );

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

    string Transform(string xslt, string data, bool preserveOutputAsIs)
    {
        if( !string.IsNullOrWhiteSpace( xslt ) )
        {
            OnProgress( "Transform", "Beginning Transform" );
            data = WrapperUtility.Transform( _config.InputType, data, xslt, preserveOutputAsIs );
            OnProgress( "Transform", "Completed Transform" );
        }

        return data;
    }

    public override object GetConfigInstance()
    {
        return new TransformConfig()
        {
            InputType = FormatType.Json,
            OutputType = FormatType.Yaml
        };
    }

    public override object GetParametersInstance()
    {
        return new TransformParameters()
        {
            Data = "{ \"Valid\": \"Data\" }",
            XslTransformations = new List<Transformation>()
            {
                new Transformation() { Xslt = "<xsl:stylesheet ... >" },
                new Transformation() { Xslt = "<xsl:stylesheet ... >", PreserveOutputAsIs = false }
            },
            RegexQueries = new List<RegexQuery>()
            {
                new RegexQuery() { Pattern = @"^\w+[\w-\.]*\@\w+((-\w+)|(\w*))\.[a-z]{2,3}$", ExecuteLineByLine = true, PreserveOutputAsIs = true }
            }
        };
    }
}


public class TransformParameters
{
    public object Data { get; set; }
    [YamlIgnore]
    internal bool HasData { get { return Data != null; } }

    [YamlIgnore]
    internal bool HasTransformations { get { return HasXslTransformations || HasRegexQueries; } } // --> || HasJsonQueries


    public List<Transformation> XslTransformations { get; set; }
    [YamlIgnore]
    internal bool HasXslTransformations { get { return XslTransformations?.Count > 0; } }

    public List<RegexQuery> RegexQueries { get; set; }
    [YamlIgnore]
    internal bool HasRegexQueries { get { return RegexQueries?.Count > 0; } }
}

public class Transformation
{
    public string Xslt { get; set; }
    [YamlIgnore]
    internal bool HasXslt { get { return !string.IsNullOrWhiteSpace( Xslt ); } }

    public bool PreserveOutputAsIs { get; set; } = true;

    public override string ToString()
    {
        return $"Xslt: {Xslt}, PreserveOutputAsIs: {PreserveOutputAsIs}";
    }
}

public class RegexQuery
{
    public string Pattern { get; set; }
    [YamlIgnore]
    internal bool HasPattern { get { return !string.IsNullOrWhiteSpace( Pattern ); } }

    //regexoptions = set default here too
    //timeout = set a default of "infiinte"

    public bool ExecuteLineByLine { get; set; } = true;

    public bool PreserveOutputAsIs { get; set; } = true;

    public override string ToString()
    {
        return $"Expression: {Pattern}, ExecuteLineByLine: {ExecuteLineByLine}, PreserveOutputAsIs: {PreserveOutputAsIs}";
    }
}