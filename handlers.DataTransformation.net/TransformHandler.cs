using System;
using System.Collections.Generic;
using System.Xml;

using Synapse.Core;
using scu = Synapse.Core.Utilities;

using Zephyr.DataTransformation;

using YamlDotNet.Serialization;

public class TransformHandler : HandlerRuntimeBase
{
    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        Exception exception = null;
        ExecuteResult result = new ExecuteResult() { Status = StatusType.Success, Message = "Complete" };

        OnProgress( "Execute", "Pre-Transform/Convert" );

        try
        {
            TransformParameters parms = DeserializeOrNew<TransformParameters>( startInfo.Parameters );

            if( parms.Data == null )
                throw new ArgumentNullException( "Data" );

            switch( parms.InputType )
            {
                case FormatType.Json:
                case FormatType.Yaml:
                {
                    bool isDict = parms.Data is Dictionary<object, object>;
                    string data = parms.Data is string ? (string)parms.Data : scu.YamlHelpers.Serialize( parms.Data, serializeAsJson: parms.InputType == FormatType.Json ); ;

                    data = TransformAndConvert( parms, data );

                    result.ExitData = data;
                    if( isDict )
                        result.ExitData = scu.YamlHelpers.Deserialize( data );

                    break;
                }
                case FormatType.Xml:
                {
                    bool isXmlDoc = parms.Data is XmlDocument;
                    string data = parms.Data is string ? (string)parms.Data : scu.XmlHelpers.Serialize<object>( parms.Data );

                    data = TransformAndConvert( parms, data );

                    result.ExitData = data;
                    if( isXmlDoc )
                        result.ExitData = scu.XmlHelpers.Deserialize<XmlDocument>( data );

                    break;
                }
            }
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

    string TransformAndConvert(TransformParameters parms, string data)
    {
        if( parms.HasXslt )
        {
            OnProgress( "TransformAndConvert", "Beginning Transform" );
            data = WrapperUtility.Transform( parms.InputType, data, parms.Xslt );
            OnProgress( "TransformAndConvert", "Completed Transform" );
        }

        if( parms.HasConvert )
        {
            OnProgress( "TransformAndConvert", $"Beginning ConvertToFormat: {parms.InputType}-->{parms.OutputType}" );
            data = WrapperUtility.ConvertToFormat( parms.InputType, data, parms.OutputType );
            OnProgress( "TransformAndConvert", $"Completed ConvertToFormat: {parms.InputType}-->{parms.OutputType}" );
        }

        return data;
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        return new TransformParameters()
        {
            InputType = FormatType.Json,
            OutputType = FormatType.Yaml,
            Data = "{ \"Valid\": Data }",
            Xslt = "<xsl:stylesheet ... >"
        };
    }
}

public class TransformParameters
{
    public FormatType InputType { get; set; } = FormatType.Json;
    public FormatType OutputType { get; set; } = FormatType.None;
    [YamlIgnore]
    internal bool HasConvert { get { return OutputType != FormatType.None && InputType != OutputType; } }

    public object Data { get; set; }

    public string Xslt { get; set; }
    [YamlIgnore]
    internal bool HasXslt { get { return !string.IsNullOrWhiteSpace( Xslt ); } }
}