import {Routes} from '@angular/router';
import {RootPage} from './pages/root.page/root.page';
import {HomePage} from './pages/home.page/home.page';
import {AuthPage} from './pages/auth.page/auth.page';
import {ApplicationPage} from './pages/application.page/application.page';
import {ApplicationConfigPage} from './pages/application-config.page/application-config.page';
import {ApplicationProvidersPage} from './pages/application-providers.page/application-providers.page';
import {ProviderPage} from './pages/provider.page/provider.page';
import {UsersPage} from './pages/users.page/users.page';
import {TokensPage} from './pages/tokens.page/tokens.page';
import {ApplicationSubscriptionsPage} from './pages/application-subscriptions.page/application-subscriptions.page';
import {SubscriptionPage} from './pages/subscription.page/subscription.page';
import UserLoginPage from './pages/user-login.page/user-login.page';
import {UserProfilePage} from './pages/user-profile/user-profile.page';

export const routes: Routes = [
  {
    path: "", component: RootPage, children: [
      {path: "", component: HomePage},
      {
        path: "app/:id", component: ApplicationPage, children: [
          {path: "config", component: ApplicationConfigPage},
          {path: "providers", component: ApplicationProvidersPage},
          {path: "providers/:providerId", component: ProviderPage},
          {path: "tokens", component: TokensPage},
          {path: "subscriptions", component: ApplicationSubscriptionsPage},
          {path: "subscriptions/:planId", component: SubscriptionPage},
          {path: "users", component: UsersPage},
        ]
      }
    ]
  },
  {path: "admin-auth", component: AuthPage},
  {path: "login", component: UserLoginPage},
  {path: "profile", component: UserProfilePage},
  {path: "**", redirectTo: "/", pathMatch: "full"}
];
