# AuthUI

Пользовательские формы авторизации Avalux Auth: вход, регистрация, профиль и смена пароля.
Это отдельное приложение на Svelte 5 + Vite. Оно не использует код и компоненты
Angular-админки (`UI/`) и собирается независимо.

## Маршруты

| Путь | Назначение |
| --- | --- |
| `/login?state=...` | Вход и регистрация |
| `/profile?state=...` | Профиль и смена пароля |

Параметр `state` создаёт backend при редиректе из клиентского приложения. Без него
формы показывают ошибку и не отправляются. Формы лежат в корне `wwwroot` и
отдаются напрямую, редиректов нет.

## Разработка

```bash
npm install
npm run dev
```

Vite поднимает приложение на `http://localhost:5173/login?state=...`. Запросы к `/api`
проксируются на `http://localhost:5000` (см. `vite.config.ts`).

## Сборка

```bash
npm run build
```

Результат попадает в `dist/` и в Dockerfile копируется в
`Api/AvaluxAuth.Api/wwwroot/`. ASP.NET отдаёт `index.html` для `/login` и
`/profile`, а сгенерированные ассеты лежат в `wwwroot/static/` (URL `/static`),
чтобы не пересекаться с `/assets` Angular-админки на том же хосте.

## Проверки

```bash
npm run check   # svelte-check + TypeScript
```
