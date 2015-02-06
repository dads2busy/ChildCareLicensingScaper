using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChildCareLicensingScraper
{
    class CleanHTML
    {
        static string Clean(string sourceHtml)
        { 
            return 
             Regex.Replace(sourceHtml, "(<(?<t>script|object|applet|embbed|frameset|iframe|form|textarea)(\\s+.*?)?>.*?</\\k<t>>)" + "|(<(script|object|applet|embbed|frameset|iframe|form|input|button|textarea)(\\s+.*?)?/?>)" + "|((?<=<\\w+)((?:\\s+)((?:on\\w+=((\"[^\"]*\")|('[^']*')|(.*?)))|(?<a>(?!on)\\w+=((\"[^\"]*\")|('[^']*')|(.*?)))))*(?=/?>))",
                match =>
                {
                    if (!match.Groups["a"].Success)
                    {
                        return string.Empty;
                    }
        
                    var attributesBuilder = new StringBuilder();
        
                    foreach(Capture capture in match.Groups["a"].Captures)
                    {
                        attributesBuilder.Append(' ');
                        attributesBuilder.Append(capture.Value);
                    }
        
                    return attributesBuilder.ToString();
                },
                RegexOptions.IgnoreCase
                    | RegexOptions.Multiline
                    | RegexOptions.ExplicitCapture
                    | RegexOptions.CultureInvariant
                    | RegexOptions.Compiled
                );
        }
    }
}
