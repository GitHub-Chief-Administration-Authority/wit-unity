﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Meta.Conduit
{
    /// <summary>
    /// This class is responsible for scanning assemblies for relevant Conduit data.
    /// </summary>
    internal class AssemblyWalker : IAssemblyWalker
    {
        /// <summary>
        /// Validates that parameters are compatible. 
        /// </summary>
        private readonly IParameterValidator parameterValidator;

        public AssemblyWalker(IParameterValidator parameterValidator)
        {
            this.parameterValidator = parameterValidator;
        }

        /// <summary>
        /// Returns a list of all assemblies that should be processed.
        /// This currently selects assemblies that are marked with the <see cref="ConduitAssemblyAttribute"/> attribute.
        /// </summary>
        /// <returns>The list of assemblies.</returns>
        public IEnumerable<IConduitAssembly> GetTargetAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.IsDefined(typeof(ConduitAssemblyAttribute)));

            return assemblies.Select(assembly => new ConduitAssembly(assembly, parameterValidator)).ToList();
        }
    }
}