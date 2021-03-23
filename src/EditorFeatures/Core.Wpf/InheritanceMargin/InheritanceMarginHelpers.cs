﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.InheritanceMargin;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.InheritanceMargin
{
    internal static class InheritanceMarginHelpers
    {
        private const InheritanceRelationship ImplementingOverriding =
            InheritanceRelationship.Implementing | InheritanceRelationship.Overriding;

        private const InheritanceRelationship ImplementingOverridden =
            InheritanceRelationship.Implementing | InheritanceRelationship.Overridden;

        /// <summary>
        /// Decide which moniker should be shown.
        /// </summary>
        public static ImageMoniker GetMoniker(InheritanceRelationship inheritanceRelationship)
        {
            //  If there are multiple targets and we have the corresponding compound image, use it
            if (inheritanceRelationship.HasFlag(ImplementingOverriding))
            {
                return KnownMonikers.Overriding;
            }

            if (inheritanceRelationship.HasFlag(ImplementingOverridden))
            {
                return KnownMonikers.Overridden;
            }

            // Otherwise, show the image based on this preference
            if (inheritanceRelationship.HasFlag(InheritanceRelationship.Implemented))
            {
                return KnownMonikers.Implemented;
            }

            if (inheritanceRelationship.HasFlag(InheritanceRelationship.Implementing))
            {
                return KnownMonikers.Implementing;
            }

            if (inheritanceRelationship.HasFlag(InheritanceRelationship.Overridden))
            {
                return KnownMonikers.Overridden;
            }

            if (inheritanceRelationship.HasFlag(InheritanceRelationship.Overriding))
            {
                return KnownMonikers.Overriding;
            }

            // The relationship is None. Don't know what image should be shown, throws
            throw ExceptionUtilities.UnexpectedValue(inheritanceRelationship);
        }
    }
}
