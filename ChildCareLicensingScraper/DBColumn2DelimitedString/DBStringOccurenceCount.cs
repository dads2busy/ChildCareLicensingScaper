//------------------------------------------------------------------------------
// <copyright file="CSSqlFunction.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.Text.RegularExpressions;

public partial class UserDefinedFunctions
{
    [Microsoft.SqlServer.Server.SqlFunction]
    public static int DBStringOccurenceCount([SqlFacet(MaxSize = -1)]string haystack, string needle)
    {
        if (haystack == null)
            haystack = "";
        int needleCount = 0;
        int currentIndex = 0;
        while (currentIndex != -1)
        {
            currentIndex = haystack.IndexOf(needle, currentIndex + 1);
            if (currentIndex > 0)
                needleCount++;
        }
        return needleCount;
    }

    [Microsoft.SqlServer.Server.SqlFunction]
    public static int RegExMatchCount([SqlFacet(MaxSize = -1)]string inputValue, string regexPattern)
    {
        // Any nulls - we can't match, return false
        if (string.IsNullOrEmpty(inputValue) || string.IsNullOrEmpty(regexPattern))
            return 0;

        Regex r1 = new Regex(regexPattern.TrimEnd(null));
        return r1.Matches(inputValue).Count;
        //return r1.Match(inputValue.TrimEnd(null)).Success;
    }

    [Microsoft.SqlServer.Server.SqlFunction]
    public static string RegExMatchDelimited([SqlFacet(MaxSize = -1)]string inputValue, string regexPattern)
    {
        // Any nulls - we can't match, return false
        if (string.IsNullOrEmpty(inputValue) || string.IsNullOrEmpty(regexPattern))
            return false.ToString();

        Regex r1 = new Regex(regexPattern.TrimEnd(null));
        string matchString = "";
        int currentIndex = 0;       
        while (r1.Match(inputValue, currentIndex).Success)
        {
            var match = r1.Match(inputValue, currentIndex);
            matchString += match.Value + ", ";
            currentIndex = match.Index + match.Value.Length;
        }
        if (matchString != "")
            return matchString.Substring(0, matchString.Length - 2);
        else
            return "";
    }

    [Microsoft.SqlServer.Server.SqlFunction]
    [return: SqlFacet(MaxSize = -1)]
    public static string ReturnPage([SqlFacet(MaxSize = -1)]string haystack)
    {
        return haystack;
    }
}