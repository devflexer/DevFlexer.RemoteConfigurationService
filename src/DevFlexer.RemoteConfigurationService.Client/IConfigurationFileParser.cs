using System.Collections.Generic;
using System.IO;

namespace DevFlexer.RemoteConfigurationService.Client
{
    public interface IConfigurationFileParser
    {
        IDictionary<string, string> Parse(Stream input);
    }
}
