// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
{
    public class DatabaseErrorHandler : IDeveloperPageExceptionFilter
    {
        public Task HandleExceptionAsync(ErrorContext errorContext, Func<ErrorContext, Task> next)
        {
            throw new NotImplementedException();
        }
    }
}
