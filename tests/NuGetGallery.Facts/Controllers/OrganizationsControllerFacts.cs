// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Xunit;

namespace NuGetGallery
{
    public class OrganizationsControllerFacts
        : AccountsControllerFacts<OrganizationsController, Organization, OrganizationAccountViewModel>
    {
        public static User GetAdminAsCurrentUser(OrganizationsController controller, Organization account)
        {
            if (controller.GetCurrentUser() == null)
            {
                var admin = CreateAdmin(account);
                controller.SetCurrentUser(admin);
            }
            return controller.GetCurrentUser();
        }

        public static User CreateAdmin(Organization account)
        {
            if (!account.Members.Any())
            {
                var currentUser = new User("currentUser") { Key = 1234 };
                var membership = new Membership()
                {
                    Organization = account,
                    Member = currentUser,
                    IsAdmin = true
                };

                currentUser.Organizations.Add(membership);
                account.Members.Add(membership);
            }
            return account.Members.Single().Member;
        }

        public class TheAccountAction : TheAccountBaseAction
        {
            protected override ActionResult InvokeAccount(OrganizationsController controller)
            {
                var accountName = GetAccount(controller).Username;
                return controller.ManageOrganization(accountName);
            }

            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return GetAdminAsCurrentUser(controller, GetAccount(controller));
            }
        }

        public class TheChangeEmailSubscriptionAction : TheChangeEmailSubscriptionBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return GetAdminAsCurrentUser(controller, GetAccount(controller));
            }

            public override Task UpdatesEmailPreferences(bool emailAllowed, bool notifyPackagePushed)
                => base.UpdatesEmailPreferences(emailAllowed, notifyPackagePushed);
            
            public override Task DisplaysMessageOnUpdate()
                => base.DisplaysMessageOnUpdate();

            [Fact]
            public async Task ReturnsForbiddenIfCurrentUserIsCollaborator()
            {
                // Arrange (override defaults)
                var controller = GetController();
                var currentUser = GetCurrentUser(controller);
                currentUser.Organizations.Single().IsAdmin = false;

                // Act
                var result = await InvokeChangeEmailSubscription(controller) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task ReturnsForbiddenIfCurrentUserIsNotMember()
            {
                // Arrange (override defaults)
                var controller = GetController();
                var currentUser = GetCurrentUser(controller);

                // switch account membership to different user
                currentUser.Organizations.Single().Member = new User("differentUser") { Key = 4321 };
                currentUser.Organizations.Clear();

                // Act
                var result = await InvokeChangeEmailSubscription(controller) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheConfirmationRequiredAction : TheConfirmationRequiredBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return GetAdminAsCurrentUser(controller, GetAccount(controller));
            }

            public override void SendsNewAccountEmailWhenPosted()
                => base.SendsNewAccountEmailWhenPosted();
        }
    }
}
