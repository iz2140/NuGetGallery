﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    /// <summary>
    /// Context object for checking permissions of an action involving a new package ID.
    /// </summary>
    public class ActionOnNewPackageContext
    {
        public string PackageId { get; }
        public IReservedNamespaceService ReservedNamespaceService { get; }

        public ActionOnNewPackageContext(string packageId, IReservedNamespaceService reservedNamespaceService)
        {
            PackageId = packageId;
            ReservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
        }
    }

    /// <summary>
    /// An action requiring permissions on <see cref="ReservedNamespace"/>s that can be done on behalf of another <see cref="User"/>.
    /// </summary>
    /// <remarks>
    /// These permissions refer to <see cref="IReadOnlyCollection{ReservedNamespace}"/> and not a single <see cref="ReservedNamespace"/> because multiple namespaces can apply to a single ID.
    /// E.g. "JQuery.Extensions.MyCoolExtension" matches both "JQuery.*" and "JQuery.Extensions.*".
    /// </remarks>
    public class ActionRequiringReservedNamespacePermissions
        : ActionRequiringEntityPermissions<IReadOnlyCollection<ReservedNamespace>>, IActionRequiringEntityPermissions<ActionOnNewPackageContext>
    {
        public PermissionsRequirement ReservedNamespacePermissionsRequirement { get; }

        public ActionRequiringReservedNamespacePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement reservedNamespacePermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement)
        {
            ReservedNamespacePermissionsRequirement = reservedNamespacePermissionsRequirement;
        }

        public PermissionsCheckResult CheckPermissions(User currentUser, User account, ActionOnNewPackageContext newPackageContext)
        {
            return CheckPermissions(currentUser, account, GetReservedNamespaces(newPackageContext));
        }

        public PermissionsCheckResult CheckPermissions(IPrincipal currentPrincipal, User account, ActionOnNewPackageContext newPackageContext)
        {
            return CheckPermissions(currentPrincipal, account, GetReservedNamespaces(newPackageContext));
        }

        protected override PermissionsCheckResult CheckPermissionsForEntity(User account, IReadOnlyCollection<ReservedNamespace> reservedNamespaces)
        {
            if (!reservedNamespaces.Any())
            {
                return PermissionsCheckResult.Allowed;
            }

            // Permissions on only a single namespace are required to perform the action.
            return reservedNamespaces.Any(rn => PermissionsHelpers.IsRequirementSatisfied(ReservedNamespacePermissionsRequirement, account, rn)) ?
                PermissionsCheckResult.Allowed : PermissionsCheckResult.ReservedNamespaceFailure;
        }

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, ActionOnNewPackageContext newPackageContext, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return TryGetAccountsIsAllowedOnBehalfOf(currentUser, GetReservedNamespaces(newPackageContext), out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(IReadOnlyCollection<ReservedNamespace> reservedNamespaces)
        {
            return reservedNamespaces.Any() ? reservedNamespaces.SelectMany(rn => rn.Owners) : Enumerable.Empty<User>();
        }

        private IReadOnlyCollection<ReservedNamespace> GetReservedNamespaces(ActionOnNewPackageContext newPackageContext)
        {
            return newPackageContext.ReservedNamespaceService.GetReservedNamespacesForId(newPackageContext.PackageId);
        }
    }
}