﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace NoBruteTesting.Abstracts.Base
{
    /// <summary>
    /// The Base Abstract Class for all Test Cases storing fields and global methods / helper methods used by all tests
    /// </summary>
    public abstract class TestCaseAbstractBase
    {
        /// <summary>
        /// The service provider
        /// </summary>
        protected IServiceCollection provider = new ServiceCollection();
    }
}
