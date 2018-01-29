// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class AccountsControllerFacts<TAccountsController, TUser, TAccountViewModel>
        where TUser : User
        where TAccountViewModel : AccountViewModel
        where TAccountsController : AccountsController<TUser, TAccountViewModel>
    {
        public abstract class AccountsControllerTestContainer : TestContainer
        {
            private const string AccountEnvironmentKey = "nuget.account";

            protected abstract User GetCurrentUser(TAccountsController controller);

            public TAccountsController GetController()
            {
                return GetController<TAccountsController>();
            }

            protected TUser GetAccount(TAccountsController controller)
            {
                var environment = controller.OwinContext.Environment;
                if (!environment.TryGetValue(AccountEnvironmentKey, out var account) || !(account is TUser))
                {
                    account = Activator.CreateInstance<TUser>();
                    ((TUser)account).Key = 2345;
                    ((TUser)account).Username = "account";
                    ((TUser)account).EmailAddress = "account@example.com";

                    environment[AccountEnvironmentKey] = account;
                }
                return environment[AccountEnvironmentKey] as TUser;
            }
        }

        public abstract class TheAccountBaseAction : AccountsControllerTestContainer
        {
            [Fact]
            public void WillGetCuratedFeedsManagedByTheCurrentUser()
            {
                // Arrange
                var controller = GetController<TAccountsController>();
                var account = GetAccount(controller);
                controller.SetCurrentUser(account);

                // Act
                InvokeAccountInternal(controller);

                // Assert
                GetMock<ICuratedFeedService>()
                    .Verify(query => query.GetFeedsForManager(account.Key));
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                // Arrange
                var controller = GetController<TAccountsController>();
                var account = GetAccount(controller);
                controller.SetCurrentUser(account);
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(account.Key))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // Act
                var result = InvokeAccountInternal(controller);

                // Assert
                var model = ResultAssert.IsView<TAccountViewModel>(result, viewName: controller.AccountAction);
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
            }

            protected abstract ActionResult InvokeAccount(TAccountsController controller);

            private ActionResult InvokeAccountInternal(TAccountsController controller)
            {
                var account = GetAccount(controller);
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);

                return InvokeAccount(controller);
            }
        }

        public abstract class TheChangeEmailSubscriptionBaseAction : AccountsControllerTestContainer
        {
            [Theory]
            [InlineData(true, true)]
            [InlineData(true, false)]
            [InlineData(false, true)]
            [InlineData(false, false)]
            public virtual async Task UpdatesEmailPreferences(bool emailAllowed, bool notifyPackagePushed)
            {
                // Arrange & Act
                var controller = GetController();
                var account = GetAccount(controller);
                var result = await InvokeChangeEmailSubscription(controller, emailAllowed, notifyPackagePushed);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
                GetMock<IUserService>().Verify(u => u.ChangeEmailSubscriptionAsync(account, emailAllowed, notifyPackagePushed));
            }

            [Fact]
            public virtual async Task DisplaysMessageOnUpdate()
            {
                // Arrange & Act
                var controller = GetController();
                var result = await InvokeChangeEmailSubscription(controller);

                // Assert
                Assert.Equal(controller.Messages.EmailPreferencesUpdated, controller.TempData["Message"]);
            }
            
            protected virtual async Task<ActionResult> InvokeChangeEmailSubscription(
                TAccountsController controller,
                bool emailAllowed = true,
                bool notifyPackagePushed = true)
            {
                // Arrange
                controller.SetCurrentUser(GetCurrentUser(controller));

                var account = GetAccount(controller);
                account.Username = "aUsername";
                account.EmailAddress = "test@example.com";
                account.EmailAllowed = !emailAllowed;
                account.NotifyPackagePushed = !notifyPackagePushed;

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);
                userService.Setup(u => u.ChangeEmailSubscriptionAsync(account, emailAllowed, notifyPackagePushed))
                    .Returns(Task.CompletedTask);

                var viewModel = Activator.CreateInstance<TAccountViewModel>();
                viewModel.AccountName = account.Username;
                viewModel.ChangeNotifications = new ChangeNotificationsViewModel
                {
                    EmailAllowed = emailAllowed,
                    NotifyPackagePushed = notifyPackagePushed
                };

                // Act
                return await controller.ChangeEmailSubscription(viewModel);
            }
        }
        public abstract class TheConfirmationRequiredBaseAction : AccountsControllerTestContainer
        {
            [Fact]
            public virtual void SendsNewAccountEmailWhenPosted()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(account);

                account.EmailConfirmationToken = "confirmation";
                account.UnconfirmedEmailAddress = "account@example.com";
                account.EmailAddress = null;

                string sentConfirmationUrl = null;
                MailAddress sentToAddress = null;
                GetMock<IMessageService>()
                    .Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()))
                    .Callback<MailAddress, string>((to, url) =>
                    {
                        sentToAddress = to;
                        sentConfirmationUrl = url;
                    });

                // Act
                var result = InvokeConfirmationRequiredPost(controller, account);

                // Assert
                // We use a catch-all route for unit tests so we can see the parameters
                // are passed correctly.
                Assert.Equal(TestUtility.GallerySiteRootHttps + "account/confirm/account/confirmation", sentConfirmationUrl);
                Assert.Equal("account@example.com", sentToAddress.Address);
            }

            protected virtual ActionResult InvokeConfirmationRequiredPost(
                TAccountsController controller,
                TUser account)
            {
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);

                // Act
                return controller.ConfirmationRequiredPost(account.Username);
            }
        }
    }
}
