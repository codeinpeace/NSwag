﻿//-----------------------------------------------------------------------
// <copyright file="SwaggerMiddleware.cs" company="NSwag">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/NSwag/NSwag/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NSwag.CodeGeneration.SwaggerGenerators;
using NSwag.CodeGeneration.SwaggerGenerators.WebApi;

namespace NSwag.AspNetCore
{
    internal class SwaggerMiddleware
    {
        private readonly RequestDelegate _nextDelegate;

        private readonly object _lock = new object();
        private readonly string _path;
        private readonly IEnumerable<Type> _controllerTypes;
        private string _swaggerJson = null;
        private readonly SwaggerOwinSettings _settings;
        private readonly SwaggerJsonSchemaGenerator _schemaGenerator;

        public SwaggerMiddleware(RequestDelegate nextDelegate, string path, IEnumerable<Type> controllerTypes, SwaggerOwinSettings settings, SwaggerJsonSchemaGenerator schemaGenerator)
        {
            _nextDelegate = nextDelegate;
            _path = path;
            _controllerTypes = controllerTypes;
            _settings = settings;
            _schemaGenerator = schemaGenerator;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.HasValue && string.Equals(context.Request.Path.Value.Trim('/'), _path.Trim('/'), StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(GenerateSwagger(context));
            }
            else
                await _nextDelegate(context);
        }

        private string GenerateSwagger(HttpContext context)
        {
            if (_swaggerJson == null)
            {
                lock (_lock)
                {
                    if (_swaggerJson == null)
                    {
                        var generator = new WebApiToSwaggerGenerator(_settings, _schemaGenerator);
                        var service = generator.GenerateForControllers(_controllerTypes);

                        service.Host = context.Request.Host.Value ?? "";
                        service.Schemes.Add(context.Request.Scheme == "http" ? SwaggerSchema.Http : SwaggerSchema.Https);
                        service.BasePath = context.Request.PathBase.Value?.Substring(0, context.Request.PathBase.Value.Length - _settings.MiddlewareBasePath?.Length ?? 0) ?? "";

                        foreach (var processor in _settings.DocumentProcessors)
                            processor.Process(service);

#pragma warning disable 618
                        _settings.PostProcess?.Invoke(service);
#pragma warning restore 618
                        _swaggerJson = service.ToJson();
                    }
                }
            }

            return _swaggerJson;
        }
    }
}