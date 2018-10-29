﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DHaven.Faux.Compiler
{
    /// <summary>
    /// Internal helper to generate code to support the special attributes that
    /// are added to the user's interface.
    /// </summary>
    internal static class AttributeInterpreter
    {
        private static readonly IList<string> ContentHeaders = new List<string>
        {
            "content-type",
            "content-length",
            "content-encoding",
            "content-language",
            "content-location",
            "content-disposition",
            "content-range",
            "content-md5",
            "expires",
            "last-modified",
            "allow"
        };

        internal static void InterpretPathValue(ParameterInfo parameter, IndentBuilder contentBuilder, string prefix = null)
        {
            var pathValue = parameter.GetCustomAttribute<PathValueAttribute>();

            if (pathValue == null)
            {
                return;
            }

            prefix = prefix ?? string.Empty;
            var key = string.IsNullOrEmpty(pathValue.Variable) ? parameter.Name : pathValue.Variable;
            contentBuilder.AppendLine($"仮variables.Add(\"{key}\", {prefix}{parameter.Name});");
        }

        internal static void InterpretRequestHeader(ParameterInfo parameter, Dictionary<string, ParameterInfo> requestHeaders, Dictionary<string, ParameterInfo> contentHeaders)
        {
            var requestHeader = parameter.GetCustomAttribute<RequestHeaderAttribute>();

            if (requestHeader == null)
            {
                return;
            }

            if (ContentHeaders.Contains(requestHeader.Header.ToLowerInvariant()))
            {
                contentHeaders.Add(requestHeader.Header, parameter);
            }
            else
            {
                requestHeaders.Add(requestHeader.Header, parameter);
            }
        }

        internal static void InterpretBodyParameter(ParameterInfo parameter, ref ParameterInfo bodyParam, ref BodyAttribute bodyAttr)
        {
            var attr = parameter.GetCustomAttribute<BodyAttribute>();

            if (attr == null)
            {
                return;
            }

            if (bodyAttr != null)
            {
                throw new WebServiceCompileException("Cannot have more than one body parameter");
            }

            bodyAttr = attr;
            bodyParam = parameter;
        }

        internal static bool CreateContentObjectIfSpecified(BodyAttribute bodyAttr, ParameterInfo bodyParam, IndentBuilder contentBuilder, string prefix = null)
        {
            if (bodyAttr == null || bodyParam == null)
            {
                return false;
            }

            prefix = prefix ?? string.Empty;
            var format = bodyAttr.Format;

            if (format == Format.Auto)
            {
                format = typeof(Stream).IsAssignableFrom(bodyParam.ParameterType) ? Format.Raw : Format.Json;
            }

            // Format.Auto is handled above.  At this point it is always Format.Raw or Format.Json
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (format)
            {
                case Format.Json:
                    contentBuilder.AppendLine($"var 仮content = DHaven.Faux.HttpSupport.ConvertToJson({prefix}{bodyParam.Name});");
                    break;
                case Format.Raw:
                    contentBuilder.AppendLine($"var 仮content = DHaven.Faux.HttpSupport.StreamRawContent({prefix}{bodyParam.Name});");
                    break;
                default:
                    return false;
            }

            return true;
        }

        internal static void ReturnContentObject(BodyAttribute bodyAttr, Type returnType, bool isAsyncCall, IndentBuilder contentBuilder)
        {
            if (bodyAttr == null || returnType == null)
            {
                return;
            }

            var format = bodyAttr.Format;

            if (format == Format.Auto)
            {
                format = typeof(Stream).IsAssignableFrom(returnType) ? Format.Raw : Format.Json;
            }

            // The Format.Auto is handled above, since it will always be Raw or Json at this point
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (format)
            {
                case Format.Json:
                    contentBuilder.AppendLine(isAsyncCall
                        ? $"return await DHaven.Faux.HttpSupport.DiscoveryAwareBase.ConvertToObjectAsync<{CompilerUtils.ToCompilableName(returnType)}>(仮response);"
                        : $"return DHaven.Faux.HttpSupport.DiscoveryAwareBase.ConvertToObject<{CompilerUtils.ToCompilableName(returnType)}>(仮response);");
                    break;
                case Format.Raw:
                    contentBuilder.AppendLine(isAsyncCall
                        ? "return await 仮response.Content.ReadAsStreamAsync();"
                        : "return 仮response.Content.ReadAsStreamAsync().Result;");
                    break;
                default:
                    return;
            }
        }

        internal static void InterpretRequestParameter(ParameterInfo parameter, IndentBuilder contentBuilder, string prefix = null)
        {
            prefix = prefix ?? string.Empty;
            var paramAttribute = parameter.GetCustomAttribute<RequestParameterAttribute>();

            if (paramAttribute == null)
            {
                return;
            }

            var paramName = string.IsNullOrEmpty(paramAttribute.Parameter)
                ? parameter.Name
                : paramAttribute.Parameter;

            contentBuilder.AppendLine($"仮reqParams.Add(\"{paramName}\", {prefix}{parameter.Name}{(parameter.ParameterType.IsClass ? "?" : "")}.ToString());");
        }

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Global
        internal static void InterpretResponseHeaderInParameters(ParameterInfo parameter, bool isAsync, ref Dictionary<string,ParameterInfo> responseHeaders)
        {
            var responseAttribute = parameter.GetCustomAttribute<ResponseHeaderAttribute>();

            if (responseAttribute == null)
            {
                return;
            }

            if (!parameter.IsOut)
            {
                throw new WebServiceCompileException("[ResponseHeaderAttribute] must be used on out parameters or for the return type.");
            }

            if (isAsync)
            {
                throw new WebServiceCompileException(
                    "[ResponseHeaderAttribute] in the parameter list cannot be used with async service calls.");
            }

            responseHeaders.Add(responseAttribute.Header, parameter);
        }

        internal static void ReturnResponseHeader(ResponseHeaderAttribute responseHeaderAttribute, Type returnType, IndentBuilder contentBuilder)
        {
            contentBuilder.AppendLine($"return DHaven.Faux.HttpSupport.DiscoveryAwareBase.GetHeaderValue<{CompilerUtils.ToCompilableName(returnType)}>(仮response, \"{responseHeaderAttribute.Header}\");");
        }
    }
}
