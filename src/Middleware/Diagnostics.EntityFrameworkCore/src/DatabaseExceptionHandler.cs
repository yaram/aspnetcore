// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
{
    public class DatabaseErrorHandler : IDeveloperPageExceptionFilter
    {
        private readonly ILogger _logger;
        private readonly DatabaseErrorPageOptions _options;

        public DatabaseErrorHandler(ILogger<DatabaseErrorHandler> logger, IOptions<DatabaseErrorPageOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task HandleExceptionAsync(ErrorContext errorContext, Func<ErrorContext, Task> next)
        {
            if (errorContext.Exception is DbException)
            {
                try
                {
                    // Look for DbContext classes registered in the service provider
                    // TODO: Decouple
                    var registeredContexts = errorContext.HttpContext.RequestServices.GetServices<DbContextOptions>()
                        .Select(o => o.ContextType);

                    if (registeredContexts.Any())
                    {
                        var contextDetails = new List<DatabaseContextDetails>();

                        foreach (var registeredContext in registeredContexts)
                        {
                            // TODO: Decouple
                            var context = (DbContext)errorContext.HttpContext.RequestServices.GetService(registeredContext);
                            // TODO: Decouple
                            var relationalDatabaseCreator = context.GetService<IDatabaseCreator>() as IRelationalDatabaseCreator;
                            if (relationalDatabaseCreator == null)
                            {
                                _logger.NotRelationalDatabase();
                            }
                            else
                            {
                                var databaseExists = await relationalDatabaseCreator.ExistsAsync();

                                if (databaseExists)
                                {
                                    databaseExists = await relationalDatabaseCreator.HasTablesAsync();
                                }

                                // TODO: Decouple
                                var migrationsAssembly = context.GetService<IMigrationsAssembly>();
                                // TODO: Decouple
                                var modelDiffer = context.GetService<IMigrationsModelDiffer>();

                                var snapshotModel = migrationsAssembly.ModelSnapshot?.Model;
                                // TODO: Decouple
                                if (snapshotModel is IConventionModel conventionModel)
                                {
                                    // TODO: Decouple
                                    var conventionSet = context.GetService<IConventionSetBuilder>().CreateConventionSet();

                                    // TODO: Decouple
                                    var typeMappingConvention = conventionSet.ModelFinalizingConventions.OfType<TypeMappingConvention>().FirstOrDefault();
                                    if (typeMappingConvention != null)
                                    {
                                        typeMappingConvention.ProcessModelFinalizing(conventionModel.Builder, null);
                                    }

                                    // TODO: Decouple
                                    var relationalModelConvention = conventionSet.ModelFinalizedConventions.OfType<RelationalModelConvention>().FirstOrDefault();
                                    if (relationalModelConvention != null)
                                    {
                                        snapshotModel = relationalModelConvention.ProcessModelFinalized(conventionModel);
                                    }
                                }

                                // TODO: Decouple
                                if (snapshotModel is IMutableModel mutableModel)
                                {
                                    snapshotModel = mutableModel.FinalizeModel();
                                }

                                // HasDifferences will return true if there is no model snapshot, but if there is an existing database
                                // and no model snapshot then we don't want to show the error page since they are most likely targeting
                                // and existing database and have just misconfigured their model

                                contextDetails.Add(new DatabaseContextDetails
                                {
                                    Type = registeredContext,
                                    DatabaseExists = databaseExists,
                                    PendingModelChanges = (!databaseExists || migrationsAssembly.ModelSnapshot != null)
                                        && modelDiffer.HasDifferences(snapshotModel?.GetRelationalModel(), context.Model.GetRelationalModel()),
                                    PendingMigrations = databaseExists
                                        ? await context.Database.GetPendingMigrationsAsync()
                                        : context.Database.GetMigrations()
                                });
                            }
                        }

                        if (contextDetails.Count > 0)
                        {
                            var page = new DatabaseErrorPage
                            {
                                Model = new DatabaseErrorPageModel(errorContext.Exception, contextDetails, _options)
                            };

                            await page.ExecuteAsync(errorContext.HttpContext);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.DatabaseErrorPageMiddlewareException(e);
                }

                await next(errorContext);
            }
            else
            {
                await next(errorContext);
            }
        }
    }
}
