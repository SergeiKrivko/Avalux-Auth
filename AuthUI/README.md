# AuthUI

Пользовательские формы авторизации Avalux Auth: вход, регистрация, профиль и смена пароля.
Это отдельное приложение на Svelte 5 + Vite. Оно не использует код и компоненты
Angular-админки (`UI/`) и собирается независимо.

## Маршруты

| Путь | Назначение |
| --- | --- |
| `/auth/login?state=...` | Вход и регистрация |
| `/auth/profile?state=...` | Профиль и смена пароля |

Параметр `state` создаёт backend при редиректе из клиентского приложения. Без него
формы показывают ошибку и не отправляются. Старые адреса `/login` и `/profile`
перенаправляются на новые в `Program.cs`.

## Разработка

```bash
npm install
npm run dev
```

Vite поднимает приложение на `http://localhost:5173/auth/`. Запросы к `/api`
проксируются на `http://localhost:5000` (см. `vite.config.ts`).

## Сборка

```bash
npm run build
```

Результат попадает в `dist/`. В Dockerfile он копируется в
`Api/AvaluxAuth.Api/wwwroot/auth/`, откуда ASP.NET раздаёт статику и отдаёт
`auth/index.html` для клиентских маршрутов.

## Проверки

```bash
npm run check   # svelte-check + TypeScript
```
