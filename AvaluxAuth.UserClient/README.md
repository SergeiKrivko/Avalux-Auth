# Avalux.Auth.UserClient

Клиент API Avalux Auth для интерфейсных приложений, использующих Avalux Auth.

Начиная с 1.4.0 клиент использует PKCE (S256) и `state` для installed-приложений:
`GetAuthorizationUrl`/`GetLinkUrl` генерируют verifier и state, поэтому обмен кода
(`GetTokenAsync`) нужно выполнять на том же экземпляре `AuthClient`; `AuthorizeInstalledAsync`
делает это автоматически. Сервер Avalux Auth требует PKCE для публичных клиентов
(SPA и десктоп-приложения), поэтому клиенты старее 1.4.0 после обновления сервера
не смогут завершить вход.
