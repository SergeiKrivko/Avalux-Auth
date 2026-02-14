import {Routes} from '@angular/router';
import {RootPage} from './pages/root.page/root.page';
import {HomePage} from './pages/home.page/home.page';
import {AuthPage} from './pages/auth.page/auth.page';
import {ApplicationPage} from './pages/application.page/application.page';
import {ApplicationConfigPage} from './pages/application-config.page/application-config.page';
import {ApplicationProvidersPage} from './pages/application-providers.page/application-providers.page';
import {ProviderPage} from './pages/provider.page/provider.page';
import {UsersPage} from './pages/users.page/users.page';

export const routes: Routes = [
  {
    path: "", component: RootPage, children: [
      {path: "", component: HomePage},
      {
        path: "app/:id", component: ApplicationPage, children: [
          {path: "config", component: ApplicationConfigPage},
          {path: "providers", component: ApplicationProvidersPage},
          {path: "providers/:providerId", component: ProviderPage},
          {path: "users", component: UsersPage},
        ]
      }
    ]
  },
  {path: "login", component: AuthPage},
  {path: "**", redirectTo: "/", pathMatch: "full"}
];
