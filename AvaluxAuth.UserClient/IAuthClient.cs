using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Avalux.Auth.UserClient.Models;

namespace Avalux.Auth.UserClient;

public interface IAuthClient
{
    public UserCredentials? Credentials { get; set; }

    /// <summary>
    /// True, если клиент авторизован
    /// </summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Токен доступа. Может быть использован для запросов к API Avalux Auth и к
    /// Вашему API, если используется авторизация через Avalux Auth.
    /// </summary>
    [NotNullIfNotNull(nameof(Credentials))]
    public string? AccessToken { get; }

    /// <summary>
    /// Получение ссылки для авторизации через один из провайдеров.
    /// Не проверяет существование провайдера, но для успешной авторизации
    /// он должен быть подключен через интерфейс администратора.
    /// Далее нужно открыть ссылку в браузере, дождаться редиректа на <c>redirectUrl</c>,
    /// получить код (query-параметр <c>code</c>) и вызвать метод <c>GetTokenAsync</c>
    /// </summary>
    /// <param name="provider">Строковый идентификатор провайдера. Например: <c>google</c>, <c>yandex</c>, <c>github</c></param>
    /// <param name="redirectUrl">Url, на который будет перенаправлен пользователь после авторизации в провайдере</param>
    /// <returns></returns>
    public string GetAuthorizationUrl(string provider, string redirectUrl);

    /// <summary>
    /// Завершение авторизации. Авторизует текущий клиент.
    /// </summary>
    /// <param name="code">Код авторизации (query-параметр <c>code</c>)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Пара токенов (доступа + обновления) и время, до которого действителен токен доступа</returns>
    [MemberNotNull(nameof(Credentials))]
    public Task<UserCredentials> GetTokenAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Связывает новый аккаунт с текущим. Требует, чтобы клиент был авторизован.
    /// </summary>
    /// <param name="code">Код авторизации (query-параметр <c>code</c>)</param>
    /// <param name="ct">CancellationToken</param>
    public Task LinkAccountAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Обмен токена обновления на новую пару токенов. Старый токен обновления перестанет действовать.
    /// Старый токен доступа (если был актуален) продолжит действовать до конца срока.
    /// </summary>
    /// <param name="refreshToken">Токен обновления</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Пара токенов (доступа + обновления) и время, до которого действителен токен доступа</returns>
    public Task<UserCredentials> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Обмен токена обновления на новую пару токенов.
    /// Если токен обновления будет активен еще хотя бы минуту, ничего не делает и возвращает параметр <c>credentials</c>.
    /// Старый токен обновления перестанет действовать.
    /// </summary>
    /// <param name="credentials">Данные авторизации пользователя</param>
    /// <param name="force">Следует ли выполнить обновление несмотря на то, что токен доступа еще действует. По умолчанию <c>false</c></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Пара токенов (доступа + обновления) и время, до которого действителен токен доступа</returns>
    public Task<UserCredentials> RefreshTokenAsync(UserCredentials credentials, bool force = false,
        CancellationToken ct = default);

    /// <summary>
    /// Обмен токена обновления на новую пару токенов.
    /// Требует, чтобы текущий клиент был авторизован.
    /// Если токен обновления будет активен еще хотя бы минуту, ничего не делает и возвращает параметр <c>credentials</c>.
    /// Старый токен обновления перестанет действовать.
    /// Если метод выполнен успешно, клиент гарантированно будет авторизован.
    /// </summary>
    /// <param name="force">Следует ли выполнить обновление несмотря на то, что токен доступа еще действует. По умолчанию <c>false</c></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Пара токенов (доступа + обновления) и время, до которого действителен токен доступа</returns>
    [MemberNotNull(nameof(Credentials))]
    public Task<UserCredentials> RefreshTokenAsync(bool force = false, CancellationToken ct = default);

    /// <summary>
    /// Отзыв токена обновления.
    /// После этого токен обновления перестанет действовать.
    /// Связанный токен доступа (если был актуален) продолжит действовать до конца срока.
    /// </summary>
    /// <param name="refreshToken">Токен обновления</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Отзыв токена обновления.
    /// После этого токен обновления перестанет действовать.
    /// Связанный токен доступа (если был актуален) продолжит действовать до конца срока.
    /// </summary>
    /// <param name="credentials">Данные авторизации пользователя</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public Task RevokeTokenAsync(UserCredentials credentials, CancellationToken ct = default);

    /// <summary>
    /// Отзыв токена обновления.
    /// После этого токен обновления перестанет действовать.
    /// Авторизация текущего клиента будет снята.
    /// Если клиент не авторизован, не делает ничего.
    /// </summary>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public Task RevokeTokenAsync(CancellationToken ct = default);

    /// <summary>
    /// Получение информации о пользователе: ID, список аккаунтов и подписок.
    /// Требует, чтобы текущий клиент был авторизован.
    /// </summary>
    /// <param name="ct">CancellationToken</param>
    /// <returns>UserInfo</returns>
    public Task<UserInfo> GetUserInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Получение токена доступа провайдера.
    /// Требует, чтобы текущий клиент был авторизован.
    /// Для провайдера должно быть включено сохранение токенов в интерфейсе администратора.
    /// </summary>
    /// <param name="provider">Строковый идентификатор провайдера. Например: <c>google</c>, <c>yandex</c>, <c>github</c></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns><c>AccountCredentials</c> - токен доступа и время, до которого актуален</returns>
    public Task<AccountCredentials> GetAccountCredentialsAsync(string provider, CancellationToken ct = default);

    /// <summary>
    /// Полный процесс авторизации для десктопного или консольного приложения.
    /// </summary>
    /// <param name="provider">Строковый идентификатор провайдера. Например: <c>google</c>, <c>yandex</c>, <c>github</c></param>
    /// <param name="redirectUrl">Url для приема кода авторизации. Должен быть <c>http://localhost:{port}</c></param>
    /// <param name="ct"></param>
    /// <returns>CancellationToken</returns>
    [SupportedOSPlatform("Windows")]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("Macos")]
    public Task<UserCredentials> AuthorizeInstalledAsync(string provider, string redirectUrl, CancellationToken ct = default);

    /// <summary>
    /// Полный процесс добавления аккаунта для десктопного или консольного приложения.
    /// </summary>
    /// <param name="provider">Строковый идентификатор провайдера. Например: <c>google</c>, <c>yandex</c>, <c>github</c></param>
    /// <param name="redirectUrl">Url для приема кода авторизации. Должен быть <c>http://localhost:{port}</c></param>
    /// <param name="ct"></param>
    /// <returns>CancellationToken</returns>
    [SupportedOSPlatform("Windows")]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("Macos")]
    public Task LinkInstalledAsync(string provider, string redirectUrl, CancellationToken ct = default);
}