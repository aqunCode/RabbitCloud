﻿using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rabbit.Cloud.Abstractions.Serialization;
using Rabbit.Cloud.ApplicationModels;
using Rabbit.Cloud.Grpc.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rabbit.Cloud.Grpc.ApplicationModels.Internal
{
    public class DefaultServerMethodInvoker : ServerMethodInvoker
    {
        private readonly ILogger<DefaultServerMethodInvoker> _logger;
        private readonly SerializerCacheTable _serializerCacheTable;

        public DefaultServerMethodInvoker(MethodModel serverMethod, IServiceProvider services, ILogger<DefaultServerMethodInvoker> logger) : base(serverMethod, services, logger)
        {
            _serializerCacheTable = services.GetRequiredService<SerializerCacheTable>();
            _logger = logger;
        }

        #region Overrides of ServerMethodInvoker<TRequest,TResponse>

        public override Task<TResponse> UnaryServerMethod<TRequest, TResponse>(TRequest request, ServerCallContext callContext)
        {
            try
            {
                object objRequest = request;
                var args = new List<object>();
                switch (objRequest)
                {
                    case DynamicRequestModel dynamicRequestModel:
                        var method = ServerMethod.MethodInfo;
                        args.AddRange(method.GetParameters().Select(p =>
                        {
                            var data = dynamicRequestModel.Items[p.Name];
                            var serializer = _serializerCacheTable.GetRequiredSerializer(p.ParameterType);
                            var value = serializer.Deserialize(p.ParameterType, data.ToByteArray());

                            if (value == null)
                                throw RpcExceptionUtilities.NotFoundSerializer(p.ParameterType);

                            return value;
                        }).ToArray());
                        break;

                    case EmptyRequestModel _:
                        break;

                    default:
                        args.Add(request);
                        break;
                }
                args.Add(callContext);
                return (Task<TResponse>)MethodInvoker(args.ToArray());
            }
            catch (Exception e)
            {
                callContext.RequestHeaders.Add("exception", e.Message);
                _logger.LogError(e, $"invoke method {ServerMethod.MethodInfo.DeclaringType.FullName}.{ServerMethod.MethodInfo.Name} error.");
                throw;
            }
        }

        public override Task<TResponse> ClientStreamingServerMethod<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext callContext)
        {
            throw new NotImplementedException();
        }

        public override Task ServerStreamingServerMethod<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext callContext)
        {
            throw new NotImplementedException();
        }

        public override Task DuplexStreamingServerMethod<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream,
            ServerCallContext callContext)
        {
            throw new NotImplementedException();
        }

        #endregion Overrides of ServerMethodInvoker<TRequest,TResponse>
    }
}